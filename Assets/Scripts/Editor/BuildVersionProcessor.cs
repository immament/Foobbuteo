// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal sealed class BuildVersionProcessor : IPreprocessBuildWithReport
{
    private const string VersionFilePath = "ProjectSettings/BuildVersion.json";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            var isAndroid = report.summary.platform == BuildTarget.Android;
            if (isAndroid && (report.summary.options & BuildOptions.PatchPackage) != 0)
                return;

            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), VersionFilePath);
            var state = ResolveVersion(
                path,
                DateTime.UtcNow,
                ReadReleaseVersion(Environment.GetCommandLineArgs()),
                isAndroid,
                PlayerSettings.Android.bundleVersionCode
            );
            PlayerSettings.bundleVersion = state.Version;
            if (isAndroid)
                PlayerSettings.Android.bundleVersionCode = state.androidVersionCode;

            Debug.Log(
                $"Build version: {PlayerSettings.bundleVersion}"
                    + (isAndroid ? $" (Android code {state.androidVersionCode})" : "")
            );
        }
        catch (Exception exception)
        {
            throw new BuildFailedException($"Cannot assign build version from {VersionFilePath}: {exception.Message}");
        }
    }

    internal static BuildVersionState ReserveVersion(
        string path,
        DateTime today,
        bool isAndroid,
        int currentAndroidVersionCode
    )
    {
        // The tracked counter is reserved before building; failed builds deliberately consume a number.
        // Exclusive access prevents two processes from reserving the same number in one checkout.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var state = JsonUtility.FromJson<BuildVersionState>(reader.ReadToEnd());
        if (state == null)
            throw new ArgumentException("Build version configuration is empty.");
        state.Reserve(today, isAndroid, currentAndroidVersionCode);

        var content = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(state, true) + "\n");
        stream.Position = 0;
        stream.Write(content, 0, content.Length);
        stream.SetLength(content.Length);
        stream.Flush(true);
        return state;
    }

    internal static BuildVersionState ResolveVersion(
        string path,
        DateTime today,
        string releaseVersion,
        bool isAndroid,
        int currentAndroidVersionCode
    )
    {
        if (releaseVersion == null)
            return ReserveVersion(path, today, isAndroid, currentAndroidVersionCode);

        var match = Regex.Match(releaseVersion, @"^(0|[1-9]\d*)\.(\d{2})\.(\d{4})\.([1-9]\d*)$");
        if (
            !match.Success
            || !int.TryParse(match.Groups[1].Value, out var major)
            || !int.TryParse(match.Groups[4].Value, out var index)
            || !DateTime.TryParseExact(
                "20" + match.Groups[2].Value + match.Groups[3].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date
            )
        )
            throw new ArgumentException("CI version must use major.yy.MMdd.dailyBuildIndex with a valid UTC date.");

        var androidVersionCode = currentAndroidVersionCode;
        if (isAndroid)
        {
            // Reserve 9,999 codes per UTC day. The release tag ledger also preserves codes on retries.
            const int codesPerDay = 10000;
            const int maxAndroidVersionCode = 2100000000;
            var days = (date - new DateTime(2020, 1, 1)).Days;
            var reservedCode = (long)days * codesPerDay + index;
            if (days < 0 || index >= codesPerDay || reservedCode > maxAndroidVersionCode)
                throw new ArgumentException("Android CI version is outside the supported date or daily index range.");
            androidVersionCode = (int)reservedCode;
        }

        return new BuildVersionState
        {
            major = major,
            lastBuildDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dailyBuildIndex = index,
            androidVersionCode = androidVersionCode,
        };
    }

    internal static string ReadReleaseVersion(string[] arguments)
    {
        var isCi = Array.IndexOf(arguments, "-foobbuteoCi") >= 0;
        var versionIndex = Array.IndexOf(arguments, "-foobbuteoVersion");
        if (!isCi && versionIndex < 0)
            return null;
        if (
            !isCi
            || versionIndex < 0
            || versionIndex + 1 >= arguments.Length
            || arguments[versionIndex + 1].StartsWith("-", StringComparison.Ordinal)
            || Array.LastIndexOf(arguments, "-foobbuteoVersion") != versionIndex
        )
            throw new ArgumentException("CI builds require -foobbuteoCi -foobbuteoVersion <reserved-version>.");
        return arguments[versionIndex + 1];
    }
}
