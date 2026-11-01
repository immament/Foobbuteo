// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Globalization;
using System.Text;
using Imma.Common;
using Imma.Foobbuteo.Common;
using UnityEngine;
using UnityEngine.UIElements;
using Application = UnityEngine.Device.Application;
using Screen = UnityEngine.Device.Screen;

namespace Imma.Foobbuteo.Client.UI
{
    [DisallowMultipleComponent]
    // Server builds strip PanelRenderer while processing the scene.
#if !UNITY_SERVER
    [RequireComponent(typeof(PanelRenderer))]
#endif
    public sealed class UiLayoutDiagnostics : BaseMonoBehaviour
    {
        private PanelRenderer m_panelRenderer;
        private VisualElement m_root;
        private IVisualElementScheduledItem m_pendingLog;
        private string m_pendingReason;
        private int m_uiVersion;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string GetUiLayoutBrowserMetrics(float panelWidth, float panelHeight, int screenMapped);

        private static string ReadBrowserMetrics(Vector2 panelSize, bool screenMapped)
        {
            try
            {
                return GetUiLayoutBrowserMetrics(panelSize.x, panelSize.y, screenMapped ? 1 : 0)
                    ?? "unavailable (empty bridge response)";
            }
            catch (System.Exception exception)
            {
                return "unavailable (WebGL bridge failed: " + exception.GetType().Name + ")";
            }
        }
#endif

        private new void OnEnable()
        {
            m_panelRenderer = GetComponent<PanelRenderer>();
            m_panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private new void OnDisable()
        {
            if (m_panelRenderer != null)
                m_panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnbindRoot();
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            UnbindRoot();
            m_root = rootElement;
            m_uiVersion = version;
            if (m_root == null)
                return;
            m_root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RequestSnapshot("ui-reload");
        }

        private void UnbindRoot()
        {
            m_pendingLog?.Pause();
            m_pendingLog = null;
            m_root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_root = null;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => RequestSnapshot("root-geometry");

        [ContextMenu("Log UI Layout Snapshot")]
        public void LogNow() => RequestSnapshot("manual");

        private void RequestSnapshot(string reason)
        {
            if (!isActiveAndEnabled)
                return;
            m_pendingReason = reason;
            if (m_pendingLog != null)
                return;
            if (m_root == null)
            {
                WriteSnapshot();
                return;
            }
            m_pendingLog = m_root.schedule.Execute(WriteSnapshot);
        }

        private void WriteSnapshot()
        {
            m_pendingLog = null;
            if (!isActiveAndEnabled)
                return;

            var text = new StringBuilder();
            text.AppendLine(
                $"[UiLayoutDiagnostics] scene={gameObject.scene.name}, object={gameObject.name}, "
                    + $"entity={GetEntityId()}, reason={m_pendingReason}, uiVersion={m_uiVersion}"
            );
            text.AppendLine(
                $"unity: screenPixels={Screen.width}x{Screen.height}, dpi={Positive(Screen.dpi)}, "
                    + $"safeAreaPixels={RectText(Screen.safeArea)}, orientation={Screen.orientation}, "
                    + $"fullscreen={Screen.fullScreen}, fullscreenMode={Screen.fullScreenMode}, "
                    + $"platform={Application.platform}, mobilePlatform={Application.isMobilePlatform}, "
                    + $"touchSupported={Input.touchSupported}"
            );

            var settings = m_panelRenderer != null ? m_panelRenderer.panelSettings : null;
            AppendPanelSettings(text, settings);
            var panelTree = m_root?.panel?.visualTree;
            AppendElement(text, "panelTree", panelTree);
            AppendElement(text, "root", m_root?.panel != null ? m_root : null);

            Vector2 panelSize = panelTree != null ? panelTree.layout.size : Vector2.zero;
            bool screenMapped =
                settings != null
                && settings.renderMode == PanelRenderMode.ScreenSpaceOverlay
                && settings.targetTexture == null
                && settings.targetDisplay == 0
                && ValidSize(panelSize);
            text.AppendLine(
                $"ratios: rootContentAspect={RootAspect()}, "
                    + $"unityScreenPixelsPerPanelUnitX={Ratio(Screen.width, panelSize.x, screenMapped)}, "
                    + $"unityScreenPixelsPerPanelUnitY={Ratio(Screen.height, panelSize.y, screenMapped)}"
            );
#if UNITY_WEBGL && !UNITY_EDITOR
            text.Append("browser: ");
            text.Append(ReadBrowserMetrics(panelSize, screenMapped));
#endif
            this.LInfo(text.ToString());
        }

        private static void AppendPanelSettings(StringBuilder text, PanelSettings settings)
        {
            if (settings == null)
            {
                text.AppendLine("panelSettings: unavailable");
                return;
            }
            var texture = settings.targetTexture;
            string targetTexture = texture != null ? $"{texture.width}x{texture.height}" : "none";
            text.AppendLine(
                $"panelSettings: name={settings.name}, renderMode={settings.renderMode}, "
                    + $"scaleMode={settings.scaleMode}, scale={Number(settings.scale)}, "
                    + $"referenceResolution={settings.referenceResolution.x}x{settings.referenceResolution.y}, "
                    + $"screenMatchMode={settings.screenMatchMode}, match={Number(settings.match)}, "
                    + $"referenceDpi={Positive(settings.referenceDpi)}, fallbackDpi={Positive(settings.fallbackDpi)}, "
                    + $"targetDisplay={settings.targetDisplay}, targetTexturePixels={targetTexture}"
            );
        }

        private static void AppendElement(StringBuilder text, string label, VisualElement element)
        {
            if (element == null)
            {
                text.AppendLine(label + ": unavailable (root is not attached to a panel)");
                return;
            }
            text.AppendLine(
                $"{label}: name={element.name}, layoutPanelUnits={RectText(element.layout)}, "
                    + $"contentPanelUnits={RectText(element.contentRect)}, "
                    + $"worldBoundPanelUnits={RectText(element.worldBound)}, "
                    + $"resolvedSizePanelUnits=({Number(element.resolvedStyle.width)}, "
                    + $"{Number(element.resolvedStyle.height)}), "
                    + $"fontSizePanelUnits={Positive(element.resolvedStyle.fontSize)}, "
                    + $"classes=[{string.Join(",", element.GetClasses())}]"
            );
        }

        private string RootAspect()
        {
            if (m_root?.panel == null)
                return "unavailable";
            var size = m_root.contentRect.size;
            return Ratio(size.x, size.y, true);
        }

        private static bool ValidSize(Vector2 size) => IsPositiveFinite(size.x) && IsPositiveFinite(size.y);

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

        private static string Ratio(float numerator, float denominator, bool mappingAvailable) =>
            mappingAvailable && IsPositiveFinite(numerator) && IsPositiveFinite(denominator)
                ? Number(numerator / denominator)
                : "unavailable";

        private static string Positive(float value) => IsPositiveFinite(value) ? Number(value) : "unavailable";

        private static string Number(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? "unavailable"
                : value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string RectText(Rect rect) =>
            $"(x={Number(rect.x)}, y={Number(rect.y)}, width={Number(rect.width)}, height={Number(rect.height)})";
    }
}
