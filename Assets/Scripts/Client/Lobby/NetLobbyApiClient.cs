// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;

namespace Imma.Foobbuteo.Client.Lobby
{
    public class NetLobbyApiClient : ILobbyApiClient, IUserPresenceApi
    {
        public bool IsReady => m_client.IsClientConnected && m_lobbyNB != null && m_lobbyNB.IsSpawned && m_hasSettings;
        private readonly IClientAppCtrl m_client;
        private readonly NetworkObjectRegistry m_registry;
        public ClientRuntimeSettings Settings { get; private set; }
        private bool m_hasSettings;
        private bool m_initialServerInfoRequested;
        private ulong m_requestId;
        private bool m_roomRestoreChecked;
        private int m_restoringRoom;
        public event Action SessionChanged;
        public event Action<UserListResponse> UserListReceived;
        public event Action<UserDisconnectResult> UserDisconnectCompleted;

        public ulong NextRequestId() => ++m_requestId;

        public void RequestUsers(ulong requestId, int roomId)
        {
            if (IsReady)
                m_lobbyNB.ToServer_RequestUserListRpc(requestId, roomId);
        }

        public void DisconnectUser(ulong requestId, int userId, ulong clientId)
        {
            if (IsReady)
                m_lobbyNB.ToServer_DisconnectUserRpc(requestId, userId, clientId);
        }

        public void JoinRoom(int roomId)
        {
            if (IsReady)
                SendGameJoinRequest(roomId);
        }

        private void HandleUserList(UserListResponse response) => UserListReceived?.Invoke(response);

        private void HandleDisconnectResult(UserDisconnectResult result) => UserDisconnectCompleted?.Invoke(result);

        #region ++ ILobbyApiClient ++++++
        public event Action<ServerInfoContract> ServerInfoUpdated;
        public event Action<string> AlertReceived;

        public void RequestServerInfo()
        {
            this.LDebug("++");
            WithLobbyNB(
                (lobbyNB) =>
                {
                    lobbyNB.ToServer_RequestServerInfoRpc();
                    this.LDebug("Done");
                }
            );
        }

        public void SendGameJoinRequest(int gameId)
        {
            if (!IsReady)
                return;
            this.LInfo($"++ {gameId}");
            m_lobbyNB.ToServer_SendGameJoinRpc(gameId);
        }

        public void DeleteGame(int gameId)
        {
            if (!IsReady)
                return;
            m_lobbyNB.ToServer_DeleteGameRpc(gameId);
        }

        public event Action<NewGameResult> NewGameCompleted;

        public void NewGame(NewGameOptions options)
        {
            if (!IsReady)
            {
                NewGameCompleted?.Invoke(new NewGameResult(false, error: "The lobby is disconnected."));
                return;
            }
            m_lobbyNB.ToServer_NewGameRpc(options);
        }

        #endregion -- ILobbyApiClient -----
        private event Action<IClientLobbyNB> LobbyReady;

        private IClientLobbyNB m_lobbyNB;
        private IDisposable m_lifecycleToken;

        public NetLobbyApiClient(NetworkObjectRegistry registry, IClientAppCtrl client)
        {
            m_registry = registry;
            m_client = client;
            m_client.ClientConnectionStateChanged += OnConnectionChanged;
            this.LTrace("+++");
            Init();
        }

        public void Init()
        {
            this.LTrace("+++");
            m_lifecycleToken = m_registry.BindLifecycle<IClientLobbyNB>(
                onSpawn: lobby =>
                {
                    this.LDebug("onSpawn ++");
                    m_lobbyNB = lobby;
                    m_lobbyNB.ServerInfoReceived += HandleServerInfo;
                    m_lobbyNB.UserInfoUpdated += HandleUserInfo;
                    m_lobbyNB.AlertReceived += HandleAlert;
                    m_lobbyNB.GameJoinFailed += HandleGameJoinFailed;
                    m_lobbyNB.NewGameCompleted += HandleNewGameCompleted;
                    m_lobbyNB.UserListReceived += HandleUserList;
                    m_lobbyNB.UserDisconnectCompleted += HandleDisconnectResult;
                    LobbyReady?.Invoke(m_lobbyNB);
                    TryRequestInitialServerInfo();
                },
                onDespawn: () =>
                {
                    this.LDebug("onDeSpawn --");
                    Unsubscribe();
                    m_lobbyNB = null;
                    m_initialServerInfoRequested = false;
                    m_hasSettings = false;
                    Settings = default;
                    SessionChanged?.Invoke();
                }
            );
        }

