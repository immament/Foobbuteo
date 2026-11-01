// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct GameInfoContract : INetworkSerializable
    {
        public int GameId;
        public FoobbuteoGameStatus Status;
        public int Turn;
        public int NumberOfTurns;
        public bool HasResult;
        public byte OwnerScore;
        public byte OpponentScore;
        private long m_startDateTimeTicks;
        public long StartDateTimeTicks => m_startDateTimeTicks;
        public DateTimeOffset StartDateTime
        {
            get { return DateTimeOffset.FromUnixTimeSeconds(m_startDateTimeTicks); }
            set { m_startDateTimeTicks = value.ToUnixTimeSeconds(); }
        }

        // private long m_currentDateTimeTicks;
        // public long CurrentDateTimeTicks => m_currentDateTimeTicks;
        // public DateTimeOffset CurrentDateTime
        // {
        //     get { return DateTimeOffset.FromUnixTimeSeconds(m_currentDateTimeTicks); }
        //     set { m_currentDateTimeTicks = value.ToUnixTimeSeconds(); }
        // }
        public long OwnerUid;
        public long OpponentUid;
        public string OwnerName;
        public string OpponentName;
        public FoobbuteoGameType MatchType;
        public TeamKey HomeTeam;
        public TeamKey AwayTeam;

        // public int OwnerCountryId;
        // public int OpponentCountryId;
        // public int OwnerRanking;
        // public int OpponentRanking;
        // public DateTime? EndDateTime;

        // public int Postponed;
        public ShirtColorEnum OwnerColor;
        public ShirtColorEnum OpponentColor;

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref GameId);
            s.SerializeValue(ref Status);
            s.SerializeValue(ref Turn);
            s.SerializeValue(ref NumberOfTurns);
            s.SerializeValue(ref HasResult);
            s.SerializeValue(ref OwnerScore);
            s.SerializeValue(ref OpponentScore);
            s.SerializeValue(ref m_startDateTimeTicks);
            // s.SerializeValue(ref m_currentDateTimeTicks);
            s.SerializeValue(ref OwnerUid);
            s.SerializeValue(ref OpponentUid);
            s.SerializeValue(ref OwnerName);
            s.SerializeValue(ref OpponentName);
            s.SerializeValue(ref MatchType);
            HomeTeam.NetworkSerialize(s);
            AwayTeam.NetworkSerialize(s);
            // s.SerializeValue(ref OwnerCountryId);
            // s.SerializeValue(ref OpponentCountryId);
            // s.SerializeValue(ref OwnerRanking);
            // s.SerializeValue(ref OpponentRanking);
            // s.SerializeValue(ref Postponed);
            s.SerializeValue(ref OwnerColor);
            s.SerializeValue(ref OpponentColor);
            // s.SerializeNullableDateTime(ref EndDateTime);
        }
    }
}
