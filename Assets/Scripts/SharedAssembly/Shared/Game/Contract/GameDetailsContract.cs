// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    [Serializable]
    public struct GameDetailsContract : INetworkSerializable
    {
        private int m_gameId;
        public int GameId => m_gameId;

        private Vector2 m_ballPosition;
        public Vector2 BallPosition => m_ballPosition;

        private GameDetails_TeamContract m_homeTeam;
        public GameDetails_TeamContract HomeTeam => m_homeTeam;

        private GameDetails_TeamContract m_awayTeam;
        public GameDetails_TeamContract AwayTeam => m_awayTeam;

        public GameDetailsContract(
            int gameId,
            Vector2 ballPosition,
            GameDetails_TeamContract homeTeam,
            GameDetails_TeamContract awayTeam
        )
        {
            m_gameId = gameId;
            m_ballPosition = ballPosition;
            m_homeTeam = homeTeam;
            m_awayTeam = awayTeam;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_gameId);
            s.SerializeValue(ref m_ballPosition);
            m_homeTeam.NetworkSerialize(s);
            m_awayTeam.NetworkSerialize(s);
        }
    }

    [Serializable]
    public struct GameDetails_TeamContract : INetworkSerializable
    {
        private int m_id;
        public int Id => m_id;
        private TeamKey m_team;
        public TeamKey Team => m_team;

        private byte m_score;
        public byte Score => m_score;

        private GameDetails_PlayerContract[] m_players;

        public GameDetails_PlayerContract[] Players => m_players;

        public GameDetails_TeamContract(
            int id,
            GameDetails_PlayerContract[] players,
            byte teamScore,
            TeamKey team = default
        )
        {
            m_id = id;
            m_players = players;
            m_score = teamScore;
            m_team = team;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref m_id);
            s.SerializeValue(ref m_players);
            s.SerializeValue(ref m_score);
            m_team.NetworkSerialize(s);

            // Players array
            // int length = m_players?.Length ?? 0;
            // s.SerializeValue(ref length);

            // if (s.IsReader)
            // {
            //     m_players = new GameDetails_PlayerContract[length];
            // }
            // for (int i = 0; i < length; i++)
            // {
            //     m_players[i].NetworkSerialize(s);
            // }
        }
    }

    [Serializable]
    public struct GameDetails_PlayerContract : INetworkSerializable
    {
        // public GameSlot Slot => new GameSlot(TeamIdx)
        public TeamIndex TeamIdx;
        public byte PlayerIdx;
        public int PlayerId;
        public int UserId;
        public bool IsUser => UserId > 0;

        [NonSerialized]
        public ulong? ClientId;

        public string Name;
        public int CountryId;
        public int PlayerLevel;
        public Vector2 Position;
        public bool HasBall;
        public int BallDistance;
        public int Zone;

        public bool IsCaptain;
        public bool IsInactive;
        public bool IsConnected;
        public int LastActivityTurn;
        public PlayerModeEnum PlayerMode;
        public float ActionPoints;

        // Skills
        public int MoveSkill;
        public int ConfrontationSkill;
        public int LowPassSkill;
        public int LongPassSkill;
        public int GkPassSkill;
        public int CrossesSkill;
        public int ShootingSkill;
        public int LongShotsSkill;
        public int GkSavesSkill;
        public int OutOfAreaSkill;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref TeamIdx);
            serializer.SerializeValue(ref PlayerIdx);
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref UserId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref CountryId);
            serializer.SerializeValue(ref PlayerLevel);

            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref HasBall);
            serializer.SerializeValue(ref BallDistance);
            serializer.SerializeValue(ref Zone);
            serializer.SerializeValue(ref IsCaptain);
            serializer.SerializeValue(ref IsInactive);
            serializer.SerializeValue(ref IsConnected);
            serializer.SerializeValue(ref LastActivityTurn);
            serializer.SerializeValue(ref PlayerMode);
            serializer.SerializeValue(ref ActionPoints);

            // Skills
            serializer.SerializeValue(ref MoveSkill);
            serializer.SerializeValue(ref ConfrontationSkill);
            serializer.SerializeValue(ref LowPassSkill);
            serializer.SerializeValue(ref LongPassSkill);
            serializer.SerializeValue(ref GkPassSkill);
            serializer.SerializeValue(ref CrossesSkill);
            serializer.SerializeValue(ref ShootingSkill);
            serializer.SerializeValue(ref LongShotsSkill);
            serializer.SerializeValue(ref GkSavesSkill);
            serializer.SerializeValue(ref OutOfAreaSkill);
        }
    }

    [Serializable]
    public struct MatchDetails_BallMoveContract : INetworkSerializable
    {
        public Vector2 Position;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
        }
    }
}
