// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared.Users;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Lobby
{
    public partial class LobbyNB
    {
        private const string k_AdministratorDisconnectReason = ClientDisconnectReason.Kicked;
        private const string k_TooManyUserRequests = "Too many requests. Please try again.";

        public event Action<UserListResponse> UserListReceived;
        public event Action<UserDisconnectResult> UserDisconnectCompleted;

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_RequestUserListRpc(ulong requestId, int roomId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            UserListResponse response;
            try
            {
                response = !AllowIncomingRpc(clientId)
                    ? new UserListResponse(requestId, roomId, error: k_TooManyUserRequests)
                    : new UserListResponse(
                        requestId,
                        roomId,
                        m_serverLobbyNBCtrl.GetUsers(clientId, roomId, IsClientConnected)
                    );
            }
            catch (AppException exception)
            {
                response = new UserListResponse(requestId, roomId, error: exception.Message);
            }
            catch (Exception exception)
            {
                this.LException(exception);
                response = new UserListResponse(requestId, roomId, error: "User list could not be loaded.");
            }
            if (IsSpawned && IsClientConnected(clientId))
                ToClient_UserListRpc(response, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void ToServer_DisconnectUserRpc(
            ulong requestId,
            int userId,
            ulong targetClientId,
            RpcParams rpcParams = default
        )
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            var result = new UserDisconnectResult(requestId);
            try
            {
                if (!AllowIncomingRpc(clientId))
                    throw new AppException(k_TooManyUserRequests);
                m_serverLobbyNBCtrl.AuthorizeUserDisconnect(
                    clientId,
                    userId,
                    targetClientId,
                    NetworkManager.ServerClientId,
                    IsClientConnected
                );
                NetworkManager.DisconnectClient(targetClientId, k_AdministratorDisconnectReason);
            }
            catch (AppException exception)
            {
                result = new UserDisconnectResult(requestId, exception.Message);
            }
            catch (Exception exception)
            {
                this.LException(exception);
                result = new UserDisconnectResult(requestId, "User could not be disconnected.");
            }
            if (IsSpawned && IsClientConnected(clientId))
                ToClient_UserDisconnectResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        private bool IsClientConnected(ulong clientId) => NetworkManager.ConnectedClients.ContainsKey(clientId);

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true, InvokePermission = RpcInvokePermission.Server)]
        public void ToClient_UserListRpc(UserListResponse response, RpcParams rpcParams = default) =>
            UserListReceived?.Invoke(response);

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true, InvokePermission = RpcInvokePermission.Server)]
        public void ToClient_UserDisconnectResultRpc(UserDisconnectResult result, RpcParams rpcParams = default) =>
            UserDisconnectCompleted?.Invoke(result);
    }
}
