// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct PlayerModeCommand : IPlayerCommand, INetworkSerializable
    {
        private uint m_commandId;
        public uint CommandId => m_commandId;

        private int m_turn;
        public int Turn => m_turn;

        private int m_playerId;
        public int PlayerId => m_playerId;

        private byte m_playerIdx;
        public byte PlayerIdx => m_playerIdx;

        private TeamIndex m_teamIdx;
        public TeamIndex TeamIdx => m_teamIdx;

        private PlayerModeEnum m_mode;
        public PlayerModeEnum Mode => m_mode;

        public PlayerModeCommand(int turn, TeamIndex teamIdx, byte playerIdx, int playerId, PlayerModeEnum mode)
        {
            m_teamIdx = teamIdx;
            m_playerIdx = playerIdx;
            m_turn = turn;
            m_playerId = playerId;
            m_mode = mode;
            m_commandId = PlayerCommand.NextCmdId;
        }

        public override string ToString() => $"(PlayerCommand: {(new { Turn, TeamIdx, PlayerIdx, PlayerId, Mode })})";

        public string CreatorTag() => $"[{TeamIdx}/{PlayerIdx}]";

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_commandId);
            s.SerializeValue(ref m_turn);
            s.SerializeValue(ref m_teamIdx);
            s.SerializeValue(ref m_playerIdx);
            s.SerializeValue(ref m_playerId);
            s.SerializeValue(ref m_mode);
        }
    }

    public enum PlayerModeEnum
    {
        GkSave = 0,
        GkOutOfArea = 1,
        GkFieldPlayer = 2,
        Confrontation = 3,
        Interception = 4,
    }
}
