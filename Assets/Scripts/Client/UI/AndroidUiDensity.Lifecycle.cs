// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR || UNITY_ANDROID
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    [RequireComponent(typeof(PanelRenderer))]
    public sealed partial class AndroidUiDensity
    {
        private PanelRenderer m_renderer;
        private PanelSettings m_originalSettings;
        private PanelSettings m_runtimeSettings;
        private AndroidUiDensityRefreshState m_refreshState;

        private void OnEnable()
        {
            m_renderer = GetComponent<PanelRenderer>();
            m_originalSettings = m_renderer.panelSettings;
            if (m_originalSettings == null)
                return;

            m_runtimeSettings = Instantiate(m_originalSettings);
            m_runtimeSettings.name = m_originalSettings.name + " (Android Density Runtime)";
            m_runtimeSettings.hideFlags = HideFlags.DontSave;
            m_refreshState = default;
            RefreshDensity();
            m_renderer.panelSettings = m_runtimeSettings;
            SubscribeToDensityChanges();
        }

        private void RefreshDensity() => m_refreshState.RefreshIfChanged(m_runtimeSettings, m_originalSettings);

        partial void SubscribeToDensityChanges();

        partial void UnsubscribeFromDensityChanges();

        private void OnDisable()
        {
            UnsubscribeFromDensityChanges();
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
            m_refreshState = default;
        }
    }
}
#endif
