// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Client
{
    public class UiScreenLayoutTests
    {
        [TestCase(393, 759, true, false)]
        [TestCase(393, 873, true, false)]
        [TestCase(873, 393, false, true)]
        [TestCase(839, 279, false, true)]
        [TestCase(649, 520, true, false)]
        [TestCase(650, 519, false, true)]
        [TestCase(650, 520, false, false)]
        [TestCase(1120, 522, false, false)]
        public void Classification_UsesIndependentLogicalWidthAndHeight(
            float width,
            float height,
            bool narrow,
            bool shortHeight
        )
        {
            Assert.IsTrue(
                UiScreenLayout.TryCreate(new Vector2(width, height), true, true, out var layout),
                "Finite positive logical dimensions must produce a layout."
            );
            Assert.AreEqual(narrow, layout.IsNarrow, "Narrow layout must follow logical width only.");
            Assert.AreEqual(shortHeight, layout.IsShort, "Short layout must follow logical height only.");
            Assert.AreEqual(narrow || shortHeight, layout.IsSmall, "Either constrained axis must select small layout.");
            Assert.AreEqual(width < height, layout.IsPortrait, "Orientation must follow dimensions.");
            Assert.AreEqual(width >= height, layout.IsLandscape, "Square viewports use horizontal orientation.");
            Assert.IsTrue(layout.IsMobilePlatform, "Mobile identity must remain separate from available space.");
            Assert.IsTrue(layout.SupportsTouch, "Touch support must remain separate from available space.");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Classification_RejectsInvalidDimensions(float invalid)
        {
            Assert.IsFalse(
                UiScreenLayout.TryCreate(new Vector2(invalid, 759), false, false, out _),
                "Invalid width must wait for settled geometry."
            );
            Assert.IsFalse(
                UiScreenLayout.TryCreate(new Vector2(393, invalid), false, false, out _),
                "Invalid height must wait for settled geometry."
            );
        }

        [TestCase(1f)]
        [TestCase(2.75f)]
        [TestCase(3.5f)]
        public void Measurement_RenderResolutionDoesNotChangeLogicalSize(float renderScale)
        {
            var logicalSize = new Vector2(393, 759);
            Vector2 renderSize = logicalSize * renderScale;
            var renderRect = new Rect(Vector2.zero, renderSize);
            Assert.IsTrue(
                UiViewportMetrics.TryCreate(
                    renderRect,
                    Vector2.zero,
                    renderSize,
                    renderSize,
                    renderRect,
                    logicalSize,
                    true,
                    true,
                    out var metrics
                ),
                "Canvas CSS dimensions must normalize a valid screen-rendered panel."
            );
            Assert.That(
                metrics.Layout.LogicalSize.x,
                Is.EqualTo(logicalSize.x).Within(0.01f),
                "Higher render density must preserve the logical width."
            );
            Assert.That(
                metrics.Layout.LogicalSize.y,
                Is.EqualTo(logicalSize.y).Within(0.01f),
                "Higher render density must preserve the logical height."
            );
            Assert.That(
                metrics.PanelUnitsPerLogicalUnit.x,
                Is.EqualTo(renderScale).Within(0.01f),
                "Local UI scaling must compensate for rendering density."
            );
            Assert.IsTrue(metrics.Layout.IsSmall, "A high-resolution phone must still use the small layout.");
        }

        [TestCase(1080, 2400, 1080, 2306, true)]
        [TestCase(2400, 1080, 2306, 1080, false)]
        public void Measurement_AndroidScaledPanelUsesSafeLogicalSizeWithoutDoubleScaling(
            float pixelWidth,
            float pixelHeight,
            float safePixelWidth,
            float safePixelHeight,
            bool portrait
        )
        {
            const float deviceDpi = 440f;
            float pixelsPerPanelUnit = deviceDpi / AndroidUiDensity.ReferenceDpi;
            var screenPixels = new Vector2(pixelWidth, pixelHeight);
            Vector2 panelSize = screenPixels / pixelsPerPanelUnit;
            Assert.IsTrue(
                UiViewportMetrics.TryCreate(
                    new Rect(Vector2.zero, panelSize),
                    Vector2.zero,
                    panelSize,
                    screenPixels,
                    new Rect(0, 0, safePixelWidth, safePixelHeight),
                    panelSize,
                    true,
                    true,
                    out var metrics
                ),
                "The recorded Android geometry must produce a safe logical viewport after panel scaling."
            );
            Assert.That(
                metrics.Layout.LogicalSize.x,
                Is.EqualTo(safePixelWidth / pixelsPerPanelUnit).Within(0.01f),
                "Safe width must exclude the device inset and apply density exactly once."
            );
            Assert.That(
                metrics.Layout.LogicalSize.y,
                Is.EqualTo(safePixelHeight / pixelsPerPanelUnit).Within(0.01f),
                "Safe height must exclude the device inset and apply density exactly once."
            );
            Assert.That(
                metrics.SafeAreaPanelUnits.y,
                Is.EqualTo((pixelHeight - safePixelHeight) / pixelsPerPanelUnit).Within(0.01f),
                "The portrait top inset must be converted from screen pixels to panel units."
            );
            Assert.AreEqual(
                Vector2.one,
                metrics.PanelUnitsPerLogicalUnit,
                "The lineup overlay must not scale again when the Android panel already normalizes density."
            );
            Assert.IsTrue(metrics.Layout.IsSmall, "Both phone orientations must select small-screen lineup defaults.");
            Assert.AreEqual(portrait, metrics.Layout.IsPortrait, "Rotation must follow the safe logical viewport.");
            var mainLayout = new UnityEngine.UIElements.VisualElement();
            Vector2 safeSize = metrics.Layout.LogicalSize;
            ResponsiveLayout.ApplyLayout(mainLayout, safeSize.x, safeSize.y, metrics.Layout.AspectRatio);
            Assert.AreEqual(
                portrait,
                mainLayout.ClassListContains("stacked-layout"),
                "The match must stack in portrait and retain its horizontal layout in landscape."
            );
            Assert.AreEqual(
                !portrait,
                mainLayout.ClassListContains("short-landscape"),
                "Density-normalized landscape height must enable the existing short-screen rules."
            );
        }

        [Test]
        public void Measurement_ClipsSafeAreaToRootAndConvertsBottomOriginToTopOrigin()
        {
            var panelSize = new Vector2(1000, 800);
            var rootContent = new Rect(0, 0, 600, 500);
            Assert.IsTrue(
                UiViewportMetrics.TryCreate(
                    rootContent,
                    new Vector2(100, 50),
                    panelSize,
                    panelSize,
                    new Rect(120, 20, 850, 700),
                    panelSize,
                    false,
                    false,
                    out var metrics
                ),
                "An inset root must intersect the screen safe area."
            );
            Assert.AreEqual(
                new Rect(20, 30, 580, 470),
                metrics.SafeAreaPanelUnits,
                "Safe-area coordinates must be clipped and expressed relative to the root's top-left."
            );
            Assert.IsFalse(metrics.Layout.IsMobilePlatform, "A small desktop window must retain desktop identity.");
            Assert.IsTrue(metrics.Layout.IsSmall, "Small available space must not depend on mobile identity.");
        }
    }
}
