// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Lobby
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class LobbyNB : IClientLobbyNB
    {
        private void OnNetworkSpawnClient()
        {
            gameObject.name = "LobbyClient";
        }

        private void OnNetworkPostSpawnClient()
        {
            ServiceLocator.Current.Get<NetworkObjectRegistry>().Register<IClientLobbyNB>(this);
            // Reconnecting clients may still be displaying a game. The application restores that room.
            AppCtx.Client.OnLobbySpawned();
        }

        private void DisposeClient()
        {
            ServiceLocator.Current.Get<NetworkObjectRegistry>().Unregister<IClientLobbyNB>();
        }

        #region ++ IClientLobbyNB +++++++

        public event Action<ServerInfoContract> ServerInfoReceived;
        public event Action<GameUser> UserInfoUpdated;
        public event Action<string> AlertReceived;
        public event Action<int, string> GameJoinFailed;

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true, InvokePermission = RpcInvokePermission.Server)]
        private void ToClient_GameJoinFailedRpc(int gameId, string message, RpcParams rpcParams = default) =>
            GameJoinFailed?.Invoke(gameId, message);

        public event Action<NewGameResult> NewGameCompleted;

        #endregion -- IClientLobbyNB ------

        #region ++ To Client RPC +++++

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        public void ToClient_SendUserInfoRpc(GameUser user, RpcParams _ = default)
        {
            UserInfoUpdated?.Invoke(user);
        }

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true, InvokePermission = RpcInvokePermission.Server)]
        public void ToClient_SendServerInfoRpc(ServerInfoContract serverInfo, RpcParams _ = default)
        {
            this.LogDebug(() =>
                $"[Client: ^{NetworkManager.Singleton.LocalClientId}] Rpc from Server, serverInfo games: {serverInfo.Games.Length} {this.InfoTag()}"
            );
            if (ServerInfoReceived == null)
            {
                this.LogWarn($"OnServerInfoReceived==null {this.InfoTag()}");
                return;
            }
            if (ServerInfoReceived.GetInvocationList().Length == 0)
            {
                this.LogWarn($"OnServerInfoReceived.GetInvocationList is empty {this.InfoTag()}");
                // return;
            }
            // this.LogWarn(
            //     $"OnServerInfoReceived.GetInvocationList length: {OnServerInfoReceived.GetInvocationList().Length}"
            // );
            ServerInfoReceived?.Invoke(serverInfo);
        }

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        public void ToClient_NewGameResultRpc(NewGameResult result, RpcParams _ = default)
        {
            NewGameCompleted?.Invoke(result);
        }

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        public void ToClient_AlertRpc(string message, RpcParams _ = default)
        {
            AlertReceived?.Invoke(message);
        }

        #endregion -- To Client RPC ------
    }
}
