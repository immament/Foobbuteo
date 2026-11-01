// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Common
{
    internal static class LoggerOutput
    {
#if UNITY_EDITOR
        private static readonly string[] EditorColors =
        {
            "",
            "red",
            "black",
            "#ffff00",
            "orange",
            "white",
            "",
            "grey",
        };
        private static readonly string[] EditorLabels =
        {
            "",
            "<color=red>E</color> ",
            "A ",
            "<color=#ffff00>DEV</color> ",
            "<color=orange>W</color> ",
            "<color=white>I </color> ",
            "D ",
            "<color=grey>T</color> ",
        };
#else
        private static readonly string[] PlainLabels =
        {
            "",
            "Error ",
            "Assert ",
            "Dev ",
            "Warn ",
            "Info ",
            "Debug ",
            "Trace ",
        };

        internal static string PlainLevelText(ImmLogger.LogLevel level) => PlainLabels[(int)level];
#endif

        internal static string LevelText(ImmLogger.LogLevel level, bool enabled)
        {
            if (!enabled)
                return string.Empty;
#if UNITY_EDITOR
            return EditorLabels[(int)level];
#elif UNITY_SERVER
            return LoggerConsole.UseAnsi ? LoggerConsole.LevelText(level) : PlainLevelText(level);
#else
            return PlainLevelText(level);
#endif
        }

        internal static string ColorMessage(string message, ImmLogger.LogLevel level)
        {
#if UNITY_EDITOR
            string color = EditorColors[(int)level];
            return string.IsNullOrEmpty(color) ? message : $"<color={color}>{message}</color>";
#elif UNITY_SERVER
            return LoggerConsole.ColorMessage(message, level);
#else
            return message;
#endif
        }
    }
}
