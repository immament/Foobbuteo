// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public class BallMoveEvent : MatchEvent
    {
        private Vector2 m_fromPosition;
        public Vector2 FromPosition => m_fromPosition;

        private Vector2 m_toPosition;
        public Vector2 ToPosition => m_toPosition;

        public BallMoveEvent(MatchEventType eventType)
            : base(eventType, default, default) { }

        public BallMoveEvent(
            MatchEventType eventType,
            int turn,
            Vector2 fromPosition,
            Vector2 toPosition,
            float? duration = null
        )
            : base(eventType, turn, duration.HasValue ? duration.Value : MoveTime(eventType, fromPosition, toPosition))
        {
            m_fromPosition = fromPosition;
            m_toPosition = toPosition;
        }

        public override void NetworkSerialize<T>(BufferSerializer<T> s)
        {
            base.NetworkSerialize(s);
            s.SerializeValue(ref m_fromPosition);
            s.SerializeValue(ref m_toPosition);
        }

        public override string ToString() => $"([{this.EventType}]  {m_fromPosition} => {m_toPosition}, [{Duration}]s)";
    }
}
