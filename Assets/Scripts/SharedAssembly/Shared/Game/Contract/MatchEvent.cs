// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public enum MatchEventType
    {
        PlayerMove = 1,
        LowPass = 2,
        HighPass = 6,
        Shot = 3,
        BallMove = 10,
        FixPosition = 11,
    }

    [Serializable]
    public abstract class MatchEvent : INetworkSerializable
    {
        // sqr distance per second
        private const float k_PassSpeedRatioSqr = 220f * 220f;
        private const float k_ShotSpeedRatioSqr = 250f * 250f;
        private const float k_PlayerSpeedRatioSqr = 200f * 200f;

        private MatchEventType m_eventType;
        public MatchEventType EventType => m_eventType;

        private int m_turn;
        public int Turn => m_turn;

        private float m_duration;
        public float Duration => m_duration;

        protected MatchEvent(MatchEventType eventType, int turn, float duration)
        {
            m_eventType = eventType;
            m_turn = turn;
            m_duration = duration;
        }

        public static MatchEvent Create(MatchEventType eventType)
        {
            return eventType switch
            {
                MatchEventType.PlayerMove or MatchEventType.FixPosition => new PlayerMoveEvent(eventType),
                MatchEventType.BallMove or MatchEventType.LowPass or MatchEventType.HighPass or MatchEventType.Shot =>
                    new BallMoveEvent(eventType),
                _ => throw new Exception($"Unknown MatchEventType: [{eventType}]"),
            };
        }

        public virtual void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_eventType);
            s.SerializeValue(ref m_turn);
            s.SerializeValue(ref m_duration);
        }

        protected static float MoveTime(MatchEventType type, Vector2 start, Vector2 end)
        {
            switch (type)
            {
                case MatchEventType.PlayerMove:
                    ImmLogger.LDebug<MatchEvent>(() =>
                        $"{end}=>{start}, {(end - start).sqrMagnitude} ratioSqr: {k_PlayerSpeedRatioSqr}, time: {(end - start).sqrMagnitude / k_PlayerSpeedRatioSqr}"
                    );
                    return (end - start).sqrMagnitude / k_PlayerSpeedRatioSqr;
                case MatchEventType.LowPass:
                case MatchEventType.HighPass:
                case MatchEventType.BallMove:
                    return (end - start).sqrMagnitude / k_PassSpeedRatioSqr;
                case MatchEventType.Shot:
                    return (end - start).sqrMagnitude / k_ShotSpeedRatioSqr;
                default:
                    return 0f;
            }
        }

        public override string ToString() => $"([{m_eventType}] turn: {m_turn}, duration: {m_duration})";
    }
}
