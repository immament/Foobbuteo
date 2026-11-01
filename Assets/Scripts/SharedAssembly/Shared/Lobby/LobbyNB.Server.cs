// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Lobby
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class LobbyNB
    {
        [SerializeField]
        private GameObject m_gamePrefab;
        public GameObject GamePrefab => m_gamePrefab;

        private IServerLobbyNBCtrl m_serverLobbyNBCtrl;

        private void InitServer()
        {
            gameObject.name = IsHost ? "LobbyHost" : "LobbyServer";
            m_serverLobbyNBCtrl = ServiceLocator.Current.Get<IServerLobbyNBCtrl>();
            _ = InitServerAsync();
            // m_serverLobbyNBCtrl = AppCtx.Server.CreateServerLobbyNBCtrl(this);
        }

        private async System.Threading.Tasks.Task InitServerAsync()
        {
            try
            {
                await m_serverLobbyNBCtrl.InitAsync(this);
            }
            catch (OperationCanceledException)
            {
                this.LDebug(() => $"Normal shutdown while initialization is still awaiting the database.");
                // Normal shutdown while initialization is still awaiting the database.
            }
            catch (Exception exception)
            {
                this.LError($"Server lobby initialization failed: {exception.Message}");
                this.LException(exception);
                throw;
            }
        }

        #region ++ To Server RPC +++++

        private bool AllowIncomingRpc(ulong clientId)
        {
            if (ServerRpcRateLimiter.TryConsume(clientId, out bool shouldLogWarning))
                return true;

            if (shouldLogWarning)
                this.LWarn($"[Server] RPC rate limit exceeded by client ^{clientId}");
            return false;
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_DeleteGameRpc(int gameId, RpcParams rpcParams = default)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            this.LogDebug(() =>
                $"[Server] from [Client: ^{rpcParams.Receive.SenderClientId}] delete ${gameId} request {this.InfoTag()}"
            );
            try
            {
                m_serverLobbyNBCtrl.DeleteGame(rpcParams.Receive.SenderClientId, gameId);
            }
            catch (AppException e)
            {
                this.LWarn(e.Message);
                ToClient_AlertRpc(e.Message, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_NewGameRpc(NewGameOptions options, RpcParams rpcParams = default)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
            {
                SendNewGameResult(
                    rpcParams.Receive.SenderClientId,
                    new NewGameResult(false, error: "Too many requests. Please try again.")
                );
                return;
            }

            this.LogDebug(() =>
                $"[Server] from [Client: ^{rpcParams.Receive.SenderClientId}] New game request {this.InfoTag()}"
            );
            _ = HandleNewGameRequestAsync(rpcParams.Receive.SenderClientId, options);
        }

        private async System.Threading.Tasks.Task HandleNewGameRequestAsync(ulong clientId, NewGameOptions options)
        {
            try
            {
                int gameId = await m_serverLobbyNBCtrl.CreateNewGameAsync(clientId, options);
                SendNewGameResult(clientId, new NewGameResult(true, gameId));
            }
            catch (AppException e)
            {
                this.LWarn(e.Message);
                SendNewGameResult(clientId, new NewGameResult(false, error: e.Message));
            }
            catch (OperationCanceledException)
            {
                this.LDebug("Game creation was canceled during server shutdown.");
            }
            catch (Exception e)
            {
                this.LExceptionTree(e, "Game creation failed");
                SendNewGameResult(clientId, new NewGameResult(false, error: "Game could not be created."));
            }
        }

        private void SendNewGameResult(ulong clientId, NewGameResult result)
        {
            if (IsSpawned && NetworkManager.ConnectedClients.ContainsKey(clientId))
                ToClient_NewGameResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_RequestServerInfoRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!AllowIncomingRpc(clientId))
                return;

            this.LogDebug(() => $"[Server] Rpc from [Client: ^{clientId}] {this.InfoTag()}");
            try
            {
                var user = m_serverLobbyNBCtrl.GetUserInfo(clientId);
                ToClient_SendUserInfoRpc(user, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                ToClient_SendServerInfoRpc(
                    m_serverLobbyNBCtrl.GetServerInfoContract(),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp)
                );
                m_serverLobbyNBCtrl.OnClientLobbyReady(clientId);
            }
            catch (AppException ex)
            {
                m_serverLobbyNBCtrl.OnClientLobbyReady(clientId);
#if UNITY_EDITOR
                if (!NetcodeExtensions.IsHostClientId(clientId))
                {
                    this.LWarn(ex.Message);
                    ToClient_AlertRpc(ex.Message, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                }
#else
                this.LWarn(ex.Message);
                ToClient_AlertRpc(ex.Message, RpcTarget.Single(clientId, RpcTargetUse.Temp));
#endif
            }
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_SendGameJoinRpc(int gameId, RpcParams rpcParams = default)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            this.LogDebug(() =>
                $"[Server] Rpc from [Client: ^{rpcParams.Receive.SenderClientId}] - Join request to game: ${gameId} {this.InfoTag()}"
            );
            try
            {
                m_serverLobbyNBCtrl.AddClientToGame(rpcParams.Receive.SenderClientId, gameId);
            }
            catch (AppException e)
            {
                this.LException(e);
                ToClient_GameJoinFailedRpc(
                    gameId,
                    e.Message,
                    RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)
                );
            }
        }

        #endregion --
    }
}
