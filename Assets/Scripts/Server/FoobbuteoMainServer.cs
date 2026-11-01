// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Server.Users;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Server
{
    public partial class FoobbuteoMainServer : IDisposable
    {
        public FoobbuteoMainServer()
        {
            m_serverLoopCancellation = new CancellationTokenSource();
        }

        internal FoobbuteoMainServer(IUserRegistry users, ClientRuntimeSettings settings)
            : this()
        {
            m_userRegistry = users;
            m_presence = new UserPresenceService(users);
            m_clientSettings = settings;
        }

        private UserPresenceService m_presence;
        private ClientRuntimeSettings m_clientSettings = new(
            ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds
        );

        public UserPresenceRow[] GetUsers(ulong clientId, int roomId, Func<ulong, bool> isConnected) =>
            m_presence.GetUsers(clientId, roomId, isConnected);

        public void AuthorizeUserDisconnect(
            ulong requesterClientId,
            int targetUserId,
            ulong targetClientId,
            ulong hostClientId,
            Func<ulong, bool> isConnected
        ) => m_presence.AuthorizeDisconnect(requesterClientId, targetUserId, targetClientId, hostClientId, isConnected);

        // ++++++++++++++++++++++++

        private IUserRegistry m_userRegistry;
        private readonly double m_gameLoopDelay = 60;
        private const string k_LobbyLoadingMessage = "Server games are still loading.";

        private FoobbuteoServerState m_serverState;
        private readonly GameLoadProgress m_gameLoadProgress = new();
        private Task m_serverLoopTask;
        private IGamesRepository m_gamesRepo;
        private readonly SemaphoreSlim m_gameLoadGate = new(1, 1);
        private readonly SemaphoreSlim m_createGameGate = new(1, 1);

        private CancellationTokenSource m_serverLoopCancellation;

        // private readonly Dictionary<ulong, FoobbuteoGameEngine> m_clientToGame = new();

        // +++++++++++++++++++++++++++

        public ServerInfoContract GetServerInfoContract()
        {
            EnsureLobbyReady();
            return new(m_serverState.ToGameListContract(), m_clientSettings);
        }

        private void EnsureLobbyReady()
        {
            if (!m_gameLoadProgress.IsLobbyReady)
                throw new AppException(k_LobbyLoadingMessage);
        }

        internal GameDetailsContract GameDetailsContract(int gameId)
        {
            return m_serverState.GameDetailsContract(gameId);
        }

        private const int k_deleteGameAfterEndMinutes = 10;

        // Clear old games, and add new game if there are any pending
        private async Task ServerLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(m_gameLoopDelay), cancellationToken);
                    var gamesToDelete = m_serverState.GetEnded(k_deleteGameAfterEndMinutes);
                    if (gamesToDelete.Count > 0)
                    {
                        foreach (var game in gamesToDelete)
                        {
                            DeleteGame(game.GameId);
                        }
                        OnGameListChanged();
                    }

                    await TryCheckForNewGamesAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal server shutdown.
            }
            catch (Exception exception)
            {
                this.LError($"Server loop failed: {exception.Message}");
                this.LException(exception);
            }
        }

        private bool InitGames(List<ServerGameState> games, out bool allGamesInitialized)
        {
            bool hasNewGame = false;
            allGamesInitialized = true;
            foreach (var gameState in games)
            {
                try
                {
                    hasNewGame |= (InitGame(gameState, notify: false) > 0);
                }
                catch (Exception ex)
                {
                    allGamesInitialized = false;
                    this.LExceptionTree(ex, "Init game #{gameState?.GameId} problem");
                }
            }
            return hasNewGame;
        }

        private int InitGame(ServerGameState game, bool notify)
        {
            if (m_serverState.ContainsGame(game.GameId))
            {
                this.LWarn($"Game #{game.GameId} already initialized");
                return 0;
            }

            this.LInfo($"Init Game #{game.GameId}");
            game.RefreshState();
            GameObject gameObj = UnityEngine.Object.Instantiate(m_lobbyNB.GamePrefab);
            gameObj.name = $"GameRoom_{game.GameId}";
            GameRoomNB gameRoomNB = gameObj.GetComponent<GameRoomNB>();
            // gameRoomNB.InitServer();
            var gameEngine = new FoobbuteoGameEngine(this, gameRoomNB, game);
            m_serverState.AddGame(gameEngine, notify);
            gameObj.GetComponent<NetworkObject>().Spawn();
            return gameEngine.GameId;
        }

        // private bool IsHostClient(ulong clientId) => (NetworkManager.ServerClientId == clientId);

        internal bool RemoveClientFromGame(ulong clientId, IServerGameRoomNB gameRoomNB)
        {
            this.LDebug(() => $"++ {gameRoomNB == null}");
            return m_serverState.RemoveClientFromGameRoom(clientId, gameRoomNB.GameId);
        }

        public void Dispose()
        {
            this.LTrace("--");
            if (m_lobbyNB != null && m_lobbyNB.NetworkManager != null)
                m_lobbyNB.NetworkManager.OnClientDisconnectCallback -= OnRoomClientDisconnected;
            if (m_serverState != null)
            {
                m_serverState.GameListChanged -= OnGameListChanged;
                m_serverState.UserChanged -= NotifyAboutUserChange;
                m_serverState.Dispose();
            }
            m_serverLoopCancellation?.Cancel();
            m_serverLoopCancellation?.Dispose();
            m_serverLoopCancellation = null;
            m_serverLoopTask = null;
            ServiceLocator.Current.Unregister<FoobbuteoMainServer>();
        }

        private async Task TryCheckForNewGamesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await CheckForNewGamesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RepositoryUnavailableException exception)
            {
                this.LWarn(exception.Message);
            }
            catch (Exception exception)
            {
                this.LExceptionTree(exception, "Checking for database games failed");
            }
        }

        private async Task CheckForNewGamesAsync(CancellationToken cancellationToken)
        {
            await m_gameLoadGate.WaitAsync(cancellationToken);
            try
            {
                m_gamesRepo ??= ServiceLocator.Current.Get<IGamesRepository>();
                this.LDebug(() => $"Checking for games after #{m_gameLoadProgress.LastGameId}");
                List<ServerGameState> games = await m_gamesRepo.GetGameListAsync(minId: m_gameLoadProgress.LastGameId);
                cancellationToken.ThrowIfCancellationRequested();

                bool hasNewGame = InitGames(games, out bool allGamesInitialized);
                bool becameReady = m_gameLoadProgress.RecordSuccessfulQuery(
                    games.ConvertAll(game => game.GameId),
                    allGamesInitialized
                );
                if (hasNewGame || becameReady)
                    OnGameListChanged();
                m_serverState.CompletePendingHandshakes();
            }
            finally
            {
                m_gameLoadGate.Release();
            }
        }

#if UNITY_EDITOR
        internal void OnDrawGizmos()
        {
            m_serverState?.OnDrawGizmos();
        }
#endif
    }

    internal sealed class GameLoadProgress
    {
        internal bool IsLobbyReady { get; private set; }
        internal int LastGameId { get; private set; }

        internal bool RecordSuccessfulQuery(IEnumerable<int> gameIds, bool allGamesInitialized)
        {
            bool becameReady = !IsLobbyReady;
            IsLobbyReady = true;

            if (allGamesInitialized)
            {
                foreach (int gameId in gameIds)
                    LastGameId = Math.Max(LastGameId, gameId);
            }

            return becameReady;
        }
    }
}
