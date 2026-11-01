// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Users;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Lobby.Contract
{
    public struct ServerInfoContract : INetworkSerializable
    {
        private GameInfoContract[] m_games;
        public GameInfoContract[] Games => m_games;
        private ClientRuntimeSettings m_settings;
        public ClientRuntimeSettings Settings => m_settings;

        public ServerInfoContract(GameInfoContract[] games)
            : this(games, new ClientRuntimeSettings(ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds)) { }

        public ServerInfoContract(GameInfoContract[] games, ClientRuntimeSettings settings)
        {
            this.m_games = games;
            m_settings = settings;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            m_settings.NetworkSerialize(s);
            int length = m_games?.Length ?? 0;
            s.SerializeValue(ref length);
            if (s.IsReader)
            {
                m_games = new GameInfoContract[length];
            }
            for (int i = 0; i < length; i++)
            {
                m_games[i].NetworkSerialize(s);
            }
        }
    }
}