        private void WithLobbyNB(Action<IClientLobbyNB> callback)
        {
            if (m_lobbyNB != null)
            {
                this.LDebug("Lobby Already ready");
                callback(m_lobbyNB);
                return;
            }

            void Handler(IClientLobbyNB lobby)
            {
                LobbyReady -= Handler;
                callback(lobby);
            }

            LobbyReady += Handler;
            this.LDebug("Lobby NOT ready (wait)");
        }

        private void HandleServerInfo(ServerInfoContract contract)
        {
            Settings = contract.Settings;
            m_hasSettings = Settings.UserListRefreshIntervalSeconds > 0;
            SessionChanged?.Invoke();
            this.LDebug(() => $"++ {contract}");
            ServerInfoUpdated?.Invoke(contract);
            if (!m_roomRestoreChecked && IsReady && m_client.OpenGameId != 0 && m_client.User != null)
            {
                m_roomRestoreChecked = true;
                int roomId = m_client.OpenGameId;
                var game = contract.Games.FirstOrDefault(item => item.GameId == roomId);
                if (game.GameId != roomId)
                {
                    m_client.OpenLobbyScene();
                    m_client.ShowToast("The previous game is no longer available.");
                }
                else if (
                    !m_client.User.GameSlot.HasValue
                    || m_client.User.GameSlot.Value.GameId != roomId
                    || game.Status == FoobbuteoGameStatus.Ended
                )
                {
                    m_restoringRoom = roomId;
                    SendGameJoinRequest(roomId);
                }
            }
        }

        private void HandleGameJoinFailed(int roomId, string message)
        {
            if (m_restoringRoom == roomId)
            {
                m_restoringRoom = 0;
                m_client.OpenLobbyScene();
            }
            HandleAlert(message);
        }

        private void HandleUserInfo(GameUser user)
        {
            this.LDebug(() => $"++ {user}");
            m_client.User = user;
        }

        private void HandleNewGameCompleted(NewGameResult result) => NewGameCompleted?.Invoke(result);

        private void HandleAlert(string message)
        {
            AlertReceived?.Invoke(message);
        }

        public void Dispose()
        {
            m_client.ClientConnectionStateChanged -= OnConnectionChanged;
            this.LTrace("--");
            Unsubscribe();
            m_lifecycleToken?.Dispose();
            m_lobbyNB = null;
            m_initialServerInfoRequested = false;
            m_hasSettings = false;
            Settings = default;
            SessionChanged?.Invoke();
        }

        private void OnConnectionChanged()
        {
            if (!m_client.IsClientConnected)
            {
                m_roomRestoreChecked = false;
                m_restoringRoom = 0;
                m_initialServerInfoRequested = false;
                m_hasSettings = false;
                Settings = default;
            }
            TryRequestInitialServerInfo();
            SessionChanged?.Invoke();
        }

        private void TryRequestInitialServerInfo()
        {
            if (
                m_initialServerInfoRequested
                || !m_client.IsClientConnected
                || m_lobbyNB == null
                || !m_lobbyNB.IsSpawned
            )
                return;

            // Receiving settings is the result of this handshake, not a prerequisite for it.
            m_initialServerInfoRequested = true;
            RequestServerInfo();
        }

        private void Unsubscribe()
        {
            if (m_lobbyNB != null)
            {
                m_lobbyNB.ServerInfoReceived -= HandleServerInfo;
                m_lobbyNB.UserInfoUpdated -= HandleUserInfo;
                m_lobbyNB.AlertReceived -= HandleAlert;
                m_lobbyNB.GameJoinFailed -= HandleGameJoinFailed;
                m_lobbyNB.NewGameCompleted -= HandleNewGameCompleted;
                m_lobbyNB.UserListReceived -= HandleUserList;
                m_lobbyNB.UserDisconnectCompleted -= HandleDisconnectResult;
            }
        }
    }
}
