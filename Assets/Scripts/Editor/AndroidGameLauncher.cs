// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_EDITOR && UNITY_ANDROID
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Toolbars;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class AndroidGameLauncher
{
    private const string ToolbarPath = "Foobbuteo/Launch Android";
    private const string MenuPath = "Foobbuteo/Android/Launch Installed Game";
    private const string DefaultDeviceId = "__builtin__target_default";
    private const int AdbTimeoutMilliseconds = 10000;

    private static bool isLaunching;

    [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Right)]
    private static MainToolbarElement CreateButton()
    {
        var content = new MainToolbarContent(
            isLaunching ? "Launching Android…" : "Launch Android",
            "Launch the installed game on the Android profile's Run Device without rebuilding and select it in Android Logcat."
        );
        return new MainToolbarButton(content, Launch) { enabled = !isLaunching };
    }

    [MenuItem(MenuPath, true)]
    private static bool CanLaunch() => !isLaunching;

    [MenuItem(MenuPath)]
    private static async void Launch()
    {
        if (isLaunching)
            return;

        isLaunching = true;
        MainToolbar.Refresh(ToolbarPath);
        try
        {
            var packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrWhiteSpace(packageName) || !Regex.IsMatch(packageName, @"\A[A-Za-z0-9_.]+\z"))
                throw new InvalidOperationException("Set a valid Android package name in Player Settings.");

            var adbName = Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";
            var adbPath = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", adbName);
            if (!File.Exists(adbPath))
                throw new FileNotFoundException("ADB was not found. Check Preferences > External Tools > Android SDK.");

            var deviceId = GetSelectedDeviceId();
            var deviceArgument = string.IsNullOrEmpty(deviceId) ? "" : $"-s \"{deviceId}\" ";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"{deviceArgument}shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.Start();
            var cancelled = false;
            void CancelLaunch()
            {
                cancelled = true;
                StopAdb(process);
            }

            AssemblyReloadEvents.beforeAssemblyReload += CancelLaunch;
            EditorApplication.quitting += CancelLaunch;
            try
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                var exited = await Task.Run(() => process.WaitForExit(AdbTimeoutMilliseconds));
                if (!exited)
                {
                    StopAdb(process);
                    throw new TimeoutException("ADB launch timed out. Check the device connection and USB debugging.");
                }

                var messages = await Task.WhenAll(output, error);
                if (cancelled)
                    return;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"ADB exited with code {process.ExitCode}: {string.Join(Environment.NewLine, messages).Trim()}"
                            + "\nCheck that the game is installed and select a connected Run Device in Build Profiles."
                    );
                }

                var deviceLabel = string.IsNullOrEmpty(deviceId) ? "ADB's default device" : deviceId;
                Debug.Log($"Android launch: started or resumed {packageName} on {deviceLabel}.");
                SelectLogcatProcess(deviceId, packageName);
            }
            finally
            {
                AssemblyReloadEvents.beforeAssemblyReload -= CancelLaunch;
                EditorApplication.quitting -= CancelLaunch;
                StopAdb(process);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Android launch failed: {exception.Message}");
        }
        finally
        {
            isLaunching = false;
            MainToolbar.Refresh(ToolbarPath);
        }
    }

    private static void SelectLogcatProcess(string deviceId, string packageName)
    {
        try
        {
            // Logcat 1.4.7 keeps its window internal; reuse its PID discovery and reconnect flow.
            const string windowTypeName =
                "Unity.Android.Logcat.AndroidLogcatConsoleWindow, Unity.Mobile.AndroidLogcat.Editor";
            var windowType = Type.GetType(windowTypeName, throwOnError: true);
            var selectMethod = windowType.GetMethod(
                "SetAutoSelect",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null
            );
            if (selectMethod == null)
                throw new MissingMethodException(windowType.FullName, "SetAutoSelect");

            var window = EditorWindow.GetWindow(windowType, false, "Android Logcat", true);
            selectMethod.Invoke(window, new object[] { deviceId, packageName });
        }
        catch (Exception exception)
        {
            // A missing or changed Logcat package must not turn a successful launch into a failure.
            Debug.LogWarning(
                $"Android launch succeeded, but Android Logcat could not select {packageName}: "
                    + exception.GetBaseException().Message
            );
        }
    }

    private static string GetSelectedDeviceId()
    {
        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile == null)
            return "";

        using var serializedProfile = new SerializedObject(profile);
        // Unity exposes the profile's Run Device only through its serialized platform settings.
        var deviceProperty = serializedProfile.FindProperty("m_PlatformBuildProfile.m_CurrentDeploymentTargetId");
        if (deviceProperty == null)
            throw new InvalidOperationException("Could not read the Android profile's Run Device setting.");

        var deviceId = deviceProperty.stringValue;
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId == DefaultDeviceId)
            return "";

        if (deviceId.StartsWith("__builtin__", StringComparison.Ordinal))
            throw new InvalidOperationException("Select a specific connected Run Device in Build Profiles.");

        if (!Regex.IsMatch(deviceId, @"\A[A-Za-z0-9_.:%\[\]-]+\z"))
            throw new InvalidOperationException("The selected Run Device has an invalid ADB identifier.");

        return deviceId;
    }

    private static void StopAdb(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch (InvalidOperationException)
        {
            // The process can exit between checking HasExited and calling Kill.
        }
        catch (Win32Exception exception)
        {
            Debug.LogWarning($"Android launch: could not stop the ADB command: {exception.Message}");
        }
    }
}
#endif
