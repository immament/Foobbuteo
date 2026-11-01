// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal struct AndroidUiDensityRefreshState
    {
        private bool m_initialized;
        private RuntimePlatform m_platform;
        private bool m_isEditor;
        private float m_dpi;

        internal void RefreshIfChanged(PanelSettings settings, PanelSettings originalSettings) =>
            RefreshIfChanged(
                settings,
                originalSettings,
                UnityEngine.Device.Application.platform,
                Application.isEditor,
                UnityEngine.Device.Screen.dpi
            );

        internal bool RefreshIfChanged(
            PanelSettings settings,
            PanelSettings originalSettings,
            RuntimePlatform platform,
            bool isEditor,
            float dpi
        )
        {
            if (settings == null)
                return false;

            // Player density is handled by UI Toolkit. Only Android simulation needs explicit DPI scaling.
            float effectiveDpi = platform == RuntimePlatform.Android && isEditor ? dpi : AndroidUiDensity.ReferenceDpi;
            if (effectiveDpi <= 0f || float.IsNaN(effectiveDpi) || float.IsInfinity(effectiveDpi))
                effectiveDpi = AndroidUiDensity.ReferenceDpi;
            if (m_initialized && m_platform == platform && m_isEditor == isEditor && m_dpi.Equals(effectiveDpi))
                return false;

            AndroidUiDensity.RefreshSettings(settings, originalSettings, platform, isEditor, effectiveDpi);
            m_platform = platform;
            m_isEditor = isEditor;
            m_dpi = effectiveDpi;
            m_initialized = true;
            return true;
        }
    }
}
