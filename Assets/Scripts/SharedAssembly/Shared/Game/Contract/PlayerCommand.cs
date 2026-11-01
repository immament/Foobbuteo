// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public interface IPlayerCommand
    {
        uint CommandId { get; }
        int Turn { get; }
        int PlayerId { get; }
        byte PlayerIdx { get; }
        TeamIndex TeamIdx { get; }
    }

    public struct PlayerCommand : INetworkSerializable, IPlayerCommand
    {
        private static uint s_nextCommandId = 0;

        public static uint NextCmdId => ++s_nextCommandId;

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

        private PlayerActionEnum m_action;
        public PlayerActionEnum Action => m_action;

        private Vector2 m_targetPosition;
        public Vector2 TargetPosition => m_targetPosition;

        public PlayerCommand WithPlayerId(int playerId)
        {
            PlayerCommand command = this;
            command.m_playerId = playerId;
            return command;
        }

        public PlayerCommand(
            int turn,
            TeamIndex teamIdx,
            byte playerIdx,
            int playerId,
            PlayerActionEnum action,
            Vector2 targetPosition
        )
        {
            m_teamIdx = teamIdx;
            m_playerIdx = playerIdx;
            m_turn = turn;
            m_playerId = playerId;
            m_action = action;
            m_targetPosition = targetPosition;
            m_commandId = NextCmdId;
        }

        public override string ToString() =>
            $"(PlayerCommand: {(new { Turn, TeamIdx, PlayerIdx, PlayerId, Action, TargetPosition, CommandId })})";

        public string CreatorTag() => $"[{TeamIdx}/{PlayerIdx}]";

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_commandId);
            serializer.SerializeValue(ref m_turn);
            serializer.SerializeValue(ref m_teamIdx);
            serializer.SerializeValue(ref m_playerIdx);
            serializer.SerializeValue(ref m_playerId);
            serializer.SerializeValue(ref m_action);
            serializer.SerializeValue(ref m_targetPosition);
        }
    }

    public enum PlayerActionEnum
    {
        Move = 1,
        LowPass = 2,
        Shot = 3,
        HighPass = 6,
    }
}
