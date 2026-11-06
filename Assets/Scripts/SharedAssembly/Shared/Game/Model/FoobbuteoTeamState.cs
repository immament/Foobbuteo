// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    [Serializable]
    public class FoobbuteoTeamState<PS>
        where PS : FoobbuteoPlayerState
    {
        public int Id { get; private set; }
        public TeamIndex TeamIdx { get; private set; }
        public string Name { get; private set; }
        public ShirtColorEnum Color { get; private set; }
        public TeamKey TeamKey { get; private set; }

        [SerializeField]
        private byte m_score;
        public byte Score
        {
            get => m_score;
            private set => m_score = value;
        }

        [SerializeReference]
        private PS[] m_players;
        public IReadOnlyList<PS> Players => m_players;

        public FoobbuteoTeamState(
            TeamIndex teamIdx,
            int id,
            string name,
            ShirtColorEnum color,
            PS[] players,
            byte score = 0,
            TeamKey teamKey = default
        )
        {
            TeamIdx = teamIdx;
            Id = id;
            Name = name;
            Color = color;
            Score = score;
            m_players = players;
            TeamKey = teamKey;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="playerIdx">Player index from 1-11</param>
        /// <returns></returns>
        public PS GetPlayerAtIdx(byte playerIdx)
        {
            return m_players[playerIdx - 1];
        }

        public PS GetPlayerById(int playerId)
        {
            foreach (var p in m_players)
            {
                if (p.PlayerId == playerId)
                    return p;
            }
            return null;
        }

        public PS GetPlayerByUserId(int userId)
        {
            foreach (var p in m_players)
            {
                if (p.UserId == userId)
                    return p;
            }
            return null;
        }

        internal void AssignCaptain(GameUser user)
        {
            Id = user.UserId;
            Name = user.Name;
        }

        public void UpdateInfo(NetFoobbuteoTeam teamInfo)
        {
            Id = teamInfo.Id;
            Name = teamInfo.Name;
            Color = teamInfo.Color;
            TeamKey = teamInfo.Team;
        }

        public PS GetGk()
        {
            return m_players[0];
        }

        public GameDetails_TeamContract ToContract()
        {
            return new(Id, m_players.Select(p => p.ToContract()).ToArray(), this.Score, TeamKey);
        }

        public NetFoobbuteoTeam ToNetFoobbuteoTeam()
        {
            return new NetFoobbuteoTeam()
            {
                Id = Id,
                Name = Name,
                Color = Color,
                Team = TeamKey,
            };
        }

        public void StartTurn()
        {
            foreach (var player in m_players)
            {
                player.StartTurn();
            }
        }

        public void RefreshState(Vector2 ballPosition)
        {
            foreach (var p in m_players)
            {
                p.RefreshState(ballPosition);
            }
        }

        public PS GetPlayerWithBall()
        {
            foreach (var p in m_players)
            {
                if (p.HasBall)
                    return p;
            }
            return null;
        }

        const float k_passInterceptSqrTreshold = 16f * 16f;

        // returns player intercept ball or null
        public (PS player, Vector2 interceptPoint) CheckPassIntercept(Vector2 begin, Vector2 end)
        {
            this.LDebug(() => $"{begin} => {end}");

            Vector2 BE = end - begin;
            float beSqrMagnitude = BE.sqrMagnitude;

            // without GK
            for (int i = 1; i < m_players.Length; i++)
            {
                var player = m_players[i];
                if (player.PlayerMode != PlayerModeEnum.Interception)
                    continue;

                Vector2? interceptPoint = CheckIntercept(player, begin, BE, beSqrMagnitude, k_passInterceptSqrTreshold);
                if (interceptPoint.HasValue)
                    return (player, interceptPoint.Value);
            }

            return (null, Vector2.zero);
        }

        // check if GK intercept shot
        public (PS player, Vector2 interceptPoint) CheckShotIntercept(Vector2 begin, Vector2 end)
        {
            this.LDebug(() => $"{begin} => {end}");

            var gk = GetGk();
            float defenseDistance = 12f + (gk.GkInterceptSkill * 12f) / 20f;
            this.LDebug(
                new
                {
                    gk.GkInterceptSkill,
                    defenseDistance,
                    gk.Zone,
                    gk.PlayerMode,
                }
            );

            Vector2 BE = end - begin;
            float beSqrMagnitude = BE.sqrMagnitude;

            Vector2? interceptPoint = CheckIntercept(gk, begin, BE, beSqrMagnitude, defenseDistance * defenseDistance);

            if (interceptPoint.HasValue)
                return (gk, interceptPoint.Value);
            return (null, Vector2.zero);
        }

        private Vector2? CheckIntercept(
            PS player,
            Vector2 begin,
            Vector2 BE,
            float beSqrMagnitude,
            float interceptDistanceSqr
        )
        {
            Vector2 pos = player.Position;
            Vector2 BP = pos - begin;
            float t = Vector2.Dot(BE, BP) / beSqrMagnitude;
            // ignore if projection outside BE (ball move begin->end)
            if (t < 0f || t > 1f)
            {
                this.LTrace(() => $"{player.Tag()} t: {t}, player: {pos}");
                return null;
            }
            var closestPoint = begin + t * BE;
            this.LTrace(() =>
                $"{player.Tag()} player: {pos}, closestPoint: {closestPoint}, closest dist: {(pos - closestPoint).magnitude}"
            );
            if ((pos - closestPoint).sqrMagnitude <= interceptDistanceSqr)
            {
                this.LDebug($"Intercept {player.Tag()}");
                return closestPoint;
            }

            return null;
        }

        public void Goal()
        {
            this.m_score++;
        }

        public List<PS> GetPlayersInZone(int zone, Vector2 ballPosition)
        {
            var result = new List<PS>();
            foreach (var p in m_players)
            {
                if (p.Zone == zone)
                {
                    p.RefreshState(ballPosition);
                    result.Add(p);
                }
            }
            return result;
        }

        public void LoadPlayer(PS player)
        {
            m_players[player.Slot.PlayerIdx - 1] = player;
        }
    }

    // public class TeamManager
    // {
    //     public int Id;
    //     public string Name;
    // }
}
