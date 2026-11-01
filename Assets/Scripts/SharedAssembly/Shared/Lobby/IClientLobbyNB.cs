// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Lobby
{
    public interface IClientLobbyNB : INetworkBehaviour
    {
        event Action<ServerInfoContract> ServerInfoReceived;
        event Action<GameUser> UserInfoUpdated;
        event Action<string> AlertReceived;
        event Action<int, string> GameJoinFailed;

        // void NewGame();
        event Action<NewGameResult> NewGameCompleted;
        event Action<UserListResponse> UserListReceived;
        event Action<UserDisconnectResult> UserDisconnectCompleted;
        void ToServer_RequestUserListRpc(ulong requestId, int roomId, RpcParams rpcParams = default);
        void ToServer_DisconnectUserRpc(
            ulong requestId,
            int userId,
            ulong targetClientId,
            RpcParams rpcParams = default
        );
        void ToServer_NewGameRpc(NewGameOptions options, RpcParams rpcParams = default);

        // void DeleteGame(int gameId);
        void ToServer_DeleteGameRpc(int gameId, RpcParams rpcParams = default);

        // void RequestServerInfo();
        void ToServer_RequestServerInfoRpc(RpcParams rpcParams = default);

        // void SendGameJoinRequest(int gameId);
        void ToServer_SendGameJoinRpc(int gameId, RpcParams rpcParams = default);
    }
}
