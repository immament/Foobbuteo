// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Imma.Common
{
    public class LoggerConfig
    {
        private readonly object m_sync = new();
        private readonly HashSet<string> m_categories = new(StringComparer.OrdinalIgnoreCase);
        private string m_className;
        private string m_classNameColor = "#cccccc";
        private string m_unityObjNameColor = "teal";
        private string m_classPrefix;
#if UNITY_SERVER && !UNITY_EDITOR
        private string m_ansiClassPrefix;
        private string m_ansiUnityObjName;
#endif
        private volatile ImmLogger.LogLevel m_resolvedLevel;
        private volatile int m_explicitLevel = -1;

        public bool logClassName;
        public bool logClassToString;
        public bool logGameObjectName;
        public bool logThreadId;
        public bool logGameTime;
        public bool logLogLevel;
        public bool logUtcTime;

        public string className
        {
            get => m_className;
            set
            {
                lock (m_sync)
                {
                    m_className = value;
                    CachePrefixes();
                }
            }
        }
        public string classNameColor
        {
            get => m_classNameColor;
            set
            {
                lock (m_sync)
                {
                    m_classNameColor = value;
#if UNITY_EDITOR || UNITY_SERVER
                    CachePrefixes();
#endif
                }
            }
        }
        public string unityObjNameColor
        {
            get => m_unityObjNameColor;
            set
            {
                lock (m_sync)
                {
                    m_unityObjNameColor = value;
#if UNITY_SERVER && !UNITY_EDITOR
                    m_ansiUnityObjName = LoggerConsole.Foreground(value);
#endif
                }
            }
        }

        /// <summary>Cached verbosity ceiling. Assigning it establishes an explicit type override.</summary>
        public ImmLogger.LogLevel minLevel
        {
            get
            {
                int explicitLevel = m_explicitLevel;
                return explicitLevel >= 0 ? (ImmLogger.LogLevel)explicitLevel : m_resolvedLevel;
            }
            set => m_explicitLevel = (int)value;
        }

        public IReadOnlyCollection<string> Categories
        {
            get
            {
                lock (m_sync)
                    return new List<string>(m_categories);
            }
        }

        internal bool AddCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Logger category must not be empty.", nameof(category));
            lock (m_sync)
                return m_categories.Add(category.Trim());
        }

        internal void ResolveLevel(ImmLogger.LogLevel level) => m_resolvedLevel = level;

        public LoggerConfig(
            string className,
            bool logClassName = true,
            bool logUnityObjToString = false,
            bool logGameObjectName = true,
            bool logThreadId = false,
            bool logGameTime = false,
            bool logLogLevel = true,
            ImmLogger.LogLevel? minLevel = null,
            bool? logUtcTime = null
        )
        {
            this.logClassName = logClassName;
            this.logClassToString = logUnityObjToString;
            this.logGameObjectName = logGameObjectName;
            this.logThreadId = logThreadId;
            this.logGameTime = logGameTime;
            this.logLogLevel = logLogLevel;
            m_resolvedLevel = ImmLogger.DefaultSubLogLevel;
            if (minLevel.HasValue)
                this.minLevel = minLevel.Value;
#if UNITY_SERVER && !UNITY_EDITOR
            this.logUtcTime = logUtcTime ?? true;
#else
            this.logUtcTime = logUtcTime ?? false;
#endif
            this.className = className;
            if (className.StartsWith("Server."))
                classNameColor = "#5fd7ff";
            else if (className.StartsWith("Client."))
                classNameColor = "#afd787";
            else if (className.EndsWith("ServerGameRoomNBCtrl"))
                classNameColor = "#79dcfd";
            else if (className.EndsWith("ClientGameRoomNBCtrl"))
                classNameColor = "#bad69d";
            unityObjNameColor = m_unityObjNameColor;
        }

        private void CachePrefixes()
        {
#if UNITY_EDITOR
            m_classPrefix = $"<color={m_classNameColor}>[{m_className}]</color> ";
#else
            m_classPrefix = $"[{m_className}] ";
#if UNITY_SERVER
            m_ansiClassPrefix =
                LoggerConsole.AnsiWrap(LoggerConsole.Foreground(m_classNameColor), $"[{m_className}]") + " ";
#endif
#endif
        }

        internal string GameTime() => logGameTime ? $"[{Time.time:F3}] " : string.Empty;

        internal string UtcTime() => logUtcTime ? FormatUtcTime(DateTime.UtcNow) : string.Empty;

        internal static string FormatUtcTime(DateTime utcNow) =>
            "[" + utcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) + "] ";

        internal string ThreadId() => logThreadId ? $"[{Thread.CurrentThread.ManagedThreadId}] " : string.Empty;

        internal string UnityObj(UnityEngine.Object obj) =>
            logClassToString && obj != null ? $"({obj}) " : string.Empty;

        internal string ClassName()
        {
            if (!logClassName)
                return string.Empty;
#if UNITY_SERVER && !UNITY_EDITOR
            return LoggerConsole.UseAnsi ? m_ansiClassPrefix : m_classPrefix;
#else
            return m_classPrefix;
#endif
        }

        internal string UnityObjectName(UnityEngine.Object obj)
        {
            if (!logGameObjectName || obj == null)
                return string.Empty;
#if UNITY_EDITOR
            return $"<color={m_unityObjNameColor}><{obj.name}></color> ";
#elif UNITY_SERVER
            return LoggerConsole.UseAnsi
                ? LoggerConsole.AnsiWrap(m_ansiUnityObjName, $"<{obj.name}>") + " "
                : $"<{obj.name}> ";
#else
            return $"<{obj.name}> ";
#endif
        }
    }
}
