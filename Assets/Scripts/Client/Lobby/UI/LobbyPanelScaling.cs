// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed partial class LobbyPanelScaling : BaseMonoBehaviour
    {
        private PanelRenderer m_renderer;
        private PanelSettings m_originalSettings;
        private PanelSettings m_runtimeSettings;
        private Vector2 m_lastScreenPixels;
        private Vector2 m_lastCssSize;
        private bool m_hasWebGlSize;

        private new void OnEnable()
        {
            base.OnEnable();
            m_renderer = GetComponent<PanelRenderer>();
            m_originalSettings = m_renderer.panelSettings;
            if (m_originalSettings == null)
                return;

            m_runtimeSettings = Instantiate(m_originalSettings);
            m_runtimeSettings.name = m_originalSettings.name + " (Lobby Runtime)";
            m_runtimeSettings.hideFlags = HideFlags.DontSave;
            StartPlatformScaling();
            m_renderer.panelSettings = m_runtimeSettings;
        }

        partial void StartPlatformScaling();

        partial void StopPlatformScaling();

        internal bool RefreshWebGlSettingsIfChanged(Vector2 screenPixels, Vector2 cssSize)
        {
            if (m_hasWebGlSize && m_lastScreenPixels.Equals(screenPixels) && m_lastCssSize.Equals(cssSize))
                return false;
            if (!TryApplyWebGlSettings(m_runtimeSettings, screenPixels, cssSize))
                return false;

            m_lastScreenPixels = screenPixels;
            m_lastCssSize = cssSize;
            m_hasWebGlSize = true;
            return true;
        }

        internal static bool TryApplyWebGlSettings(PanelSettings settings, Vector2 screenPixels, Vector2 cssSize)
        {
            if (settings == null || !UiScreenLayout.IsValidSize(screenPixels) || !UiScreenLayout.IsValidSize(cssSize))
                return false;

            // Screen-space panels use uniform scaling; the canvas and render target share an aspect ratio.
            float scale = screenPixels.x / cssSize.x;
            if (settings.scaleMode != PanelScaleMode.ConstantPixelSize)
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            if (!Mathf.Approximately(settings.scale, scale))
                settings.scale = scale;
            return true;
        }

        private new void OnDisable()
        {
            base.OnDisable();
            StopPlatformScaling();
            if (m_renderer != null && m_renderer.panelSettings == m_runtimeSettings)
                m_renderer.panelSettings = m_originalSettings;
            if (m_runtimeSettings != null)
            {
                if (Application.isPlaying)
                    Destroy(m_runtimeSettings);
                else
                    DestroyImmediate(m_runtimeSettings);
            }
            m_runtimeSettings = null;
            m_originalSettings = null;
            m_hasWebGlSize = false;
        }
    }
}
