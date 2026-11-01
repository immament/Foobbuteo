// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    [Serializable]
    public class PlayerMoveEvent : MatchEvent
    {
        private GameSlot m_slot;

        // private TeamIndex m_teamIdx;
        public TeamIndex TeamIdx => m_slot.TeamIdx;

        // private byte m_playerIdx;
        public byte PlayerIdx => m_slot.PlayerIdx;

        private int m_playerId;
        public int PlayerId => m_playerId;

        private Vector2 m_fromPosition;
        public Vector2 FromPosition => m_fromPosition;

        private Vector2 m_toPosition;
        public Vector2 ToPosition => m_toPosition;
        private bool m_withBall;
        public bool WithBall => m_withBall;

        public PlayerMoveEvent(MatchEventType eventType)
            : base(eventType, default, default) { }

        public PlayerMoveEvent(
            MatchEventType eventType,
            int turn,
            FoobbuteoPlayerState player,
            Vector2 toPosition,
            bool withBall = false,
            float? duration = null
        )
            : base(eventType, turn, duration ?? MoveTime(eventType, player.Position, toPosition))
        {
            // m_teamIdx = player.TeamIdx;
            // m_playerIdx = player.PlayerIdx;
            m_slot = player.Slot;
            m_playerId = player.PlayerId;
            m_fromPosition = player.Position;
            m_toPosition = toPosition;
            m_withBall = withBall;
        }

        public override void NetworkSerialize<T>(BufferSerializer<T> s)
        {
            base.NetworkSerialize(s);
            m_slot.NetworkSerialize(s);
            // s.SerializeValue(ref m_teamIdx);
            // s.SerializeValue(ref m_playerIdx);
            s.SerializeValue(ref m_playerId);
            s.SerializeValue(ref m_fromPosition);
            s.SerializeValue(ref m_toPosition);
            s.SerializeValue(ref m_withBall);
        }

        public override string ToString() =>
            $"([{this.EventType}] {m_slot}/{m_playerId}, {m_fromPosition} => {m_toPosition}, withBall: {m_withBall}, [{Duration}]s)";
    }
}
