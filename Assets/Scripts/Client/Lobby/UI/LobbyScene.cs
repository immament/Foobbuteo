// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    public class LobbyScene : BaseMonoBehaviour
    {
        [SerializeField]
        private PanelRenderer m_panelRenderer;
        private LobbyUiController m_lobbyUiController;
        private ILobbyApiClient m_lobbyApi;

        new void Awake()
        {
            base.Awake();
            Bootstrap();
        }

        private void Bootstrap()
        {
            m_lobbyApi = ServiceLocator.Current.GetOrDefault<ILobbyApiClient>();
            if (m_lobbyApi == null)
            {
                this.LogDebug(() => $"Create new NetLobbyApiClient");
                m_lobbyApi = new NetLobbyApiClient(ServiceLocator.Current.Get<NetworkObjectRegistry>(), AppCtx.Client);
                ServiceLocator.Current.Register(m_lobbyApi);
            }
            else
            {
                this.LogDebug(() => $"Reuse NetLobbyApiClient");
            }

            m_lobbyUiController = ServiceLocator.Current.GetOrDefault<LobbyUiController>();
            if (m_lobbyUiController == null)
            {
                this.LogDebug(() => $"Create new LobbyUiController");
                m_lobbyUiController = new LobbyUiController(m_panelRenderer, m_lobbyApi);
                ServiceLocator.Current.Register<LobbyUiController>(m_lobbyUiController);
            }
            else
            {
                this.LogDebug(() => $"Reuse LobbyUiController");
                m_lobbyUiController.Init(m_panelRenderer, m_lobbyApi);
            }
        }

        new void OnDestroy()
        {
            m_lobbyUiController?.DetachPanel(m_panelRenderer);
            this.LogInfo("-----");
        }
    }
}
