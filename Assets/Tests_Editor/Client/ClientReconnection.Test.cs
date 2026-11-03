// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public sealed class ClientReconnectionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject m_object;
        private NetworkManager m_manager;
        private UnityTransport m_transport;
        private ClientNetCtrl m_network;
        private ClientAppCtrl m_client;
        private IClientAppCtrl m_previousClient;
        private object m_connections;
        private ClientConnectionRecovery Recovery => (ClientConnectionRecovery)GetField(m_client, "m_recovery");

        [SetUp]
        public void SetUp()
        {
            m_previousClient = AppCtx.Client;
            m_object = new GameObject("Client reconnection test");
            m_transport = m_object.AddComponent<UnityTransport>();
            m_manager = m_object.AddComponent<NetworkManager>();
            m_manager.NetworkConfig = new NetworkConfig { NetworkTransport = m_transport };
            m_connections = GetField(m_manager, "ConnectionManager");
            SetField(m_connections, "Transport", m_transport);
            m_network = new ClientNetCtrl(m_manager);
            m_client = m_object.AddComponent<ClientAppCtrl>();
            SetField(m_client, "m_NetworkManager", m_manager);
            SetField(m_client, "m_clientNetCtrl", m_network);
            m_network.ConnectionStateChanged += () => Invoke(m_client, "OnClientConnectionStateChanged");
            AppCtx.Client = m_client;
        }

        [TearDown]
        public void TearDown()
        {
            SetConnected(false);
            Object.DestroyImmediate(m_object);
            AppCtx.Client = m_previousClient;
        }

        [TestCase(NetworkTransport.DisconnectEvents.ProtocolError)]
        [TestCase(NetworkTransport.DisconnectEvents.ProtocolTimeout)]
        [TestCase(NetworkTransport.DisconnectEvents.MaxConnectionAttempts)]
        [TestCase(NetworkTransport.DisconnectEvents.ClosedByRemote)]
        [TestCase(NetworkTransport.DisconnectEvents.Disconnected)]
        [TestCase(NetworkTransport.DisconnectEvents.TransportShutdown)]
        public void TransportLoss_WithWebsiteRecovery_SchedulesReconnect(NetworkTransport.DisconnectEvents cause)
        {
            ConfigureAuthentication();
            Connect();

            Disconnect(cause);

            Assert.IsTrue(Recovery.IsPending, $"Transport loss {cause} must permit website-token recovery.");
        }

        [TestCase(ClientDisconnectReason.Kicked, false)]
        [TestCase(ClientDisconnectReason.SessionReplaced, false)]
        [TestCase("Unknown server rejection", false)]
        [TestCase(ClientDisconnectReason.ServiceUnavailable, true)]
        [TestCase(ClientDisconnectReason.ServerBusy, true)]
        [TestCase(ClientDisconnectReason.InvalidToken, true)]
        public void ServerReason_TakesPrecedenceOverTransportFailure(string reason, bool retry)
        {
            ConfigureAuthentication();
            Connect();
            SetField(m_connections, "ServerDisconnectReason", reason);

            Disconnect(NetworkTransport.DisconnectEvents.ProtocolError);

            Assert.AreEqual(retry, Recovery.IsPending, $"Server reason '{reason}' must retain its recovery policy.");
        }

        [Test]
        public void TransportAuthenticationFailure_RemainsTerminalAfterShutdown()
        {
            ConfigureAuthentication();
            Connect();
            Disconnect(NetworkTransport.DisconnectEvents.AuthenticationFailure);
            SetTransportEvent(NetworkTransport.DisconnectEvents.TransportShutdown);
            Invoke(m_network, "OnClientStopped", false);

            Assert.IsFalse(
                Recovery.IsPending,
                "Shutdown must not turn a transport authentication failure into a retry."
            );
        }

        [Test]
        public void ShutdownCallbacks_PreserveScheduledRecoveryAndBackoff()
        {
            ConfigureAuthentication();
            Connect();
            Disconnect(NetworkTransport.DisconnectEvents.ProtocolError);
            double now = Time.realtimeSinceStartupAsDouble;
            Assert.IsTrue(Recovery.TryBegin(now, false, false), "Transport loss must schedule a retry.");

            SetTransportEvent(NetworkTransport.DisconnectEvents.TransportShutdown);
            Invoke(m_network, "OnPreShutdown");
            Invoke(m_network, "OnClientStopped", false);

            Assert.IsTrue(Recovery.IsPending, "Shutdown callbacks must preserve the pending reconnect.");
            Assert.IsFalse(
                Recovery.TryBegin(now, false, false),
                "Shutdown callbacks must not reset the retry deadline."
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EditorDevelopment_StopControlsScheduleRecoveryWithoutRememberedLogin(bool developmentButton)
        {
            ConfigureAuthentication(false);
            Connect();
            if (developmentButton)
                m_client.DevDisconnect();

            Disconnect(NetworkTransport.DisconnectEvents.TransportShutdown);
            Invoke(m_network, "OnClientStopped", false);

            Assert.IsTrue(
                Recovery.IsPending,
                $"Stop Client (development button={developmentButton}) must recover in Editor."
            );
        }

        [Test]
        public void AuthenticatedConnectionWithoutRecovery_DoesNotFallBackToDevelopmentToken()
        {
            ConfigureAuthentication(false);
            m_network.SetClientConfig(Connection());
            Connect();

            Disconnect(NetworkTransport.DisconnectEvents.ProtocolError);

            Assert.IsFalse(
                Recovery.IsPending,
                "A real authenticated connection must never fall back to a development token."
            );
        }

        [Test]
        public void HostShutdown_AfterServerRoleResets_DoesNotScheduleRecovery()
        {
            ConfigureAuthentication();
            var serverRole = typeof(NetworkClient).GetProperty("IsServer", PrivateInstance);
            serverRole.SetValue(m_manager.LocalClient, true);
            Connect();
            serverRole.SetValue(m_manager.LocalClient, false);

            Disconnect(NetworkTransport.DisconnectEvents.TransportShutdown);
            Invoke(m_network, "OnClientStopped", true);

            Assert.IsFalse(Recovery.IsPending, "Stopping a host must never reconnect its local client.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ApplicationExit_CancelsRecoveryAndIgnoresLaterDisconnects(bool editorExit)
        {
            ConfigureAuthentication();
            Connect();
            Disconnect(NetworkTransport.DisconnectEvents.ProtocolError);
            Assert.IsTrue(Recovery.IsPending, "The client must have a pending retry before application exit.");

            if (editorExit)
                Invoke(m_client, "OnPlayModeStateChanged", UnityEditor.PlayModeStateChange.ExitingPlayMode);
            else
                Invoke(m_client, "OnApplicationQuit");

            Assert.IsFalse(Recovery.IsPending, "Application exit must cancel recovery before disable callbacks.");
            Invoke(m_network, "OnClientStopped", false);
            Assert.IsFalse(Recovery.IsPending, "Late shutdown callbacks must not restart recovery after exit.");
        }

        [Test]
        public void Disposal_CancelsRecovery()
        {
            ConfigureAuthentication();
            Connect();
            Disconnect(NetworkTransport.DisconnectEvents.ProtocolError);
            Assert.IsTrue(Recovery.IsPending, "The test must have pending recovery before disposal.");

            // Edit Mode does not dispatch MonoBehaviour disable callbacks for this fixture.
            Invoke(m_client, "OnDisable");

            Assert.IsFalse(Recovery.IsPending, "Disabling the client must cancel pending recovery.");
        }

        [Test]
        public void NewConnectionAttempt_ClearsPreviousTerminalClassification()
        {
            ConfigureAuthentication();
            Connect();
            Disconnect(NetworkTransport.DisconnectEvents.AuthenticationFailure);
            Assert.IsFalse(m_network.CanRetryDisconnect, "The initial authentication failure must be terminal.");
            var config = Connection();
            config.token = null;
            m_network.SetClientConfig(config);

            m_network.StartClient();

            Assert.IsTrue(m_network.CanRetryDisconnect, "A new attempt must not retain the previous terminal cause.");
            Assert.IsEmpty(m_network.DisconnectReason, "A new attempt must clear the previous rejection message.");
        }

        [Test]
        public void InvalidGameToken_UpdateRequestsFreshWebsiteToken()
        {
            int refreshes = 0;
            const string renewedToken = "renewed-token";
            var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
            var auth = new ClientAuthenticationCoordinator(
                new UnusedWeb(),
                new EmptyRememberedSessionStore(),
                () => now,
                webSession: (connection, _) =>
                {
                    refreshes++;
                    Assert.IsNull(connection.token, "Recovery must not reuse the consumed website token.");
                    return Task.FromResult(
                        AuthenticationResult.ParseWebSession(
                            200,
                            "application/json",
                            new JObject
                            {
                                ["token"] = renewedToken,
                                ["tokenExpiresAtUtc"] = now.AddSeconds(50).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            }.ToString(),
                            connection,
                            now
                        )
                    );
                }
            );
            auth.AcceptExternalConfig(Connection());
            SetField(m_client, "m_authentication", auth);
            ClientConnectionConfig handedOff = null;
            auth.ConnectionReady += config => handedOff = config;
            Connect();
            SetField(m_connections, "ServerDisconnectReason", ClientDisconnectReason.InvalidToken);
            Disconnect(NetworkTransport.DisconnectEvents.AuthenticationFailure);

            Invoke(m_client, "Update");

            Assert.AreEqual(
                1,
                refreshes,
                "The retry loop must request a fresh website token after invalid game-token rejection."
            );
            Assert.AreEqual(
                renewedToken,
                handedOff?.token,
                "Only the newly issued token must reach the next connection."
            );
            Assert.IsFalse(auth.RequiresLogin, "Renewing a game token must preserve the website session.");
        }

        private void ConfigureAuthentication(bool website = true)
        {
            var auth = new ClientAuthenticationCoordinator(
                new UnusedWeb(),
                new EmptyRememberedSessionStore(),
                webSession: website
                    ? (_, _) => Task.FromResult(AuthenticationResult.Failure("network_error", "test"))
                    : null
            );
            if (website)
                auth.AcceptExternalConfig(Connection());
            SetField(m_client, "m_authentication", auth);
            auth.StateChanged += () => Invoke(m_client, "OnAuthenticationStateChanged");
        }

        private void Connect()
        {
            SetConnected(true);
            Invoke(m_network, "OnClientConnected", m_manager.LocalClientId);
            SetConnected(false);
        }

        private void Disconnect(NetworkTransport.DisconnectEvents cause)
        {
            SetTransportEvent(cause);
            SetField(
                m_connections,
                "m_DisconnectReason",
                $"[Disconnect Event][Client-26][TransportClientId-4294967296][{cause}] Gracefully disconnected."
            );
            Invoke(m_network, "OnClientDisconnect", m_manager.LocalClientId);
        }

        private void SetTransportEvent(NetworkTransport.DisconnectEvents cause) =>
            typeof(NetworkTransport).GetProperty("DisconnectEvent").SetValue(m_transport, cause);

        private void SetConnected(bool connected) =>
            typeof(NetworkClient)
                .GetProperty("IsConnected", PrivateInstance)
                .SetValue(m_manager.LocalClient, connected);

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, PrivateInstance).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);

        private static ClientConnectionConfig Connection() =>
            new()
            {
                token = "test-token",
                host = "localhost",
                port = 7777,
                webSocketPath = "/",
            };

        private sealed class UnusedWeb : IAuthenticationWebClient
        {
            public Task<AuthenticationResult> LoginAsync(
                string login,
                string password,
                bool remember,
                CancellationToken cancellation
            ) => throw new InvalidOperationException("This scenario must not submit credentials.");

            public Task<AuthenticationResult> RefreshTokenAsync(string token, CancellationToken cancellation) =>
                throw new InvalidOperationException("This scenario has no remembered session.");

            public Task<bool> RevokeAsync(string token, CancellationToken cancellation) => Task.FromResult(true);
        }
    }
}
