// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;

namespace Imma.Foobbuteo.Client.Game
{
    public class ClientCmdManager
    {
        private readonly IClientGameRoomNB m_apiClient;
        private readonly Func<bool> m_canSend;

        public ClientCmdManager(IClientGameRoomNB apiClient, Func<bool> canSend)
        {
            m_apiClient = apiClient;
            m_canSend = canSend;
        }

        public void Submit(PlayerCommand cmd)
        {
            if (!m_canSend() || !m_apiClient.IsSpawned)
                return;
            this.LDebug(cmd);
            m_apiClient.SendMoveCmd(cmd);
        }

        internal void ChangeMode(PlayerModeCommand cmd)
        {
            if (!m_canSend() || !m_apiClient.IsSpawned)
                return;
            m_apiClient.SendModeCmd(cmd);
        }
    }
}
