// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_ANDROID && !UNITY_EDITOR
using Imma.Foobbuteo.Client.UI;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    public sealed partial class LobbyPanelScaling
    {
        // UI Toolkit owns native density changes after this initial configuration.
        partial void StartPlatformScaling() =>
            AndroidUiDensity.TryApplySettings(m_runtimeSettings, UnityEngine.RuntimePlatform.Android, false);
    }
}
#endif
