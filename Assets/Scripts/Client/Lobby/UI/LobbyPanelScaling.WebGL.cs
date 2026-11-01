// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_WEBGL && !UNITY_EDITOR
using Imma.Foobbuteo.Client.UI;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    public sealed partial class LobbyPanelScaling
    {
        private WebGlUiViewportObserver m_viewportObserver;

        partial void StartPlatformScaling() =>
            m_viewportObserver = new WebGlUiViewportObserver(
                (pixels, css) => RefreshWebGlSettingsIfChanged(pixels, css)
            );

        partial void StopPlatformScaling()
        {
            m_viewportObserver?.Dispose();
            m_viewportObserver = null;
        }
    }
}
#endif
