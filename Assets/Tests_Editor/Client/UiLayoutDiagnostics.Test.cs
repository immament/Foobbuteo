// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Reflection;
using Imma.Common;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public class UiLayoutDiagnosticsTests
    {
        [UnityTest]
        public IEnumerator LateEnableLogsExistingUiAndDisableCancelsPendingSnapshots()
        {
            ImmLogger.Level = ImmLogger.LogLevel.Info;
            var logger = ImmLogger.GetLoggerByType<UiLayoutDiagnostics>();
            var previousLevel = logger.minLevel;
            const string objectName = "diagnostics-lifecycle-test";
            var instance = new GameObject(objectName);
            instance.SetActive(false);
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var tree = ScriptableObject.CreateInstance<VisualTreeAsset>();
            int snapshots = 0;
            void OnLog(string text, string stack, LogType type)
            {
                if (text.Contains("[UiLayoutDiagnostics]") && text.Contains("object=" + objectName))
                    snapshots++;
            }
            Application.logMessageReceived += OnLog;
            try
            {
                var renderer = instance.AddComponent<PanelRenderer>();
                renderer.enabled = false;
                var diagnostics = instance.AddComponent<UiLayoutDiagnostics>();
                diagnostics.enabled = false;
                diagnostics.SetLoggerLevel(ImmLogger.LogLevel.Info);
                var rootField = typeof(PanelRenderer).GetField(
                    "m_RootVisualElement",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var root = (VisualElement)
                    Activator.CreateInstance(
                        rootField.FieldType,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new object[] { renderer, tree },
                        null
                    );
                rootField.SetValue(renderer, root);
                instance.SetActive(true);
                window.Show();
                window.rootVisualElement.Add(root);
                yield return TestUtils.WaitForUi(() => root?.panel != null, "diagnostic test panel attachment");
                Assert.AreEqual(0, snapshots, "A disabled component must not log when its panel loads.");

                SetEnabled(diagnostics, true);
                yield return TestUtils.WaitForUi(() => snapshots > 0, "late-enabled diagnostics snapshot");
                diagnostics.LogNow();
                SetEnabled(diagnostics, false);
                int beforeDisable = snapshots;
                root.style.width = 320;
                bool checkpointReached = false;
                root.schedule.Execute(() => checkpointReached = true);
                yield return TestUtils.WaitForUi(() => checkpointReached, "panel update after disabling diagnostics");
                Assert.AreEqual(
                    beforeDisable,
                    snapshots,
                    "Disabled diagnostics must cancel queued logging and ignore geometry changes."
                );

                SetEnabled(diagnostics, true);
                yield return TestUtils.WaitForUi(
                    () => snapshots > beforeDisable,
                    "snapshot after re-enabling diagnostics"
                );
            }
            finally
            {
                SetEnabled(instance.GetComponent<UiLayoutDiagnostics>(), false);
                Application.logMessageReceived -= OnLog;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(tree);
                window.Close();
                Object.DestroyImmediate(window);
                logger.minLevel = previousLevel;
            }
        }

        private static void SetEnabled(UiLayoutDiagnostics diagnostics, bool enabled)
        {
            diagnostics.enabled = enabled;
            // Edit Mode tests explicitly drive lifecycle methods of runtime-only MonoBehaviours.
            typeof(UiLayoutDiagnostics)
                .GetMethod(enabled ? "OnEnable" : "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(diagnostics, null);
        }
    }
}
