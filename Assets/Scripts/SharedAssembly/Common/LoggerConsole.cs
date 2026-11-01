// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_SERVER && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace Imma.Common
{
    internal static class LoggerConsole
    {
        private const string Reset = "\u001b[0m";
        private const double MinimumLuminance = 0.65;
        internal static bool UseAnsi { get; private set; }
        private static readonly string[] AnsiColors =
        {
            "",
            "\u001b[31m",
            "\u001b[35m",
            "\u001b[93m",
            "\u001b[33m",
            "\u001b[37m",
            "\u001b[36m",
            "\u001b[90m",
        };
        private static readonly string[] AnsiLabels = CreateAnsiLabels();

        internal static void Initialize()
        {
            UseAnsi = false;
#if UNITY_STANDALONE_WIN
            // stdout must be a console: redirected output and file logging stay plain.
            var output = GetStdHandle(StandardOutputHandle);
            if (GetConsoleMode(output, out uint mode))
                UseAnsi = SetConsoleMode(output, mode | EnableProcessedOutput | EnableVirtualTerminalProcessing);
#else
            UseAnsi = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("TERM") != "dumb";
#endif
        }

        private static string[] CreateAnsiLabels()
        {
            var labels = new string[AnsiColors.Length];
            for (int index = 0; index < labels.Length; index++)
                labels[index] = AnsiWrap(AnsiColors[index], LoggerOutput.PlainLevelText((ImmLogger.LogLevel)index));
            return labels;
        }

        internal static string LevelText(ImmLogger.LogLevel level) => AnsiLabels[(int)level];

        internal static string ColorMessage(string message, ImmLogger.LogLevel level) =>
            UseAnsi ? AnsiWrap(AnsiColors[(int)level], message) : message;

        internal static string AnsiWrap(string color, string text) =>
            string.IsNullOrEmpty(color) ? text : $"{color}{text}{Reset}";

        // Pure managed conversion: logger registration may run on a worker thread.
        internal static string Foreground(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            if (NamedColors.TryGetValue(html, out string named))
                html = named;
            if (html[0] != '#')
                return string.Empty;
            string hex = html.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
                hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length == 8)
                hex = hex.Substring(0, 6);
            if (
                hex.Length != 6
                || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)
            )
                return string.Empty;
            double red = (rgb >> 16) / 255.0,
                green = ((rgb >> 8) & 255) / 255.0,
                blue = (rgb & 255) / 255.0;
            double luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
            double blend = luminance < MinimumLuminance ? (MinimumLuminance - luminance) / (1 - luminance) : 0;
            return $"\u001b[38;2;{Math.Round((red + (1 - red) * blend) * 255)};{Math.Round((green + (1 - green) * blend) * 255)};{Math.Round((blue + (1 - blue) * blend) * 255)}m";
        }

        private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = "#ff0000",
            ["cyan"] = "#00ffff",
            ["blue"] = "#0000ff",
            ["darkblue"] = "#0000a0",
            ["lightblue"] = "#add8e6",
            ["purple"] = "#800080",
            ["yellow"] = "#ffff00",
            ["lime"] = "#00ff00",
            ["fuchsia"] = "#ff00ff",
            ["white"] = "#ffffff",
            ["silver"] = "#c0c0c0",
            ["grey"] = "#808080",
            ["black"] = "#000000",
            ["orange"] = "#ffa500",
            ["brown"] = "#a52a2a",
            ["maroon"] = "#800000",
            ["green"] = "#008000",
            ["olive"] = "#808000",
            ["navy"] = "#000080",
            ["teal"] = "#008080",
            ["aqua"] = "#00ffff",
            ["magenta"] = "#ff00ff",
            ["gray"] = "#808080",
            ["clear"] = "#000000",
        };

#if UNITY_STANDALONE_WIN
        private const int StandardOutputHandle = -11;
        private const uint EnableProcessedOutput = 0x0001;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleMode(IntPtr handle, uint mode);
#endif
    }
}
#endif
