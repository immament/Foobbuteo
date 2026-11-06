// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Imma.Common
{
    public static class ImmLogger
    {
        public enum LogLevel
        {
            None = 0,
            Error,
            Assert,
            Dev,
            Warn,
            Info,
            Debug,
            Trace,
        }

        public enum ConfigurationSource
        {
            BaseFile,
            JavaScript,
            Editor,
        }

        private static readonly object Sync = new();
        private static readonly Dictionary<Type, LoggerConfig> Loggers = new();
        private static readonly Dictionary<string, LogLevel>[] CategoryLevels =
        {
            new(StringComparer.OrdinalIgnoreCase),
            new(StringComparer.OrdinalIgnoreCase),
            new(StringComparer.OrdinalIgnoreCase),
        };
        private static volatile LogLevel s_level = LogLevel.Warn;
        private static readonly LogLevel?[] GlobalLevels = new LogLevel?[CategoryLevels.Length];
        private static readonly LogLevel?[] DefaultLevels = new LogLevel?[CategoryLevels.Length];
        private static LogLevel s_defaultSubLogLevel = LogLevel.Warn;
        private static ILogHandler s_logHandler;
        private static int s_mainThreadId;

        public static LogLevel Level
        {
            get => s_level;
            set => s_level = value;
        }
        public static LogLevel DefaultSubLogLevel
        {
            get
            {
                lock (Sync)
                    return s_defaultSubLogLevel;
            }
            set
            {
                lock (Sync)
                {
                    s_defaultSubLogLevel = value;
                    RefreshLevels();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            lock (Sync)
            {
                s_level = LogLevel.Info;
                s_defaultSubLogLevel = LogLevel.Info;
                Loggers.Clear();
                Array.Clear(GlobalLevels, 0, GlobalLevels.Length);
                Array.Clear(DefaultLevels, 0, DefaultLevels.Length);
                foreach (var layer in CategoryLevels)
                    layer.Clear();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            s_logHandler = Debug.unityLogger.logHandler;
#if UNITY_SERVER && !UNITY_EDITOR
            LoggerConsole.Initialize();
#endif
        }

        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == s_mainThreadId;
        internal static ILogHandler Handler =>
            s_logHandler
            ?? throw new InvalidOperationException(
                "ImmLogger must be initialized on the Unity main thread before logging."
            );

        public static LoggerConfig GetLoggerByType<T>() => GetLoggerByType(typeof(T));

        public static LoggerConfig GetLoggerByType(Type type) => AddType(type);

        public static LoggerConfig AddType(Type type, params string[] categories)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            categories ??= Array.Empty<string>();
            foreach (string category in categories)
                if (string.IsNullOrWhiteSpace(category))
                    throw new ArgumentException("Logger category must not be empty.", nameof(categories));
            lock (Sync)
            {
                bool changed = false;
                if (!Loggers.TryGetValue(type, out var config))
                {
                    changed = true;
                    config = new LoggerConfig((type.FullName ?? type.Name).Replace("Imma.Foobbuteo.", string.Empty));
#if UNITY_SERVER && !UNITY_EDITOR
                    config.logGameTime = true;
#endif
                    foreach (string segment in (type.Namespace ?? string.Empty).Split('.'))
                        if (segment == "Server" || segment == "Client" || segment == "Auth")
                            config.AddCategory(segment);
                    Loggers.Add(type, config);
                }
                foreach (string category in categories)
                    changed |= config.AddCategory(category);
                if (changed)
                    ResolveLevel(config);
                return config;
            }
        }

        public static void Configure(
            LoggingSettings settings,
            ConfigurationSource source = ConfigurationSource.BaseFile
        )
        {
            if (settings == null)
                return;
            lock (Sync)
            {
                if (settings.LogLevel.HasValue)
                {
                    GlobalLevels[(int)source] = settings.LogLevel;
                    s_level = ResolveConfiguredLevel(GlobalLevels);
                }
                if (settings.DefaultSubLogLevel.HasValue)
                {
                    DefaultLevels[(int)source] = settings.DefaultSubLogLevel;
                    s_defaultSubLogLevel = ResolveConfiguredLevel(DefaultLevels);
                }
                foreach (var pair in settings.Categories)
                    CategoryLevels[(int)source][pair.Key] = pair.Value;
                RefreshLevels();
            }
        }

        private static LogLevel ResolveConfiguredLevel(LogLevel?[] levels)
        {
            for (int layer = levels.Length - 1; layer >= 0; layer--)
                if (levels[layer].HasValue)
                    return levels[layer].Value;
            throw new InvalidOperationException("No log level has been configured.");
        }

        private static void RefreshLevels()
        {
            foreach (var config in Loggers.Values)
                ResolveLevel(config);
        }

        private static void ResolveLevel(LoggerConfig config)
        {
            LogLevel? categoryLevel = null;
            foreach (string category in config.Categories)
            {
                for (int layer = CategoryLevels.Length - 1; layer >= 0; layer--)
                {
                    if (!CategoryLevels[layer].TryGetValue(category, out var level))
                        continue;
                    if (!categoryLevel.HasValue || level < categoryLevel.Value)
                        categoryLevel = level;
                    break;
                }
            }
            config.ResolveLevel(categoryLevel ?? s_defaultSubLogLevel);
        }

        public static bool IsEnabled<T>(LogLevel level) => IsEnabled(typeof(T), level);

        public static bool IsEnabled(this UnityEngine.Object caller, LogLevel level) =>
            IsEnabled(caller.GetType(), level);

        public static bool IsEnabled(Type type, LogLevel level) => TryGetEnabled(type, level, out _);

        private static bool TryGetEnabled(Type type, LogLevel level, out LoggerConfig config)
        {
            config = null;
            if (level == LogLevel.None || s_level < level)
                return false;
            lock (Sync)
            {
                if (!Loggers.TryGetValue(type, out config))
                    config = AddType(type);
                return config.minLevel >= level;
            }
        }

        public static void SetLoggerLevel<T>(LogLevel level) => GetLoggerByType<T>().minLevel = level;

        public static void SetLoggerLevel(this UnityEngine.Object caller, LogLevel level) =>
            GetLoggerByType(caller.GetType()).minLevel = level;

        public static void SetLoggerLevel(this UnityEngine.Object caller, NullableLogLevel level)
        {
            if (level.HasValue)
                caller.SetLoggerLevel(level.Value);
        }

        public static void SetLoggerOptions(
            this UnityEngine.Object caller,
            NullableLogLevel level,
            Color classNameColor,
            Color unityObjNameColor,
            params string[] categories
        )
        {
            var config = AddType(caller.GetType(), categories);
            if (level.HasValue)
                config.minLevel = level.Value;
            if (classNameColor.a != 0)
                config.classNameColor = $"#{ColorUtility.ToHtmlStringRGBA(classNameColor)}";
            if (unityObjNameColor.a != 0)
                config.unityObjNameColor = $"#{ColorUtility.ToHtmlStringRGBA(unityObjNameColor)}";
        }

        public static bool RemoveType<T>() => RemoveType(typeof(T));

        public static bool RemoveType(Type type)
        {
            lock (Sync)
                return Loggers.Remove(type);
        }

        [HideInCallstack]
        public static void LogException(this UnityEngine.Object caller, Exception ex) =>
            WriteException(caller.GetType(), caller, ex);

        [HideInCallstack]
        public static void LException<T>(this T caller, Exception ex)
            where T : class => WriteException(caller?.GetType() ?? typeof(T), caller as UnityEngine.Object, ex);

        [HideInCallstack]
        public static void LExceptionTree<T>(this T caller, Exception ex, string messagePrefix = null)
            where T : class
        {
            if (!IsEnabled(caller?.GetType() ?? typeof(T), LogLevel.Error))
                return;
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (messagePrefix != null)
                    caller.LWarn(() => $"{messagePrefix}: {current.Message}");
                caller.LException(current);
            }
        }

        [HideInCallstack]
        private static void WriteException(Type type, UnityEngine.Object context, Exception exception)
        {
            if (TryGetEnabled(type, LogLevel.Error, out _))
                Handler.LogException(exception, context);
        }

        #region ++ LogError / LError ++

        [HideInCallstack]
        public static void LogError(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Error,
                LogLevel.Error,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogError(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Error,
                LogLevel.Error,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LError<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Error,
                LogLevel.Error,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LError<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Error,
                LogLevel.Error,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LError<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Error,
                LogLevel.Error,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LError<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Error,
                LogLevel.Error,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogError / LError --

        #region ++ LogAssert / LAssert ++

        [HideInCallstack]
        public static void LogAssert(
            this UnityEngine.Object unityObj,
            bool condition,
            object message = null,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            if (condition)
                return;
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Assert,
                LogLevel.Assert,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogAssert(
            this UnityEngine.Object unityObj,
            bool condition,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            if (condition)
                return;
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Assert,
                LogLevel.Assert,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LAssert<T>(
            this T caller,
            bool condition,
            object message = null,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            if (condition)
                return;
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Assert,
                LogLevel.Assert,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LAssert<T>(
            this T caller,
            bool condition,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            if (condition)
                return;
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Assert,
                LogLevel.Assert,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LAssert<T>(
            bool condition,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            if (condition)
                return;
            Write(
                typeof(T),
                null,
                LogType.Assert,
                LogLevel.Assert,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LAssert<T>(
            bool condition,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            if (condition)
                return;
            Write(
                typeof(T),
                null,
                LogType.Assert,
                LogLevel.Assert,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogAssert / LAssert --

        #region ++ LogWarn / LWarn ++

        [HideInCallstack]
        public static void LogWarn(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Warning,
                LogLevel.Warn,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogWarn(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Warning,
                LogLevel.Warn,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LWarn<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Warning,
                LogLevel.Warn,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LWarn<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
            where T : class
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Warning,
                LogLevel.Warn,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LWarn<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Warning,
                LogLevel.Warn,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LWarn<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Warning,
                LogLevel.Warn,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogWarn / LWarn --

        #region ++ LogDev / LDev ++

        [HideInCallstack]
        public static void LogDev(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Warning,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogDev(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Warning,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDev<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Warning,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDev<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Warning,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDev<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Warning,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDev<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Warning,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogDev / LDev --

        #region ++ LogDevLog / LDevLog ++

        [HideInCallstack]
        public static void LogDevLog(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogDevLog(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDevLog<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDevLog<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDevLog<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Dev,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDevLog<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Dev,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogDevLog / LDevLog --

        #region ++ LogInfo / LInfo ++

        [HideInCallstack]
        public static void LogInfo(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Info,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogInfo(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Info,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LInfo<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Info,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LInfo<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Info,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LInfo<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Info,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LInfo<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Info,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogInfo / LInfo --

        #region ++ LogDebug / LDebug ++

        [HideInCallstack]
        public static void LogDebug(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Debug,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogDebug(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Debug,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDebug<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Debug,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDebug<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Debug,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDebug<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Debug,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LDebug<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Debug,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogDebug / LDebug --

        #region ++ LogTrace / LTrace ++

        [HideInCallstack]
        public static void LogTrace(
            this UnityEngine.Object unityObj,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Trace,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LogTrace(
            this UnityEngine.Object unityObj,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                unityObj.GetType(),
                unityObj,
                LogType.Log,
                LogLevel.Trace,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LTrace<T>(
            this T caller,
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Trace,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LTrace<T>(
            this T caller,
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                caller?.GetType() ?? typeof(T),
                caller as UnityEngine.Object,
                LogType.Log,
                LogLevel.Trace,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LTrace<T>(
            object message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Trace,
                message,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        [HideInCallstack]
        public static void LTrace<T>(
            Func<string> messageFactory,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0
        )
        {
            Write(
                typeof(T),
                null,
                LogType.Log,
                LogLevel.Trace,
                messageFactory,
                callerMemberName,
                callerFilePath,
                callerLineNumber
            );
        }

        #endregion -- LogTrace / LTrace --

        [HideInCallstack]
        private static void Write(
            Type type,
            UnityEngine.Object context,
            LogType logType,
            LogLevel level,
            object message,
            string member,
            string file,
            int line
        )
        {
            if (!TryGetEnabled(type, level, out var config))
                return;
            var formatted = FormatMsg(ToJsonOrString(message), member, file, line);
            var coloredMessage = LoggerOutput.ColorMessage(formatted, level);
#if UNITY_EDITOR
            if (context != null)
                coloredMessage += GetIsEditorText();
#endif
            // Context/name and game-time access require the Unity main thread. Plain background logs omit game time.
            Handler.LogFormat(
                logType,
                context,
                "{0}{1}{2}{3}{4}{5}{6}",
                LoggerOutput.LevelText(level, config.logLogLevel),
                config.ThreadId(),
                config.UtcTime(),
                IsMainThread ? config.GameTime() : string.Empty,
                context == null ? string.Empty : config.UnityObj(context) + config.UnityObjectName(context),
                config.ClassName(),
                coloredMessage
            );
        }

#if UNITY_EDITOR
        private static string GetIsEditorText() =>
            IsMainThread && !Application.isPlaying ? " <color=grey>(Editor)</color>" : string.Empty;
#endif

        public static string FormatMsg(
            string message,
            string callerMemberName,
            string callerFilePath,
            int callerLineNumber
        )
        {
#if UNITY_EDITOR
            return $"<a href=\"{callerFilePath}\" line=\"{callerLineNumber}\">{callerMemberName}:{callerLineNumber}</a> {message}";
#else
            return $"({callerMemberName}:{callerLineNumber}) {message}";
#endif
        }

        public static string ToJsonOrString(object value) =>
            value switch
            {
                null => string.Empty,
                string text => text,
                Func<string> factory => factory() ?? string.Empty,
                _ => value.ToString() ?? string.Empty,
            };
    }
}
