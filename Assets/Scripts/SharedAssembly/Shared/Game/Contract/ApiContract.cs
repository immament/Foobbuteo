// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public readonly struct ProcessingStartedMessage
    {
        public int TurnNumber { get; }

        public ProcessingStartedMessage(int turnNumber)
        {
            TurnNumber = turnNumber;
        }
    }

    [Serializable]
    public struct TurnResultReadyMessage : INetworkSerializable
    {
        private int m_turnNumber;
        public int TurnNumber => m_turnNumber;

        private float m_startTime;
        public float StartTime => m_startTime;

        private float m_stageEndTime;
        public float StageEndTime => m_stageEndTime;

        private TurnResult m_result;
        public TurnResult Result => m_result;

        public TurnResultReadyMessage(int turnNumber, float startTime, float stageDuration, TurnResult result)
        {
            m_turnNumber = turnNumber;
            m_result = result;
            m_startTime = startTime;
            m_stageEndTime = startTime + stageDuration;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_turnNumber);
            s.SerializeValue(ref m_startTime);
            s.SerializeValue(ref m_stageEndTime);
            m_result.NetworkSerialize(s);
        }
    }

    public enum TurnResultTypeEnum : byte
    {
        None = 0,
        Goal = 1,
        Offside = 2,
        Intercept = 3,
        GoalKick = 4,
    }

    [Serializable]
    public struct TurnResult : INetworkSerializable
    {
        private TurnResultTypeEnum m_turnResultType;
        public TurnResultTypeEnum TurnResultType => m_turnResultType;

        private float m_playbackDuration;
        public float PlaybackDuration => m_playbackDuration;

        private byte m_homeScore;
        public byte HomeScore => m_homeScore;
        private byte m_awayScore;
        public byte AwayScore => m_awayScore;

        private PlayerKey m_playerWithBall;
        public PlayerKey PlayerWithBall => m_playerWithBall;

        private PlayerKey m_offsidePlayer;
        public PlayerKey OffsidePlayer => m_offsidePlayer;

        private MatchEvent[] m_events;
        public MatchEvent[] Events => m_events;

        private MatchCommentaryCue[] m_commentary;
        public MatchCommentaryCue[] Commentary => m_commentary;

        public TurnResult(
            TurnResultTypeEnum turnResultType,
            byte homeScore,
            byte awayScore,
            MatchEvent[] events,
            PlayerKey playerWithBall,
            PlayerKey offsidePlayer,
            float playbackDuration,
            MatchCommentaryCue[] commentary = null
        )
        {
            m_turnResultType = turnResultType;
            m_homeScore = homeScore;
            m_awayScore = awayScore;
            m_events = events;
            m_playerWithBall = playerWithBall;
            m_offsidePlayer = offsidePlayer;
            m_playbackDuration = playbackDuration;
            m_commentary = commentary ?? Array.Empty<MatchCommentaryCue>();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            m_playerWithBall.NetworkSerialize(s);
            m_offsidePlayer.NetworkSerialize(s);
            s.SerializeValue(ref m_playbackDuration);
            s.SerializeValue(ref m_turnResultType);
            s.SerializeValue(ref m_homeScore);
            s.SerializeValue(ref m_awayScore);

            SerializeEvents(s);
            int count = m_commentary?.Length ?? 0;
            s.SerializeValue(ref count);
            if (s.IsReader)
                m_commentary = new MatchCommentaryCue[count];
            for (int i = 0; i < count; i++)
                m_commentary[i].NetworkSerialize(s);
        }

        private void SerializeEvents<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            int length = m_events?.Length ?? 0;
            s.SerializeValue(ref length);
            if (s.IsReader)
                m_events = new MatchEvent[length];

            for (int i = 0; i < length; i++)
            {
                MatchEventType eventType = s.IsWriter ? m_events[i].EventType : default;

                s.SerializeValue(ref eventType);

                if (s.IsReader)
                {
                    m_events[i] = MatchEvent.Create(eventType);
                }

                m_events[i].NetworkSerialize(s);
            }
        }
    }
}
