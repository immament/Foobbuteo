// SPDX-License-Identifier: AGPL-3.0-only
using System.Reflection;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class AndroidUiDensityTests
    {
        [Test]
        public void CachedDensity_RefreshesOnlyWhenSimulatedPlatformOrDensityChanges()
        {
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            original.scale = 1.5f;
            var runtime = Object.Instantiate(original);
            var state = new AndroidUiDensityRefreshState();
            try
            {
                Assert.IsTrue(
                    state.RefreshIfChanged(runtime, original, RuntimePlatform.Android, true, 440f),
                    "The first sample must initialize the simulated panel."
                );
                Assert.IsFalse(
                    state.RefreshIfChanged(runtime, original, RuntimePlatform.Android, true, 440f),
                    "An unchanged simulated device must not reapply panel settings."
                );
                Assert.IsTrue(
                    state.RefreshIfChanged(runtime, original, RuntimePlatform.Android, true, 320f),
                    "Selecting a device with another density must refresh scaling."
                );
                Assert.AreEqual(2f, runtime.scale, "The new device must replace the previous density scale.");
                Assert.IsTrue(
                    state.RefreshIfChanged(runtime, original, RuntimePlatform.WindowsEditor, true, 320f),
                    "Returning to the Game view must refresh even when the reported DPI is unchanged."
                );
                Assert.AreEqual(original.scale, runtime.scale, "Leaving simulation must restore the saved scale.");
            }
            finally
            {
                Object.DestroyImmediate(runtime);
                Object.DestroyImmediate(original);
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void CachedDensity_UnavailableDpiDoesNotCauseRepeatedRefreshes(float dpi)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var state = new AndroidUiDensityRefreshState();
            try
            {
                Assert.IsTrue(
                    state.RefreshIfChanged(settings, null, RuntimePlatform.Android, true, dpi),
                    "An unavailable DPI must initialize the panel using the fallback density."
                );
                Assert.IsFalse(
                    state.RefreshIfChanged(settings, null, RuntimePlatform.Android, true, dpi),
                    "Repeated unavailable DPI readings must not continuously refresh settings."
                );
                Assert.AreEqual(1f, settings.scale, "Unavailable DPI must preserve the reference density scale.");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase(PanelScaleMode.ConstantPixelSize)]
        [TestCase(PanelScaleMode.ScaleWithScreenSize)]
        public void AndroidPlayer_UsesDensityScalingForEitherOriginalPanelMode(PanelScaleMode originalMode)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scaleMode = originalMode;
                settings.scale = 2f;
                Assert.IsTrue(
                    AndroidUiDensity.TryApplySettings(settings, RuntimePlatform.Android, false),
                    "An Android player must apply density scaling to both match and lobby panels."
                );
                Assert.AreEqual(
                    PanelScaleMode.ConstantPhysicalSize,
                    settings.scaleMode,
                    "Android panels must keep a consistent physical UI size across screen densities."
                );
                Assert.AreEqual(160f, settings.referenceDpi, "Android UI must use the approved 160-DPI baseline.");
                Assert.AreEqual(160f, settings.fallbackDpi, "Unknown screen DPI must use the same sizing baseline.");
                Assert.AreEqual(1f, settings.scale, "Panel density must not be multiplied by an old scale override.");
                AndroidUiDensity.TryApplySettings(settings, RuntimePlatform.Android, false);
                Assert.AreEqual(1f, settings.scale, "Panels sharing settings must not compound their scaling.");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase(RuntimePlatform.WindowsEditor, true)]
        [TestCase(RuntimePlatform.IPhonePlayer, true)]
        [TestCase(RuntimePlatform.WindowsPlayer, false)]
        [TestCase(RuntimePlatform.WebGLPlayer, false)]
        public void OtherPlatformsAndEditor_PreserveAllPanelSettings(RuntimePlatform platform, bool isEditor)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceDpi = 96f;
                settings.fallbackDpi = 120f;
                settings.scale = 1.5f;
                string before = EditorJsonUtility.ToJson(settings);
                Assert.IsFalse(
                    AndroidUiDensity.TryApplySettings(settings, platform, isEditor),
                    "Density overrides must be restricted to Android players and Android simulation."
                );
                Assert.AreEqual(
                    before,
                    EditorJsonUtility.ToJson(settings),
                    "Other platforms must retain every setting."
                );
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase(440f, 2.75f)]
        [TestCase(320f, 2f)]
        [TestCase(0f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void AndroidSimulator_UsesExplicitDeviceDensity(float dpi, float expectedScale)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scale = 3f;
                Assert.IsTrue(
                    AndroidUiDensity.TryApplySettings(settings, RuntimePlatform.Android, true, dpi),
                    "Android simulation must apply density scaling even though it runs in the Editor."
                );
                Assert.AreEqual(
                    PanelScaleMode.ConstantPixelSize,
                    settings.scaleMode,
                    "Simulator scaling must not depend on the host monitor's physical DPI."
                );
                Assert.AreEqual(expectedScale, settings.scale, "The simulated density must use the 160-DPI baseline.");
                AndroidUiDensity.TryApplySettings(settings, RuntimePlatform.Android, true, dpi);
                Assert.AreEqual(expectedScale, settings.scale, "Repeated updates must not compound density scaling.");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AndroidSimulator_MapsPortraitAndLandscapeSafeAreasToPanelUnits(bool landscape)
        {
            const float dpi = 440f;
            const float expectedShortSide = 1080f / (dpi / 160f);
            var pixels = landscape ? new Vector2(2400, 1080) : new Vector2(1080, 2400);
            var safePixels = landscape ? new Rect(94, 0, 2306, 1080) : new Rect(0, 0, 1080, 2306);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                AndroidUiDensity.TryApplySettings(settings, RuntimePlatform.Android, true, dpi);
                Vector2 panelSize = pixels / settings.scale;
                Assert.IsTrue(
                    UiViewportMetrics.TryCreate(
                        new Rect(Vector2.zero, panelSize),
                        Vector2.zero,
                        panelSize,
                        pixels,
                        safePixels,
                        panelSize,
                        true,
                        true,
                        out var metrics
                    ),
                    "Simulated geometry must yield a usable mobile viewport in either orientation."
                );
                Assert.AreEqual(
                    expectedShortSide,
                    landscape ? metrics.Layout.LogicalSize.y : metrics.Layout.LogicalSize.x,
                    0.01f,
                    "The 1080-pixel side at 440 DPI must measure approximately 393 UI units."
                );
                Assert.AreEqual(
                    safePixels.size / settings.scale,
                    metrics.SafeAreaPanelUnits.size,
                    "The safe area must receive density conversion exactly once."
                );
                Assert.AreEqual(!landscape, metrics.Layout.IsPortrait, "Layout orientation must follow the device.");
                Assert.IsTrue(metrics.Layout.IsMobilePlatform, "Simulated Android must retain mobile layout behavior.");
                Assert.IsTrue(metrics.Layout.SupportsTouch, "Simulated Android must retain touch layout behavior.");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SimulatorChanges_UpdateDensityAndRestoreDesktopSettings()
        {
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            original.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            original.scale = 1.5f;
            original.referenceDpi = 96f;
            original.fallbackDpi = 120f;
            var runtime = Object.Instantiate(original);
            string before = EditorJsonUtility.ToJson(original);
            try
            {
                AndroidUiDensity.RefreshSettings(runtime, original, RuntimePlatform.Android, true, 440f);
                Assert.AreEqual(2.75f, runtime.scale, "Selecting a 440-DPI device must set its density immediately.");
                AndroidUiDensity.RefreshSettings(runtime, original, RuntimePlatform.Android, true, 320f);
                Assert.AreEqual(2f, runtime.scale, "Changing devices must replace the previous density.");
                AndroidUiDensity.RefreshSettings(runtime, original, RuntimePlatform.WindowsEditor, true, 96f);
                Assert.AreEqual(
                    original.scaleMode,
                    runtime.scaleMode,
                    "Leaving simulation must restore desktop scaling."
                );
                Assert.AreEqual(original.scale, runtime.scale, "Leaving simulation must restore the original scale.");
                Assert.AreEqual(original.referenceDpi, runtime.referenceDpi, "Desktop reference DPI must be restored.");
                Assert.AreEqual(original.fallbackDpi, runtime.fallbackDpi, "Desktop fallback DPI must be restored.");
                Assert.AreEqual(
                    before,
                    EditorJsonUtility.ToJson(original),
                    "Simulation must never mutate saved settings."
                );
            }
            finally
            {
                Object.DestroyImmediate(runtime);
                Object.DestroyImmediate(original);
            }
        }

        [TestCase(typeof(AndroidUiDensity))]
        [TestCase(typeof(LobbyPanelScaling))]
        public void PanelLifecycle_RestoresOriginalAndReleasesRuntimeCopy(System.Type scalingType)
        {
            var owner = new GameObject("Android simulation panel test");
            owner.SetActive(false);
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var renderer = owner.AddComponent<PanelRenderer>();
                renderer.panelSettings = original;
                var scaling = owner.AddComponent(scalingType);
                const BindingFlags lifecycleFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                for (int enableCycle = 0; enableCycle < 2; enableCycle++)
                {
                    scalingType.GetMethod("OnEnable", lifecycleFlags).Invoke(scaling, null);
                    var runtime = renderer.panelSettings;
                    Assert.AreNotSame(original, runtime, "Each enable cycle must create an owned settings copy.");
                    AndroidUiDensity.RefreshSettings(runtime, original, RuntimePlatform.Android, true, 440f);
                    Assert.AreEqual(1f, original.scale, "Simulation must preserve the original asset's scale.");
                    scalingType.GetMethod("OnDisable", lifecycleFlags).Invoke(scaling, null);
                    Assert.AreSame(
                        original,
                        renderer.panelSettings,
                        "Disabling must restore the original settings reference."
                    );
                    Assert.IsTrue(runtime == null, "Disabling must release the temporary settings copy.");
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void MissingPanelSettings_DoesNotThrow()
        {
            Assert.IsFalse(
                AndroidUiDensity.TryApplySettings(null, RuntimePlatform.Android, false),
                "A panel without settings must safely skip density configuration."
            );
        }

        [TestCase("Assets/Scenes/LobbyScene.unity")]
        [TestCase("Assets/Scenes/GameScene.unity")]
        public void ClientScenes_ConfigureEveryPanelWithoutChangingSavedScaling(string scenePath)
        {
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                int panels = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<PanelRenderer>(true))
                {
                    panels++;
                    Assert.IsTrue(
                        renderer.GetComponent<AndroidUiDensity>() != null
                            || renderer.GetComponent<LobbyPanelScaling>() != null,
                        $"{scenePath}: panel {renderer.name} must use Android density configuration."
                    );
                    Assert.AreNotEqual(
                        PanelScaleMode.ConstantPhysicalSize,
                        renderer.panelSettings.scaleMode,
                        $"{scenePath}: saved scaling must retain its non-Android default."
                    );
                }
                Assert.Greater(panels, 0, $"{scenePath} must contain the client panels covered by this test.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
