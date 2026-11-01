// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Shared
{
    // public struct GameSlot : INetworkSerializable
    // {
    //     private int m_gameId;
    //     public int GameId => m_gameId;

    //     private TeamIndex m_teamIdx;
    //     public TeamIndex TeamIdx => m_teamIdx;

    //     private byte m_playerIdx;
    //     public byte PlayerIdx => m_playerIdx;

    //     public GameSlot(int gameId, TeamIndex teamIdx, byte playerIdx)
    //     {
    //         m_gameId = gameId;
    //         m_teamIdx = teamIdx;
    //         m_playerIdx = playerIdx;
    //     }

    //     public void NetworkSerialize<T>(BufferSerializer<T> serializer)
    //         where T : IReaderWriter
    //     {
    //         serializer.SerializeValue(ref m_gameId);
    //         serializer.SerializeValue(ref m_teamIdx);
    //         serializer.SerializeValue(ref m_playerIdx);
    //     }
    // }
}
