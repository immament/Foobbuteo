// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

internal sealed class AndroidPatchGarminWorkaround : IPreprocessBuildWithReport
{
    private const string GarminPackage = "com.garmin.android.apps.connectmobile";
    private const int AdbTimeoutMilliseconds = 5000;

    public int callbackOrder => -1100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android || (report.summary.options & BuildOptions.PatchPackage) == 0)
            return;

        // Temporary workaround: Unity 6000.6.4f1 mistakes Garmin's logcat child for the target app,
        // then spends 30 seconds retrying force-stop. Remove once Unity's process detection is fixed.
        try
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            var device = "";
            if (profile != null)
            {
                using var serializedProfile = new SerializedObject(profile);
                // No public device-selection API; fall back to ADB's default if this field changes.
                device = serializedProfile
                    .FindProperty("m_PlatformBuildProfile.m_CurrentDeploymentTargetId")
                    ?.stringValue;
            }

            var adbName =
                UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";
            var adbPath = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", adbName);
            var hasDevice = !string.IsNullOrWhiteSpace(device);
            var deviceArgument = hasDevice ? $"-s \"{device}\" " : "";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"{deviceArgument}shell am force-stop {GarminPackage}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                },
            };

            process.Start();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(AdbTimeoutMilliseconds))
            {
                process.Kill();
                Debug.LogWarning("Android Patch workaround: stopping Garmin timed out; continuing build.");
                return;
            }

            if (process.ExitCode != 0)
            {
                Debug.LogWarning(
                    $"Android Patch workaround: could not stop Garmin: {error.GetAwaiter().GetResult().Trim()}"
                );
                return;
            }

            Debug.Log(
                $"Android Patch workaround: force-stopped Garmin on {(hasDevice ? device : "ADB default device")}."
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Android Patch workaround: could not stop Garmin; continuing build. {exception.Message}");
        }
    }
}
#endif
