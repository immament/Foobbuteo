// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Unity.Netcode;

namespace Imma.Foobbuteo.Server
{
    public partial class FoobbuteoMainServer : IServerLobbyNBCtrl
    {
        private LobbyNB m_lobbyNB;

        public async Task InitAsync(LobbyNB lobbyNB)
        {
            if (m_lobbyNB != null)
            {
                this.LError("InitServer already done. lobbyNB already set!");
                return;
            }
            m_userRegistry ??= ServiceLocator.Current.Get<IUserRegistry>();
            m_presence ??= new Users.UserPresenceService(m_userRegistry);
            m_serverState = new FoobbuteoServerState(m_userRegistry);
            m_serverState.GameListChanged += OnGameListChanged;
            m_serverState.UserChanged += NotifyAboutUserChange;

            ServiceLocator.Current.Register(this);
            this.LTrace(() => $"InitServer {lobbyNB != null}");
            m_lobbyNB = lobbyNB;
            m_lobbyNB.NetworkManager.OnClientDisconnectCallback += OnRoomClientDisconnected;

            await TryCheckForNewGamesAsync(m_serverLoopCancellation.Token);

            m_serverLoopTask = ServerLoopAsync(m_serverLoopCancellation.Token);
        }

        public bool AddClientToGame(ulong clientId, int gameId)
        {
            EnsureLobbyReady();
            if (m_userRegistry.TryGetUser(clientId, out var user))
            {
                return m_serverState.AddClientToGame(clientId, gameId, user);
            }
            return false;
        }

        public async Task<int> CreateNewGameAsync(ulong clientId, NewGameOptions gameOptions)
        {
            await m_createGameGate.WaitAsync(m_serverLoopCancellation.Token);
            try
            {
                EnsureLobbyReady();
                if (!m_userRegistry.TryGetUser(clientId, out var user))
                    throw new AppException($"Can't find user for client ^{clientId}");
                EnsureNoParticipation(user);
                // if (!user.IsAdmin && m_serverState.IsGameToPlayExists())
                //     throw new AppException("There are already game to play");

                if (!NewGameOptionsRules.TryValidate(gameOptions, out string error))
                    throw new AppException(error);
                if (gameOptions.GameType == FoobbuteoGameType.MvM && !user.TryGetManagedTeam(gameOptions.Team, out _))
                    throw new AppException("You do not manage the selected team.");
                if (gameOptions.GameType == FoobbuteoGameType.PvP && user.PlayerId <= 0)
                    throw new AppException("A valid player is required to create a game.");
                m_gamesRepo ??= ServiceLocator.Current.Get<IGamesRepository>();
                int gameId = await m_gamesRepo.CreateGameAsync(gameOptions, user);

                await CheckForNewGamesAsync(m_serverLoopCancellation.Token);
                if (!m_serverState.ContainsGame(gameId))
                    throw new AppException($"Created game #{gameId} could not be loaded.");

                AddClientToGame(clientId, gameId);
                return gameId;
            }
            finally
            {
                m_createGameGate.Release();
            }
        }

        public void DeleteGame(ulong clientId, int gameId)
        {
            EnsureLobbyReady();
            if (!m_userRegistry.TryGetUser(clientId, out var user))
                throw new AppException($"Can't find user for client ^{clientId}");

            if (!user.IsAdmin)
                throw new AppException($"User @{user.UserId} is not allowed to delete games");

            DeleteGame(gameId);
        }

        internal async Task JoinMvMGameAsync(ulong clientId, int gameId, TeamKey team)
        {
            await m_createGameGate.WaitAsync(m_serverLoopCancellation.Token);
            try
            {
                EnsureLobbyReady();
                var user = m_userRegistry.GetUser(clientId);
                var engine = m_serverState.GetGame(gameId);
                bool alreadyJoined =
                    engine.GameState.Status == FoobbuteoGameStatus.Pending
                    && engine.GameState.AwayTeam.Id == user.UserId
                    && engine.GameState.AwayTeam.TeamKey == team;
                if (!alreadyJoined)
                    EnsureNoParticipation(user);
                m_gamesRepo ??= ServiceLocator.Current.Get<IGamesRepository>();
                await m_serverState.GetGame(gameId).JoinMvMGameAsync(user, team, m_gamesRepo);
                AddClientToGame(clientId, gameId);
                NotifyAboutGameChange();
            }
            finally
            {
                m_createGameGate.Release();
            }
        }

        internal void DeleteGame(int gameId)
        {
            this.LDebug("+++");
            if (m_serverState.DeleteGame(gameId))
            {
                // OnGameListChanged();
            }
        }

        internal Task WaitForParticipationChangeAsync() => m_createGameGate.WaitAsync(m_serverLoopCancellation.Token);

        internal void ReleaseParticipationChange() => m_createGameGate.Release();

        internal void EnsureNoParticipation(GameUser user)
        {
            if (user.GameSlot.HasValue || m_serverState.HasParticipation(user.UserId))
                throw new AppException("User already participates in an open game.");
        }

        public GameUser GetUserInfo(ulong clientId)
        {
            return m_userRegistry.GetUser(clientId);
        }

        public void OnClientLobbyReady(ulong clientId) =>
            m_serverState.OnLobbyHandshake(clientId, m_gameLoadProgress.IsLobbyReady);

        private void OnRoomClientDisconnected(ulong clientId) => m_serverState.ForgetClient(clientId);

        public bool TryGetUserInfo(ulong clientId, out GameUser user, out string reason)
        {
            if (m_userRegistry.TryGetUser(clientId, out var serverUser, out reason))
            {
                user = serverUser;
                return true;
            }
            user = null;
            return false;
        }

        internal void NotifyAboutUserChange(ulong clientId, ServerUser user)
        {
            m_lobbyNB.ToClient_SendUserInfoRpc(user, m_lobbyNB.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        internal void NotifyAboutGameChange()
        {
            OnGameListChanged();
        }

        private void OnGameListChanged()
        {
            if (!m_gameLoadProgress.IsLobbyReady)
                return;
            this.LDebug("++");
            m_lobbyNB.ToClient_SendServerInfoRpc(GetServerInfoContract(), m_lobbyNB.RpcTarget.ClientsAndHost);
        }
    }
}
