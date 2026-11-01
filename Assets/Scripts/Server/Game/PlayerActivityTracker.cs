// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Game
{
    internal sealed class PlayerActivityTracker
    {
        private readonly ServerGameState m_game;
        private readonly IUserRegistry m_users;
        private readonly int m_inactiveAfterTurns;

        internal PlayerActivityTracker(ServerGameState game, IUserRegistry users, int inactiveAfterTurns)
        {
            if (inactiveAfterTurns <= 0)
                throw new ArgumentOutOfRangeException(nameof(inactiveAfterTurns));
            m_game = game;
            m_users = users;
            m_inactiveAfterTurns = inactiveAfterTurns;
        }

        internal CaptainAssignment? FindCaptainReplacement(FoobbuteoTeamState<ServerPlayerState> team)
        {
            if (m_game.MatchType == FoobbuteoGameType.MvM)
                return null;
            ServerPlayerState captain = team.Id > 0 ? team.GetPlayerByUserId(team.Id) : null;
            if (captain != null && !captain.IsInactive)
                return null;

            return SelectEligibleCaptain(team);
        }

        internal CaptainAssignment? FindCaptainAfterClaim(
            FoobbuteoTeamState<ServerPlayerState> team,
            ServerPlayerState claimedPlayer,
            ServerUser claimant
        )
        {
            if (team.Id <= 0)
                return new CaptainAssignment(
                    team.TeamIdx,
                    claimedPlayer.Slot.PlayerIdx,
                    claimant.UserId,
                    claimant.PlayerId,
                    claimant.Name
                );

            ServerPlayerState captain = team.GetPlayerByUserId(team.Id);
            // Unrelated claims leave an existing captain's replacement until turn closure.
            if (captain != null && (captain != claimedPlayer || !captain.IsInactive))
                return null;

            // Selection precedes the claim save, so consider the slot's incoming owner.
            return SelectEligibleCaptain(team, claimedPlayer, claimant);
        }

        private CaptainAssignment? SelectEligibleCaptain(
            FoobbuteoTeamState<ServerPlayerState> team,
            ServerPlayerState claimedPlayer = null,
            ServerUser claimant = null
        )
        {
            foreach (ServerPlayerState player in team.Players)
            {
                if (player == claimedPlayer && claimant != null)
                    return new CaptainAssignment(
                        team.TeamIdx,
                        player.Slot.PlayerIdx,
                        claimant.UserId,
                        claimant.PlayerId,
                        claimant.Name
                    );
                if (!player.IsUser || !player.IsConnected || player.IsInactive)
                    continue;
                string name = m_users.TryGetUser(player.UserId, out var user) ? user.Name : player.Name;
                return new CaptainAssignment(team.TeamIdx, player.Slot.PlayerIdx, player.UserId, player.PlayerId, name);
            }
            return null;
        }

        internal void ApplyCaptain(CaptainAssignment? assignment, Action<ServerPlayerState> changed)
        {
            if (!assignment.HasValue)
                return;
            CaptainAssignment captain = assignment.Value;
            var team = m_game.Team(captain.TeamIndex);
            var info = team.ToNetFoobbuteoTeam();
            info.Id = captain.UserId;
            info.Name = captain.Name;
            team.UpdateInfo(info);
            foreach (ServerPlayerState player in team.Players)
            {
                bool isCaptain = player.Slot.PlayerIdx == captain.PlayerIndex;
                if (player.IsCaptain == isCaptain)
                    continue;
                player.SetCaptain(isCaptain);
                changed(player);
            }
        }

        internal void CloseInput(Action<ServerPlayerState> changed)
        {
            CloseTeam(m_game.HomeTeam);
            CloseTeam(m_game.AwayTeam);

            void CloseTeam(ServerTeamState team)
            {
                foreach (ServerPlayerState player in team.Players)
                {
                    bool wasInactive = player.IsInactive;
                    player.CloseInput(m_game.CurrentTurn, m_inactiveAfterTurns);
                    if (wasInactive != player.IsInactive)
                        changed(player);
                }
            }
        }
    }
}
