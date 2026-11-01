// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;
using UnityEngine.UIElements;
using Application = UnityEngine.Device.Application;
using Screen = UnityEngine.Device.Screen;

namespace Imma.Foobbuteo.Client.UI
{
    internal readonly struct UiViewportMetrics
    {
        internal UiScreenLayout Layout { get; }
        internal Rect SafeAreaPanelUnits { get; }
        internal Vector2 PanelUnitsPerLogicalUnit { get; }

        private UiViewportMetrics(UiScreenLayout layout, Rect safeArea, Vector2 panelUnitsPerLogicalUnit)
        {
            Layout = layout;
            SafeAreaPanelUnits = safeArea;
            PanelUnitsPerLogicalUnit = panelUnitsPerLogicalUnit;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int GetUiCanvasCssSize(out float width, out float height);
#endif

        internal static bool TryReadCanvasCssSize(out Vector2 size)
        {
            size = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GetUiCanvasCssSize(out float width, out float height) == 0)
                return false;
            size = new Vector2(width, height);
#endif
            return UiScreenLayout.IsValidSize(size);
        }

        internal static bool TryRead(VisualElement root, out UiViewportMetrics metrics)
        {
            metrics = default;
            if (root?.panel == null)
                return false;
            Vector2 panelSize = root.panel.visualTree.layout.size;
            Vector2 logicalCanvasSize = panelSize;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!TryReadCanvasCssSize(out logicalCanvasSize))
                return false;
#endif
            // Native panels already account for density through PanelSettings; only WebGL needs CSS normalization.
            return TryCreate(
                root.contentRect,
                root.worldBound.position,
                panelSize,
                new Vector2(Screen.width, Screen.height),
                Screen.safeArea,
                logicalCanvasSize,
                Application.isMobilePlatform,
                Application.isMobilePlatform || Input.touchSupported,
                out metrics
            );
        }

        internal static bool TryCreate(
            Rect rootContent,
            Vector2 rootPanelPosition,
            Vector2 panelSize,
            Vector2 screenPixels,
            Rect safeAreaPixels,
            Vector2 logicalCanvasSize,
            bool isMobilePlatform,
            bool supportsTouch,
            out UiViewportMetrics metrics
        )
        {
            metrics = default;
            if (
                !UiScreenLayout.IsValidSize(rootContent.size)
                || !UiScreenLayout.IsValidSize(panelSize)
                || !UiScreenLayout.IsValidSize(screenPixels)
                || !UiScreenLayout.IsValidSize(logicalCanvasSize)
            )
                return false;

            Vector2 panelPerPixel = panelSize / screenPixels;
            var safePanel = new Rect(
                safeAreaPixels.x * panelPerPixel.x - rootPanelPosition.x,
                (screenPixels.y - safeAreaPixels.yMax) * panelPerPixel.y - rootPanelPosition.y,
                safeAreaPixels.width * panelPerPixel.x,
                safeAreaPixels.height * panelPerPixel.y
            );
            var available = Rect.MinMaxRect(
                Mathf.Max(rootContent.xMin, safePanel.xMin),
                Mathf.Max(rootContent.yMin, safePanel.yMin),
                Mathf.Min(rootContent.xMax, safePanel.xMax),
                Mathf.Min(rootContent.yMax, safePanel.yMax)
            );
            Vector2 unitsPerLogicalUnit = panelSize / logicalCanvasSize;
            if (
                !UiScreenLayout.TryCreate(
                    available.size / unitsPerLogicalUnit,
                    isMobilePlatform,
                    supportsTouch,
                    out var layout
                )
            )
                return false;
            metrics = new UiViewportMetrics(layout, available, unitsPerLogicalUnit);
            return true;
        }
    }
}
