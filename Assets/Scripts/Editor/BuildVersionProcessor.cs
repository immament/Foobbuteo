// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.IO;
using System.Text;
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
            var state = ReserveVersion(path, DateTime.Now, isAndroid, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.bundleVersion = state.Version + GetSuffix(PlayerSettings.bundleVersion);
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

    internal static string GetSuffix(string currentVersion)
    {
        foreach (var suffix in new[] { "-d", "-f", "-r" })
        {
            if (currentVersion.EndsWith(suffix, StringComparison.Ordinal))
                return suffix;
        }
        return "";
    }
}
