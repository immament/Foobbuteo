// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
using Imma.Foobbuteo.Client.UI;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    public sealed partial class LobbyPanelScaling
    {
        private AndroidUiDensityRefreshState m_densityRefreshState;

        partial void StartPlatformScaling()
        {
            m_densityRefreshState = default;
            RefreshDensity();
            UiDensityEditorEvents.Changed += RefreshDensity;
        }

        private void RefreshDensity() => m_densityRefreshState.RefreshIfChanged(m_runtimeSettings, m_originalSettings);

        partial void StopPlatformScaling()
        {
            UiDensityEditorEvents.Changed -= RefreshDensity;
            m_densityRefreshState = default;
        }
    }
}
#endif
