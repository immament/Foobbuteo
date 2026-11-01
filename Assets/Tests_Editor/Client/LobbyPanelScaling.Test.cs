// SPDX-License-Identifier: AGPL-3.0-only
using System.Reflection;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class LobbyPanelScalingTests
    {
        [Test]
        public void WebGl_CachedGeometryRefreshesOnResizeAndRenderDensityChanges()
        {
            var owner = new GameObject("Cached WebGL scaling test");
            owner.SetActive(false);
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            LobbyPanelScaling scaling = null;
            try
            {
                var renderer = owner.AddComponent<PanelRenderer>();
                renderer.panelSettings = original;
                scaling = owner.AddComponent<LobbyPanelScaling>();
                typeof(LobbyPanelScaling)
                    .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                var cssSize = new Vector2(400, 800);
                var screenPixels = cssSize * 2f;

                Assert.IsTrue(
                    scaling.RefreshWebGlSettingsIfChanged(screenPixels, cssSize),
                    "The first valid browser size must initialize scaling."
                );
                Assert.IsFalse(
                    scaling.RefreshWebGlSettingsIfChanged(screenPixels, cssSize),
                    "An unchanged browser size must not reapply settings."
                );
                Assert.IsTrue(
                    scaling.RefreshWebGlSettingsIfChanged(screenPixels, cssSize / 2f),
                    "A CSS resize must refresh even when render dimensions stay unchanged."
                );
                Assert.AreEqual(4f, renderer.panelSettings.scale, "The CSS resize must update the pixel ratio.");
                Assert.IsTrue(
                    scaling.RefreshWebGlSettingsIfChanged(screenPixels / 2f, cssSize / 2f),
                    "A render density change must refresh even when the CSS size stays unchanged."
                );
                Assert.AreEqual(
                    2f,
                    renderer.panelSettings.scale,
                    "The new render density must update the pixel ratio."
                );

                typeof(LobbyPanelScaling)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                typeof(LobbyPanelScaling)
                    .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                Assert.IsTrue(
                    scaling.RefreshWebGlSettingsIfChanged(screenPixels / 2f, cssSize / 2f),
                    "Re-enabling must initialize the new settings copy even when browser dimensions are unchanged."
                );
            }
            finally
            {
                if (scaling != null)
                    typeof(LobbyPanelScaling)
                        .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(scaling, null);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void Lifecycle_OwnsRuntimeCopyRestoresOriginalAndReleasesCopy()
        {
            var owner = new GameObject("Lobby panel scaling test");
            owner.SetActive(false);
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var renderer = owner.AddComponent<PanelRenderer>();
                renderer.panelSettings = original;
                var scaling = owner.AddComponent<LobbyPanelScaling>();
                typeof(LobbyPanelScaling)
                    .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                var runtime = renderer.panelSettings;
                float initialScale = runtime.scale;
                Assert.AreNotSame(
                    original,
                    runtime,
                    "The lobby must own a copy rather than mutate the saved settings."
                );
                runtime.scale = 3f;
                Assert.AreEqual(1f, original.scale, "Changing runtime scaling must preserve the original asset.");
                typeof(LobbyPanelScaling)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                Assert.AreSame(
                    original,
                    renderer.panelSettings,
                    "Disabling scaling must restore the original settings."
                );
                Assert.IsTrue(runtime == null, "The owned settings copy must be released on disable.");
                typeof(LobbyPanelScaling)
                    .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
                Assert.AreEqual(
                    initialScale,
                    renderer.panelSettings.scale,
                    "Re-enabling must recalculate the initial scale without retaining runtime overrides."
                );
                typeof(LobbyPanelScaling)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(scaling, null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(original);
            }
        }

        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(2.75f)]
        [TestCase(3f)]
        public void WebGl_RenderDensityNormalizesToCssPixelsAndMatchesAndroidLayout(float density)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                foreach (
                    var cssSize in new[]
                    {
                        new Vector2(393, 839),
                        new Vector2(839, 393),
                        new Vector2(649, 720),
                        new Vector2(650, 720),
                    }
                )
                {
                    Vector2 pixels = cssSize * density;
                    Assert.IsTrue(
                        LobbyPanelScaling.TryApplyWebGlSettings(settings, pixels, cssSize),
                        "Valid canvas geometry must configure logical scaling at density " + density
                    );
                    Assert.AreEqual(
                        PanelScaleMode.ConstantPixelSize,
                        settings.scaleMode,
                        "WebGL must use explicit pixel-to-CSS scaling."
                    );
                    Assert.AreEqual(density, settings.scale, "The panel scale must equal render pixels per CSS pixel.");
                    Vector2 panelSize = pixels / settings.scale;
                    Assert.IsTrue(
                        UiViewportMetrics.TryCreate(
                            new Rect(Vector2.zero, panelSize),
                            Vector2.zero,
                            panelSize,
                            pixels,
                            new Rect(Vector2.zero, pixels),
                            cssSize,
                            false,
                            true,
                            out var web
                        ),
                        "WebGL geometry must produce a usable logical viewport."
                    );
                    Assert.IsTrue(
                        UiScreenLayout.TryCreate(cssSize, true, true, out var android),
                        "The density-normalized Android viewport must be valid."
                    );
                    Assert.AreEqual(
                        android.LogicalSize,
                        web.Layout.LogicalSize,
                        "Equivalent viewports must have the same logical dimensions."
                    );
                    Assert.AreEqual(
                        android.IsNarrow,
                        web.Layout.IsNarrow,
                        "The width breakpoint must be platform-independent."
                    );
                    Assert.AreEqual(
                        android.IsShort,
                        web.Layout.IsShort,
                        "The height breakpoint must be platform-independent."
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void WebGl_InvalidCanvasGeometryPreservesPreviousSettings(float invalidWidth)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scale = 2f;
                Assert.IsFalse(
                    LobbyPanelScaling.TryApplyWebGlSettings(
                        settings,
                        new Vector2(1080, 2400),
                        new Vector2(invalidWidth, 839)
                    ),
                    "Unavailable or invalid browser geometry must defer scaling."
                );
                Assert.AreEqual(2f, settings.scale, "Invalid geometry must preserve the last valid scale.");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
