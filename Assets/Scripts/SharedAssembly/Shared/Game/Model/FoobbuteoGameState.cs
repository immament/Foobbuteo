// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.NetState;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    public enum FoobbuteoGameType
    {
        MvM = 0,
        PvP = 1,
        TvT = 2,
    }

    public enum FoobbuteoGameStatus
    {
        Pending = 0,
        Ongoing = 1,
        Ended = 2,
        Disabled = 3,
        Creating = 4,
    }

    public enum ShirtColorEnum
    {
        Blue = 0,
        Red = 1,
        Green = 2,
        Orange = 3,
        Black = 4,
        White = 5,
        Brown = 6,
        Purple = 7,
    }

    [Serializable]
    public class FoobbuteoGameState<PS>
        where PS : FoobbuteoPlayerState
    {
        public FoobbuteoGameState(int gameId, FoobbuteoGameType gameType)
        {
            m_gameId = gameId;
            MatchType = gameType;
        }

        [SerializeField]
        private int m_gameId;
        public int GameId => m_gameId;

        public FoobbuteoGameType MatchType { get; }
        public FoobbuteoGameStatus Status { get; protected set; }
        public TurnStageEnum TurnStage { get; protected set; }
        public float StageEndTime { get; protected set; }

        [SerializeField]
        protected int m_currentTurn;
        public int CurrentTurn => m_currentTurn;

        public int NumberOfTurns { get; protected set; }

        public float StageStartTime { get; protected set; }

        public DateTimeOffset StartDateTime { get; protected set; }

        public string InfoTag => $"[G:{GameId} T:{CurrentTurn}]";

        [SerializeReference]
        protected FoobbuteoTeamState<PS> m_homeTeam;
        public virtual FoobbuteoTeamState<PS> HomeTeam => m_homeTeam;

        [SerializeReference]
        protected FoobbuteoTeamState<PS> m_awayTeam;

        public FoobbuteoTeamState<PS> AwayTeam => m_awayTeam;

        public FoobbuteoTeamState<PS> Team(TeamIndex idx) => idx == TeamIndex.Home ? HomeTeam : AwayTeam;

        public FoobbuteoTeamState<PS> GetOpponentTeam(TeamIndex idx) => idx == TeamIndex.Home ? AwayTeam : HomeTeam;

        public PS GetPlayer(PlayerKey key)
        {
            return Team(key.TeamIdx).GetPlayerAtIdx(key.PlayerIdx);
        }

        public PS GetPlayer(TeamIndex teamIdx, byte playerIdx)
        {
            return Team(teamIdx).GetPlayerAtIdx(playerIdx);
        }

        public PS GetPlayerByUserId(int userId)
        {
            if (userId <= 0)
                return null;

            return HomeTeam.GetPlayerByUserId(userId) ?? AwayTeam.GetPlayerByUserId(userId);
        }

        public bool TryGetUserSlot(int userId, out GameSlot slot)
        {
            slot = default;
            if (userId <= 0 || Status == FoobbuteoGameStatus.Ended)
                return false;
            if (MatchType == FoobbuteoGameType.MvM)
            {
                if (HomeTeam.Id == userId)
                    slot = GameSlot.ForManager(GameId, TeamIndex.Home);
                else if (AwayTeam.Id == userId)
                    slot = GameSlot.ForManager(GameId, TeamIndex.Away);
                return slot.IsManager;
            }
            var player = GetPlayerByUserId(userId);
            if (player == null)
                return false;
            slot = player.Slot;
            return true;
        }

        public bool IsWaitingForOpponent =>
            MatchType == FoobbuteoGameType.MvM
            && Status == FoobbuteoGameStatus.Pending
            && (HomeTeam.Id <= 0 || AwayTeam.Id <= 0);

        public bool CanStart =>
            MatchType != FoobbuteoGameType.MvM || HomeTeam.Id > 0 && AwayTeam.Id > 0 && HomeTeam.Id != AwayTeam.Id;

        public bool CanJoinAsManager(GameUser user, TeamKey team, out string reason)
        {
            if (MatchType != FoobbuteoGameType.MvM || Status != FoobbuteoGameStatus.Pending)
                reason = "Only a pending MvM game can be joined as manager.";
            else if (HomeTeam == null || AwayTeam == null || AwayTeam.Id > 0)
                reason = "The away side is not available.";
            else if (user == null || user.UserId <= 0 || user.InGameRoomId != GameId)
                reason = "Enter the game room before joining a team.";
            else if (user.GameSlot.HasValue || HomeTeam.Id == user.UserId)
                reason = "User already participates in an open game.";
            else if (!user.TryGetManagedTeam(team, out _))
                reason = "You do not manage the selected Footstar team.";
            else if (HomeTeam.TeamKey == team)
                reason = "Choose a different team from the home side.";
            else
            {
                reason = null;
                return true;
            }
            return false;
        }

        public PS GetPlayerForCommand(IPlayerCommand cmd, GameUser user, out string reason) =>
            GetPlayerForCommand(new PlayerKey(cmd.TeamIdx, cmd.PlayerIdx), user, out reason);

        public PS GetPlayerForCommand(PlayerKey playerKey, GameUser user, out string reason)
        {
            reason = null;
            if (!playerKey.IsValid(out reason))
                return null;
            if (TurnStage != TurnStageEnum.PlayerMove)
            {
                reason = $"Player can move only during 'Player Move' turn stage. Current stage: '{TurnStage}'";
                return null;
            }

            if (user == null)
            {
                reason = "User not found";
                return null;
            }

            if (user.InGameRoomId != GameId)
            {
                reason = $"User is not in room ${GameId}";
                return null;
            }

            if (!user.GameSlot.HasValue)
            {
                reason = "User is not control any player";
                return null;
            }

            if (user.GameSlot.Value.GameId != GameId)
            {
                reason = $"User control player in other game {user.GameSlot.Value.GameId}";
                return null;
            }

            if (user.GameSlot.Value.TeamIdx != playerKey.TeamIdx)
            {
                reason = $"Can not control player in opponent team ({playerKey.TeamIdx}/{playerKey.PlayerIdx})";
                return null;
            }

            var movePlayer = GetPlayer(playerKey);
            return MatchType == FoobbuteoGameType.MvM
                ? GetMvMPlayerForCommand(movePlayer, playerKey, user, out reason)
                : GetPvPPlayerForCommand(movePlayer, playerKey, user, out reason);
        }

        private PS GetMvMPlayerForCommand(PS movePlayer, PlayerKey playerKey, GameUser user, out string reason)
        {
            reason = null;
            if (!user.GameSlot.Value.IsManager || Team(playerKey.TeamIdx).Id != user.UserId)
            {
                reason = "Only the assigned manager can control this team.";
                return null;
            }
            return movePlayer;
        }

        private PS GetPvPPlayerForCommand(PS movePlayer, PlayerKey playerKey, GameUser user, out string reason)
        {
            reason = null;
            if (!user.GameSlot.Value.TryGetPlayerKey(out var userPlayerKey))
            {
                reason = "A player slot is required for this game type.";
                return null;
            }
            if (userPlayerKey.PlayerIdx != playerKey.PlayerIdx)
            {
                if (!movePlayer.UsesAutomaticControl)
                {
                    reason = $"Can not control players of other user {movePlayer.Slot}";
                    return null;
                }

                var userPlayer = GetPlayer(userPlayerKey);
                if (!userPlayer.IsCaptain)
                {
                    reason = $"Can not control player {movePlayer.Slot}. Only captain can control bots. ";
                    return null;
                }
            }

            return movePlayer;
        }

        public bool CanClaimPlayer(GameUser user, PlayerKey playerKey, out string reason) =>
            GetPlayerForClaim(user, playerKey, out reason) != null;

        private PS GetPlayerForClaim(GameUser user, PlayerKey playerKey, out string reason)
        {
            if (MatchType == FoobbuteoGameType.MvM)
            {
                reason = "Players cannot be claimed in MvM games.";
                return null;
            }
            return GetPvPPlayerForClaim(user, playerKey, out reason);
        }

        private PS GetPvPPlayerForClaim(GameUser user, PlayerKey playerKey, out string reason)
        {
            if (this.Status == FoobbuteoGameStatus.Ended)
            {
                reason = "Cannot claim the player after the game has ended";
                return null;
            }
            if (!playerKey.IsValid(out reason))
                return null;

            if (user == null)
            {
                reason = "User not found";
                return null;
            }

            if (user.UserId == GetOpponentTeam(playerKey.TeamIdx).Id)
            {
                reason =
                    $"You are the captain of a team; you cannot control players on the opposing team (@{user.UserId})";
                return null;
            }

            var player = GetPlayer(playerKey);
            return player.CanClaimPlayer(user, out reason) ? player : null;
        }

        public PS ClaimPlayer(GameUser user, PlayerKey playerKey)
        {
            var player = GetPlayerForClaim(user, playerKey, out string reason) ?? throw new AppException(reason);
            var team = Team(playerKey.TeamIdx);
            bool isCaptain = team.Id <= 0 || user.UserId == team.Id;
            player.ClaimPlayer(user, isCaptain);
            if (team.Id <= 0)
                team.AssignCaptain(user);
            return player;
        }
    }
}
