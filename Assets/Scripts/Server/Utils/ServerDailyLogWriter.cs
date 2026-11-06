// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_SERVER && !UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Imma.Foobbuteo.Server
{
    internal static class ServerDailyLogWriter
    {
        private static readonly object Lock = new();
        private static readonly Regex AnsiEscapeSequence = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
        private static readonly Regex UnityColorTag = new(
            @"</?color(?:=[^>]+)?>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static StreamWriter s_writer;
        private static DateTime s_currentDay;
        private static string s_logDirectory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var serverDirectory = Path.GetDirectoryName(Application.dataPath);
            var configuredLogDirectory = ServerConfig.Config.Logging.LogDirectory;

            if (string.IsNullOrWhiteSpace(configuredLogDirectory))
                return;

            s_logDirectory = Path.IsPathRooted(configuredLogDirectory)
                ? configuredLogDirectory
                : Path.Combine(serverDirectory, configuredLogDirectory);

            Console.Out.WriteLine($"logDirectory: {s_logDirectory}");

            Directory.CreateDirectory(s_logDirectory);

            Application.logMessageReceivedThreaded += WriteLog;
            Application.quitting += Shutdown;
        }

        private static void WriteLog(string message, string stackTrace, LogType logType)
        {
            lock (Lock)
            {
                try
                {
                    var now = DateTime.Now;
                    EnsureWriter(now);
                    var fileMessage = RemoveColors(message);

                    s_writer.WriteLine($"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logType}] {fileMessage}");

                    if (
                        !string.IsNullOrWhiteSpace(stackTrace)
                        && (logType == LogType.Error || logType == LogType.Assert || logType == LogType.Exception)
                    )
                    {
                        s_writer.WriteLine(stackTrace);
                    }
                }
                catch (Exception exception)
                {
                    // Do not use Debug.Log here: that would call WriteLog again.
                    Console.Error.WriteLine($"Failed to write server log: {exception}");
                }
            }
        }

        private static string RemoveColors(string message)
        {
            var withoutAnsi = AnsiEscapeSequence.Replace(message, string.Empty);
#if UNITY_EDITOR
            return UnityColorTag.Replace(withoutAnsi, string.Empty);
#else
            return withoutAnsi;
#endif
        }

        private static void EnsureWriter(DateTime now)
        {
            var day = now.Date;

            if (s_writer != null && day == s_currentDay)
                return;

            s_writer?.Dispose();

            s_currentDay = day;

            var logPath = Path.Combine(s_logDirectory, $"foobbuteo2d-server-{day:yyyy-MM-dd}.log");

            var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            s_writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
        }

        private static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= WriteLog;

            lock (Lock)
            {
                s_writer?.Dispose();
                s_writer = null;
            }
        }
    }
}

#endif
