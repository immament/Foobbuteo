// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR || UNITY_ANDROID
using Imma.Foobbuteo.Client.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public sealed partial class GamePanelSettingsRuntime
    {
        private PanelSettings m_mainOriginal;
        private PanelSettings m_tooltipOriginal;
        private PanelSettings m_runtime;
        private AndroidUiDensityRefreshState m_density;

        private void OnEnable() => Acquire();

        internal void Acquire()
        {
            if (
                m_runtime != null
                || m_mainRenderer == null
                || m_tooltipRenderer == null
                || m_mainRenderer.panelSettings == null
            )
                return;
            m_mainOriginal = m_mainRenderer.panelSettings;
            m_tooltipOriginal = m_tooltipRenderer.panelSettings;
            m_runtime = Instantiate(m_mainOriginal);
            m_runtime.name = m_mainOriginal.name + " (Game Runtime)";
            m_runtime.hideFlags = HideFlags.DontSave;
            m_density = default;
            RefreshDensity();
            m_mainRenderer.panelSettings = m_runtime;
            m_tooltipRenderer.panelSettings = m_runtime;
            SubscribeToDensityChanges();
        }

        private void RefreshDensity() => m_density.RefreshIfChanged(m_runtime, m_mainOriginal);

        partial void SubscribeToDensityChanges();

        partial void UnsubscribeFromDensityChanges();

        private void OnDisable() => Release();

        internal void Release()
        {
            UnsubscribeFromDensityChanges();
            if (m_mainRenderer != null && m_mainRenderer.panelSettings == m_runtime)
                m_mainRenderer.panelSettings = m_mainOriginal;
            if (m_tooltipRenderer != null && m_tooltipRenderer.panelSettings == m_runtime)
                m_tooltipRenderer.panelSettings = m_tooltipOriginal;
            if (m_runtime != null)
            {
                if (Application.isPlaying)
                    Destroy(m_runtime);
                else
                    DestroyImmediate(m_runtime);
            }
            m_runtime = m_mainOriginal = m_tooltipOriginal = null;
            m_density = default;
        }
    }
}
#endif
