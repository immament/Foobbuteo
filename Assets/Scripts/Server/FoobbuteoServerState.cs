// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Model
{
    internal class FoobbuteoServerState : IDisposable
    {
        public FoobbuteoServerState(IUserRegistry userRegistry, List<FoobbuteoGameEngine> games = null)
        {
            m_userRegistry = userRegistry;
            m_games = games?.ToDictionary(d => d.GameId) ?? new();

            foreach (var game in m_games.Values)
                SubscribeGame(game);
        }

        private void SubscribeGame(FoobbuteoGameEngine game)
        {
            void OnGameStarted()
            {
                foreach (var user in m_userRegistry.GetUsers())
                {
                    if (!user.ClientId.HasValue || !m_initializedClients.Contains(user.ClientId.Value))
                        continue;
                    if (!game.GameState.TryGetUserSlot(user.UserId, out _))
                        continue;
                    OpenAssignedRoom(user.ClientId.Value);
                }
                GameListChanged?.Invoke();
            }

            void OnGameEnded()
            {
                ClearUsersPlayingGame(game.GameId);
                GameListChanged?.Invoke();
            }

            void OnPlayerClaimReplaced(ServerUser user) => ClearUserGameSlot(user, game.GameId);

            m_gameSubscriptions.Add(game, (OnGameStarted, OnGameEnded, OnPlayerClaimReplaced));
            game.GameStarted += OnGameStarted;
            game.GameEnded += OnGameEnded;
            game.PlayerClaimReplaced += OnPlayerClaimReplaced;
        }

        private readonly Dictionary<
            FoobbuteoGameEngine,
            (Action Started, Action Ended, Action<ServerUser> ClaimReplaced)
        > m_gameSubscriptions = new();
        private readonly HashSet<ulong> m_initializedClients = new();
        private readonly HashSet<ulong> m_pendingClients = new();

        private void UnsubscribeGame(FoobbuteoGameEngine game)
        {
            if (!m_gameSubscriptions.Remove(game, out var handlers))
                return;
            game.GameStarted -= handlers.Started;
            game.GameEnded -= handlers.Ended;
            game.PlayerClaimReplaced -= handlers.ClaimReplaced;
            game.StopConnectionTracking();
        }

        public void Dispose()
        {
            foreach (var game in m_gameSubscriptions.Keys.ToArray())
                UnsubscribeGame(game);
            m_initializedClients.Clear();
            m_pendingClients.Clear();
        }

        internal void ForgetClient(ulong clientId)
        {
            m_initializedClients.Remove(clientId);
            m_pendingClients.Remove(clientId);
        }

        internal void OnLobbyHandshake(ulong clientId, bool gamesReady)
        {
            if (!m_userRegistry.TryGetUser(clientId, out _) || m_initializedClients.Contains(clientId))
                return;
            if (!gamesReady)
            {
                m_pendingClients.Add(clientId);
                return;
            }
            m_pendingClients.Remove(clientId);
            m_initializedClients.Add(clientId);
            OpenAssignedRoom(clientId, reopen: true);
        }

        internal void CompletePendingHandshakes()
        {
            foreach (ulong clientId in m_pendingClients.ToArray())
            {
                m_pendingClients.Remove(clientId);
                OnLobbyHandshake(clientId, gamesReady: true);
            }
        }

        private void OpenAssignedRoom(ulong clientId, bool reopen = false)
        {
            if (!m_userRegistry.TryGetUser(clientId, out var user))
                return;
            var assignedGames = m_games
                .Values.Where(game =>
                    game.GameState.Status != FoobbuteoGameStatus.Ended
                    && game.GameState.TryGetUserSlot(user.UserId, out _)
                )
                .OrderByDescending(game =>
                    user.GameSlot.HasValue
                    && game.GameState.TryGetUserSlot(user.UserId, out var slot)
                    && user.GameSlot.Value == slot
                )
                .ThenByDescending(game => game.GameState.Status == FoobbuteoGameStatus.Ongoing)
                .ThenBy(game => game.GameState.StartDateTime)
                .ThenBy(game => game.GameId)
                .ToArray();
            if (assignedGames.Length > 1)
                this.LWarn(
                    $"User @{user.UserId} is assigned to multiple active games; opening #{assignedGames[0].GameId}."
                );
            if (assignedGames.Length > 0)
                EnterGameRoom(clientId, assignedGames[0], user, forced: true, reopen: reopen);
        }

        private readonly IUserRegistry m_userRegistry;

        private readonly Dictionary<int, FoobbuteoGameEngine> m_games;

        public event Action GameListChanged;
        public event Action<ulong, ServerUser> UserChanged;

        // private readonly Dictionary<ulong, FoobbuteoGameEngine> m_clientToGame = new();

        // public IReadOnlyCollection<FoobbuteoGameState> Games => m_games.Values.ToArray();

        public GameInfoContract[] ToGameListContract()
        {
            this.LTrace(m_games.Count);
            return m_games.Values.Select(g => g.GameState.ToGameInfoContract()).ToArray();
        }

        internal GameDetailsContract GameDetailsContract(int gameId)
        {
            return m_games[gameId].ToGameDetailsContract();
        }

        internal void AddGame(FoobbuteoGameEngine game, bool notify)
        {
            SubscribeGame(game);
            m_games.Add(game.GameId, game);
            if (game.GameState.Status == FoobbuteoGameStatus.Ended)
                ClearUsersPlayingGame(game.GameId);
            if (notify)
                GameListChanged?.Invoke();
        }

        internal bool ContainsGame(int gameId) => m_games.ContainsKey(gameId);

        internal FoobbuteoGameEngine GetGame(int gameId) =>
            m_games.TryGetValue(gameId, out var game) ? game : throw new AppException($"Game #{gameId} not found.");

        internal bool HasParticipation(int userId) =>
            m_games.Values.Any(game => game.GameState.TryGetUserSlot(userId, out _));

        private bool TryGetGame(int gameId, out FoobbuteoGameEngine newGameEngine)
        {
            return m_games.TryGetValue(gameId, out newGameEngine);
        }

        internal List<FoobbuteoGameEngine> GetEnded(int deleteGameAfterEndMinutes)
        {
            var gamesToRemove = m_games
                .Values.Where(game => game.GameState.EndDate < DateTime.UtcNow.AddMinutes(-deleteGameAfterEndMinutes))
                .ToList();
            return gamesToRemove;

            // foreach (var game in gamesToRemove)
            // {
            //     // TODO remove room NB & engine
            //     game.GameRoomNB.Close();
            //     m_games.Remove(game.GameId);
            // }
            // return gamesToRemove.Count;
        }

        internal bool IsGameToPlayExists()
        {
            return m_games.Values.Any(game => game.GameState.Status != FoobbuteoGameStatus.Ended);
        }

        internal bool AddClientToGame(ulong clientId, int gameId, ServerUser user)
        {
            if (!TryGetGame(gameId, out var game))
                throw new AppException($"Game #{gameId} not found.");
            return EnterGameRoom(clientId, game, user, forced: false, reopen: false);
        }

        private bool EnterGameRoom(ulong clientId, FoobbuteoGameEngine game, ServerUser user, bool forced, bool reopen)
        {
            int oldRoomId = user.InGameRoomId;
            var oldSlot = user.GameSlot.NullableValue;
            TryGetGame(oldRoomId, out var oldGame);
            if (!forced && oldRoomId != game.GameId && oldGame != null)
                EnsureCanLeave(user, oldGame);

            UpdateUserGameSlot(user, game);
            user.EnterRoom(game.GameId);
            if (oldRoomId != game.GameId || oldSlot != user.GameSlot.NullableValue)
                UserChanged?.Invoke(clientId, user);

            if (oldRoomId != game.GameId && oldGame != null)
                oldGame.ServerGameRoomNB.CloseGameRoom(clientId);
            if (oldRoomId != game.GameId || reopen)
                game.ServerGameRoomNB.AddClientToGameRoom(clientId);
            return true;
        }

        private static void EnsureCanLeave(ServerUser user, FoobbuteoGameEngine game)
        {
            if (!GameRoomLeavePolicy.CanLeave(user, game.GameId, game.GameState.Status))
                throw new AppException(GameRoomLeavePolicy.LockedMessage);
        }

        private void UpdateUserGameSlot(ServerUser user, FoobbuteoGameEngine game)
        {
            if (game.GameState.Status == FoobbuteoGameStatus.Ended)
            {
                if (user.IsPlayGame(game.GameId))
                    user.StopPlayGame(game.GameId);
                return;
            }

            if (
                !game.GameState.TryGetUserSlot(user.UserId, out var slot)
                || (user.GameSlot.HasValue && user.GameSlot.Value == slot)
            )
                return;

            user.RestoreGameSlot(slot);
        }

        internal bool RemoveClientFromGameRoom(ulong clientId, int gameId)
        {
            // this.LDebug($"Game: ${gameId}, Client: ^{clientId}");
            if (!m_userRegistry.TryGetUser(clientId, out var user))
            {
                this.LWarn($"Client ^{clientId} not found. Can't leave game: ${gameId}!");
                return false;
            }
            if (user.InGameRoomId <= 0)
            {
                this.LWarn($"Client ^{clientId} is without room). Can't leave game: ${gameId}!");
                return false;
            }
            if (user.InGameRoomId != gameId)
            {
                this.LWarn($"Client ^{clientId} in other room ${user.InGameRoomId}. Can't leave game: ${gameId}!");
                return false;
            }
            if (TryGetGame(gameId, out var game))
                EnsureCanLeave(user, game);
            bool result = user.LeaveRoom(gameId);
            if (result)
                UserChanged?.Invoke(clientId, user);
            this.LDebug(() => $"Client ^{clientId} leave room ${gameId}.");
            return result;
        }

        internal bool DeleteGame(int gameId)
        {
            if (m_games.Remove(gameId, out var game))
            {
                UnsubscribeGame(game);
                RemoveAllClientsFromGame(game);
                game.ServerGameRoomNB.Close();
                GameListChanged?.Invoke();
                return true;
            }
            return false;
        }

        private void RemoveAllClientsFromGame(FoobbuteoGameEngine game)
        {
            var usersToRemove = m_userRegistry.GetUsersInRoom(game.GameId);

            foreach (var user in usersToRemove)
            {
                bool changed = user.IsPlayGame(game.GameId);
                if (changed)
                    user.StopPlayGame(game.GameId);
                if (user.InGameRoomId == game.GameId)
                {
                    user.LeaveRoom(game.GameId);
                    changed = true;
                    if (user.ClientId.HasValue)
                    {
                        game.ServerGameRoomNB.CloseGameRoom(user.ClientId.Value);
                    }
                }
                if (changed && user.ClientId.HasValue)
                    UserChanged?.Invoke(user.ClientId.Value, user);
            }
        }

        private void ClearUsersPlayingGame(int gameId)
        {
            foreach (var user in m_userRegistry.GetUsersInRoom(gameId))
                ClearUserGameSlot(user, gameId);
        }

        private void ClearUserGameSlot(ServerUser user, int gameId)
        {
            if (!user.IsPlayGame(gameId))
                return;

            this.LTrace(() => $"@{user.UserId} stops playing game #{gameId}");
            user.StopPlayGame(gameId);
            if (user.ClientId.HasValue)
                UserChanged?.Invoke(user.ClientId.Value, user);
        }

#if UNITY_EDITOR
        internal void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;
            foreach (var game in m_games.Values)
            {
                if (game.GameState.Status == FoobbuteoGameStatus.Ongoing)
                {
                    game.GameState.HomeTeam?.OnDrawGizmos();
                    game.GameState.AwayTeam?.OnDrawGizmos();
                }
            }
        }
#endif
    }
}
