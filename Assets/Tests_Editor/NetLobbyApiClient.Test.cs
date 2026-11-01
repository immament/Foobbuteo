// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imma.Foobbuteo.Client.Lobby;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class NetLobbyApiClientTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void InitialConnection_WaitsForConnectionAndLobby(bool connectFirst)
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);

            if (connectFirst)
                client.SetConnected(true);
            else
                registry.Register<IClientLobbyNB>(lobby);

            Assert.AreEqual(
                0,
                lobby.ServerInfoRequests,
                $"Initialization must wait for both prerequisites (connectFirst={connectFirst})."
            );

            if (connectFirst)
                registry.Register<IClientLobbyNB>(lobby);
            else
                client.SetConnected(true);

            Assert.AreEqual(
                1,
                lobby.ServerInfoRequests,
                $"The second prerequisite must trigger one request (connectFirst={connectFirst})."
            );
            Assert.IsTrue(
                lobby.AllHandlersAttachedWhenRequested,
                "The readiness request must be sent only after all lobby response handlers are attached."
            );
        }

        [Test]
        public void AlreadyConnectedAndRegistered_ConstructionRequestsServerInfo()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby();
            client.SetConnected(true);
            registry.Register<IClientLobbyNB>(lobby);

            using var api = new NetLobbyApiClient(registry, client);

            Assert.AreEqual(
                1,
                lobby.ServerInfoRequests,
                "An API created after connection and lobby registration must still initialize."
            );
            Assert.IsTrue(
                lobby.AllHandlersAttachedWhenRequested,
                "Immediate registry binding must attach response handlers before requesting data."
            );
        }

        [Test]
        public void RepeatedConnectedNotifications_DoNotDuplicateInitialRequest()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby { OnServerInfoRequest = () => client.SetConnected(true) };
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);

            client.SetConnected(true);
            client.SetConnected(true);

            Assert.AreEqual(
                1,
                lobby.ServerInfoRequests,
                "Repeated or reentrant connection notifications must not send duplicate initial requests."
            );
        }

        [Test]
        public void Reconnect_RequestsAgainAndReceivesFreshServerInfo()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);
            int serverInfoUpdates = 0;
            ServerInfoContract latest = default;
            api.ServerInfoUpdated += contract =>
            {
                serverInfoUpdates++;
                latest = contract;
            };
            client.SetConnected(true);
            lobby.SendServerInfo(ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds);
            Assert.IsTrue(api.IsReady, "Receiving initial settings must make the connected lobby API ready.");

            client.SetConnected(false);

            Assert.IsFalse(api.IsReady, "Logging out must clear API readiness.");
            Assert.AreEqual(
                0,
                api.Settings.UserListRefreshIntervalSeconds,
                "Logging out must clear the previous server's settings."
            );

            client.SetConnected(true);
            Assert.IsFalse(api.IsReady, "Reconnecting must wait for fresh settings before declaring readiness.");
            const int refreshedIntervalSeconds = 25;
            lobby.SendServerInfo(refreshedIntervalSeconds);

            Assert.AreEqual(
                2,
                lobby.ServerInfoRequests,
                "Logging in again with the same API and lobby must send a new initial request."
            );
            Assert.IsTrue(api.IsReady, "Receiving fresh server settings must restore readiness.");
            Assert.AreEqual(
                refreshedIntervalSeconds,
                api.Settings.UserListRefreshIntervalSeconds,
                "Reconnect must use newly received settings instead of retaining old settings."
            );
            Assert.AreEqual(
                2,
                serverInfoUpdates,
                "Both initial and reconnect responses must reach game-list subscribers."
            );
            Assert.AreEqual(
                refreshedIntervalSeconds,
                latest.Settings.UserListRefreshIntervalSeconds,
                "Subscribers must receive the fresh reconnect snapshot."
            );
        }

        [Test]
        public void LobbyReplacement_RequestsAgainAndClearsPreviousSettings()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var previous = new FakeLobby();
            var replacement = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            client.SetConnected(true);
            registry.Register<IClientLobbyNB>(previous);
            previous.SendServerInfo(ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds);

            registry.SetActive<IClientLobbyNB>(replacement);

            Assert.AreEqual(
                1,
                replacement.ServerInfoRequests,
                "Replacing the lobby must initialize the replacement even while connected."
            );
            Assert.IsFalse(api.IsReady, "A replacement must wait for its own server settings.");
            Assert.AreEqual(
                0,
                api.Settings.UserListRefreshIntervalSeconds,
                "Despawn must discard settings from the previous lobby."
            );
            Assert.IsFalse(previous.HasAnyHandlers, "The replaced lobby must release all API response handlers.");
        }

        [Test]
        public void UnspawnedLobby_DoesNotRequestUntilSpawned()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby { IsSpawned = false };
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);

            client.SetConnected(true);

            Assert.AreEqual(
                0,
                lobby.ServerInfoRequests,
                "Connection alone must not send an RPC through an unspawned lobby."
            );

            lobby.IsSpawned = true;
            client.SetConnected(true);

            Assert.AreEqual(
                1,
                lobby.ServerInfoRequests,
                "A spawned lobby must allow the next readiness check to initialize."
            );
        }

        [Test]
        public void Dispose_UnsubscribesAndPreventsFurtherInitialization()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            client.SetConnected(true);
            registry.Register<IClientLobbyNB>(lobby);
            lobby.SendServerInfo(ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds);

            api.Dispose();
            client.SetConnected(false);
            client.SetConnected(true);
            var replacement = new FakeLobby();
            registry.SetActive<IClientLobbyNB>(replacement);

            Assert.IsFalse(lobby.HasAnyHandlers, "Disposal must detach every lobby response handler.");
            Assert.AreEqual(0, client.ConnectionSubscribers, "Disposal must detach connection notifications.");
            Assert.AreEqual(1, lobby.ServerInfoRequests, "Disposal must prevent reconnect requests.");
            Assert.AreEqual(
                0,
                replacement.ServerInfoRequests,
                "A disposed API must not initialize newly registered lobbies."
            );
            Assert.AreEqual(
                0,
                api.Settings.UserListRefreshIntervalSeconds,
                "Disposal must clear previously received settings."
            );
        }

        [Test]
        public void ExplicitRefresh_AfterInitializationStillRequestsServerInfo()
        {
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient();
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);
            client.SetConnected(true);

            api.RequestServerInfo();

            Assert.AreEqual(
                2,
                lobby.ServerInfoRequests,
                "The automatic-request guard must not block an explicit server-info refresh."
            );
        }

        [TestCase(false, FoobbuteoGameStatus.Ongoing, 1)]
        [TestCase(true, FoobbuteoGameStatus.Ongoing, 0)]
        [TestCase(true, FoobbuteoGameStatus.Ended, 1)]
        public void RestoringRoom_RejoinsSpectatorsAndLeavesActiveParticipantsToServerHandshake(
            bool assigned,
            FoobbuteoGameStatus status,
            int expectedRequests
        )
        {
            const int roomId = 42;
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient { OpenGameId = roomId, User = new GameUser() };
            if (assigned)
                client.User.RestoreGameSlot(Imma.Foobbuteo.Shared.GameSlot.ForManager(roomId, TeamIndex.Home));
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);
            client.SetConnected(true);

            lobby.SendGames(new GameInfoContract { GameId = roomId, Status = status });
            lobby.SendGames(new GameInfoContract { GameId = roomId, Status = status });

            Assert.AreEqual(
                expectedRequests,
                lobby.JoinRequests.Count,
                "Recovery must restore spectators once and avoid duplicating server-side participant restoration."
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RestoringRoom_WhenMissingOrRejectedReturnsToLobby(bool missing)
        {
            const int roomId = 42;
            using var registry = new NetworkObjectRegistry();
            var client = new FakeClient { OpenGameId = roomId, User = new GameUser() };
            var lobby = new FakeLobby();
            using var api = new NetLobbyApiClient(registry, client);
            registry.Register<IClientLobbyNB>(lobby);
            client.SetConnected(true);

            if (missing)
                lobby.SendGames();
            else
            {
                lobby.SendGames(new GameInfoContract { GameId = roomId, Status = FoobbuteoGameStatus.Ongoing });
                lobby.RejectJoin(roomId);
            }

            Assert.AreEqual(
                1,
                client.LobbyOpens,
                "An unavailable previous room must leave recovery in the connected lobby."
            );
            Assert.AreEqual(0, client.OpenGameId, "Fallback must clear the previous room target.");
        }

        private sealed class FakeLobby : IClientLobbyNB
        {
            public event Action<int, string> GameJoinFailed;
            public List<int> JoinRequests { get; } = new();

            public void SendGames(params GameInfoContract[] games) =>
                ServerInfoReceived?.Invoke(new ServerInfoContract(games));

            public void RejectJoin(int roomId) => GameJoinFailed?.Invoke(roomId, "Room is inaccessible.");

            public bool IsSpawned { get; set; } = true;
            public int ServerInfoRequests { get; private set; }
            public bool AllHandlersAttachedWhenRequested { get; private set; } = true;
            public Action OnServerInfoRequest { get; set; }
            public event Action<ServerInfoContract> ServerInfoReceived;
            public event Action<GameUser> UserInfoUpdated;
            public event Action<string> AlertReceived;
            public event Action<NewGameResult> NewGameCompleted;
            public event Action<UserListResponse> UserListReceived;
            public event Action<UserDisconnectResult> UserDisconnectCompleted;

            public bool HasAnyHandlers =>
                ServerInfoReceived != null
                || UserInfoUpdated != null
                || AlertReceived != null
                || NewGameCompleted != null
                || UserListReceived != null
                || UserDisconnectCompleted != null;

            public void ToServer_RequestServerInfoRpc(RpcParams rpcParams = default)
            {
                ServerInfoRequests++;
                AllHandlersAttachedWhenRequested &=
                    ServerInfoReceived != null
                    && UserInfoUpdated != null
                    && AlertReceived != null
                    && NewGameCompleted != null
                    && UserListReceived != null
                    && UserDisconnectCompleted != null;
                OnServerInfoRequest?.Invoke();
            }

            public void SendServerInfo(int refreshIntervalSeconds) =>
                ServerInfoReceived?.Invoke(
                    new ServerInfoContract(
                        Array.Empty<GameInfoContract>(),
                        new ClientRuntimeSettings(refreshIntervalSeconds)
                    )
                );

            public void ToServer_RequestUserListRpc(ulong requestId, int roomId, RpcParams rpcParams = default) { }

            public void ToServer_DisconnectUserRpc(
                ulong requestId,
                int userId,
                ulong targetClientId,
                RpcParams rpcParams = default
            ) { }

            public void ToServer_NewGameRpc(NewGameOptions options, RpcParams rpcParams = default) { }

            public void ToServer_DeleteGameRpc(int gameId, RpcParams rpcParams = default) { }

            public void ToServer_SendGameJoinRpc(int gameId, RpcParams rpcParams = default) => JoinRequests.Add(gameId);
        }

        private sealed class FakeClient : IClientAppCtrl
        {
            public ClientSessionView SessionView =>
                IsClientConnected ? ClientSessionView.Session : ClientSessionView.Login;
            public bool CanLogout => true;
            public int OpenGameId { get; set; }
            public int LobbyOpens { get; private set; }

            public void OnLobbySpawned() { }

            public GameUser User { get; set; }
            public bool IsClientConnected { get; private set; }
            public bool IsClientConnecting => false;
            public bool IsAuthenticating => false;
            public bool IsAuthenticationReady => true;
            public bool CanRememberLogin => false;
            public bool HasRememberedLogin => false;
            public string AuthenticationMessage => "";
            public event Action ClientConnectionStateChanged;
            public event Action<GameUser> UserInfoUpdated
            {
                add { }
                remove { }
            }
            public event Action AuthenticationStateChanged
            {
                add { }
                remove { }
            }
            public int ConnectionSubscribers => ClientConnectionStateChanged?.GetInvocationList().Length ?? 0;

            public void SetConnected(bool connected)
            {
                IsClientConnected = connected;
                ClientConnectionStateChanged?.Invoke();
            }

            public Task LoginAsync(string login, string password, bool remember) => Task.CompletedTask;

            public Task RetryRememberedLoginAsync() => Task.CompletedTask;

            public Task LogoutAsync() => Task.CompletedTask;

            public bool OpenLobbyScene()
            {
                LobbyOpens++;
                OpenGameId = 0;
                return true;
            }

            public void OpenGameScene(int gameId) { }

            public void StartClient() { }

            public void DevDisconnect() { }

            public void SetRootElement(VisualElement rootElement) { }

            public void ShowToast(string message, float durationInSeconds = 3) { }

            public void ShowBanner(string message, float durationInSeconds = 3) { }
        }
    }
}
