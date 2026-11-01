// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace Tests.Editor
{
    public class UserHandlerTests
    {
        private const ulong k_existingClientId = 91001;
        private const ulong k_incomingClientId = 91002;
        private const int k_userId = 92001;
        private const int k_playerId = 93001;
        private const string k_testToken = "approval-test-token";

        private GameObject m_object;
        private UserHandler m_handler;
        private NetworkManager m_networkManager;
        private UserRegistry m_registry;
        private PendingAuthenticationRepository m_authentication;
        private ServerUser m_existingUser;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            // Keep Unity lifecycle callbacks inactive; tests drive only the approval boundary.
            m_object = new GameObject("User handler approval test");
            m_object.SetActive(false);
            m_networkManager = m_object.AddComponent<NetworkManager>();
            // NGO's editor reset callback may be cleared after Play Mode with Domain Reload disabled.
            m_networkManager.NetworkConfig = new NetworkConfig();
            m_handler = m_object.AddComponent<UserHandler>();
            m_registry = new UserRegistry();
            m_existingUser = User(k_existingClientId);
            m_registry.RegisterUser(m_existingUser);
            m_authentication = new PendingAuthenticationRepository();
            SetField("m_NetworkManager", m_networkManager);
            SetField("m_userRegistry", m_registry);
            SetField("m_authenticationRepository", m_authentication);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_object);
            ServiceLocator.ClearAll();
        }

        [Test]
        public async Task Approval_WhenAuthenticationFails_PreservesExistingConnection()
        {
            AddPendingClient();
            var response = new NetworkManager.ConnectionApprovalResponse { Pending = true };
            Task approval = StartApproval(response);

            m_authentication.Completion.SetResult(null);
            await approval;

            AssertExistingConnection(response);
            Assert.AreEqual(
                ClientDisconnectReason.InvalidToken,
                response.Reason,
                "Invalid authentication must retain its rejection reason."
            );
        }

        [Test]
        public async Task Approval_WhenPendingClientExpires_DoesNotReplaceExistingConnection()
        {
            AddPendingClient();
            var response = new NetworkManager.ConnectionApprovalResponse { Pending = true };
            Task approval = StartApproval(response);
            m_networkManager.PendingClients.Remove(k_incomingClientId);

            m_authentication.Completion.SetResult(User(k_incomingClientId));
            await approval;

            AssertExistingConnection(response);
        }

        [TestCase("OnPreShutdown")]
        [TestCase("OnServerStopped")]
        [TestCase("OnDisable")]
        public async Task Approval_FromPreviousServerSession_DoesNotReplaceExistingConnection(string lifecycleCallback)
        {
            AddPendingClient();
            var response = new NetworkManager.ConnectionApprovalResponse { Pending = true };
            Task approval = StartApproval(response);
            if (lifecycleCallback == "OnServerStopped")
                Invoke(lifecycleCallback, false);
            else
                Invoke(lifecycleCallback);
            // A restarted server can reuse client IDs. Presence alone is insufficient.
            m_networkManager.PendingClients.Remove(k_incomingClientId);
            AddPendingClient();

            m_authentication.Completion.SetResult(User(k_incomingClientId));
            await approval;

            AssertExistingConnection(response);
        }

        [Test]
        public async Task Approval_ForCurrentPendingClient_RegistersAuthenticatedUser()
        {
            m_registry.UnregisterUserOnDisconnect(k_existingClientId);
            AddPendingClient();
            var response = new NetworkManager.ConnectionApprovalResponse { Pending = true };
            Task approval = StartApproval(response);

            m_authentication.Completion.SetResult(User(k_incomingClientId));
            await approval;

            Assert.IsTrue(response.Approved, "A live pending request with valid authentication must be approved.");
            Assert.IsFalse(response.Pending, "Authentication completion must release the pending approval.");
            Assert.AreEqual(
                k_userId,
                m_registry.GetUser(k_incomingClientId).UserId,
                "Approval must register the authenticated account."
            );
        }

        [Test]
        public async Task HostApproval_WaitsForDatabaseThenRegistersReturnedUserSynchronously()
        {
            SetHostPayload();
            Task<bool> preparation = m_handler.TryPrepareHostApprovalAsync();

            Assert.IsFalse(preparation.IsCompleted, "Host startup must wait while database authentication is pending.");
            Assert.IsFalse(
                m_registry.TryGetUser(NetworkManager.ServerClientId, out _),
                "The host must not receive a placeholder user while authentication is pending."
            );

            const int databaseUserId = 94001;
            const int databasePlayerId = 95001;
            const string databaseName = "Database host user";
            var databaseUser = new ServerUser(
                NetworkManager.ServerClientId,
                databaseUserId,
                databasePlayerId,
                databaseName,
                UserRoles.Admin
            );
            m_authentication.Completion.SetResult(databaseUser);

            Assert.IsTrue(await preparation, "Successful database authentication must allow host startup.");
            var response = new NetworkManager.ConnectionApprovalResponse();
            Invoke(
                "ApprovalCheck",
                new NetworkManager.ConnectionApprovalRequest
                {
                    ClientNetworkId = NetworkManager.ServerClientId,
                    Payload = m_networkManager.NetworkConfig.ConnectionData,
                },
                response
            );

            Assert.IsTrue(response.Approved, "The host callback must approve synchronously using the database user.");
            Assert.IsFalse(response.Pending, "The local host callback must never defer approval.");
            Assert.AreSame(
                databaseUser,
                m_registry.GetUser(NetworkManager.ServerClientId),
                "The lobby must read the exact authenticated database user from the registry."
            );
            Assert.AreEqual(1, m_authentication.CallCount, "The one-time host token must be consumed only once.");
            Assert.AreEqual(
                NetworkManager.ServerClientId,
                m_authentication.LastClientId,
                "Database authentication must identify the local host client."
            );
            Assert.AreEqual(
                k_testToken,
                m_authentication.LastToken,
                "Host authentication must consume the configured connection token."
            );
            Assert.IsNull(GetField("m_preparedHostUser"), "Approval must release the cached host identity.");
        }

        [Test]
        public async Task HostApproval_WhenTokenIsRejected_DoesNotPrepareOrRegisterUser()
        {
            SetHostPayload();
            Task<bool> preparation = m_handler.TryPrepareHostApprovalAsync();

            m_authentication.Completion.SetResult(null);

            Assert.IsFalse(await preparation, "An invalid database token must prevent host startup.");
            Assert.IsFalse(
                m_registry.TryGetUser(NetworkManager.ServerClientId, out _),
                "Rejected authentication must not register a mock host identity."
            );
            Assert.IsNull(GetField("m_preparedHostUser"), "A rejected token must leave no cached host identity.");
        }

        [TestCase("OnPreShutdown")]
        [TestCase("OnServerStopped")]
        [TestCase("OnDisable")]
        [TestCase("CancelPendingApprovals")]
        public async Task HostApproval_AfterSessionEnds_DiscardsDatabaseResult(string lifecycleCallback)
        {
            SetHostPayload();
            Task<bool> preparation = m_handler.TryPrepareHostApprovalAsync();
            if (lifecycleCallback == "OnServerStopped")
                Invoke(lifecycleCallback, false);
            else
                Invoke(lifecycleCallback);

            m_authentication.Completion.SetResult(User(NetworkManager.ServerClientId));

            Assert.IsFalse(await preparation, "Authentication from an ended session must not start a host.");
            Assert.IsNull(GetField("m_preparedHostUser"), "An ended session must not retain a prepared host identity.");
            Assert.IsFalse(
                m_registry.TryGetUser(NetworkManager.ServerClientId, out _),
                "A late database result must not authorize the host."
            );
        }

        [Test]
        public void HostApproval_WhenDatabaseIsUnavailable_DoesNotFallBackToMockUser()
        {
            SetHostPayload();
            Task<bool> preparation = m_handler.TryPrepareHostApprovalAsync();

            m_authentication.Completion.SetException(new RepositoryUnavailableException("Test database unavailable."));

            Assert.ThrowsAsync<RepositoryUnavailableException>(
                async () => await preparation,
                "The host startup boundary must receive database failures instead of authorizing a mock user."
            );
            Assert.IsNull(GetField("m_preparedHostUser"), "A database failure must leave no prepared host identity.");
        }

        private void SetHostPayload()
        {
            m_networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new ConnectionPayload { token = k_testToken })
            );
        }

        private object GetField(string field) =>
            typeof(UserHandler).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m_handler);

        private void AssertExistingConnection(NetworkManager.ConnectionApprovalResponse response)
        {
            Assert.IsFalse(response.Approved, "An invalid or abandoned request must not be approved.");
            Assert.IsFalse(response.Pending, "Completed authentication must release the pending response.");
            Assert.AreSame(
                m_existingUser,
                m_registry.GetUser(k_existingClientId),
                "The active user must not be displaced."
            );
            Assert.AreEqual(
                k_existingClientId,
                m_existingUser.ClientId,
                "The original connection must remain authorized."
            );
            Assert.IsFalse(
                m_registry.TryGetUser(k_incomingClientId, out _),
                "The abandoned client must not gain authorization."
            );
        }

        private void AddPendingClient() => m_networkManager.PendingClients.Add(k_incomingClientId, new PendingClient());

        private Task StartApproval(NetworkManager.ConnectionApprovalResponse response)
        {
            SetField("m_pendingApprovalChecks", 1);
            return (Task)Invoke("ApprovalCheckAsync", response, k_testToken, k_incomingClientId);
        }

        private object Invoke(string method, params object[] arguments) =>
            typeof(UserHandler)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_handler, arguments);

        private void SetField(string field, object value) =>
            typeof(UserHandler)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_handler, value);

        private static ServerUser User(ulong clientId) =>
            new(clientId, k_userId, k_playerId, "Approval test user", UserRoles.Player);

        private sealed class PendingAuthenticationRepository : IAuthenticationRepository
        {
            public TaskCompletionSource<ServerUser> Completion { get; } = new();
            public int CallCount { get; private set; }
            public ulong LastClientId { get; private set; }
            public string LastToken { get; private set; }

            public Task<ServerUser> ValidateAndConsumeTokenAsync(ulong clientId, string token)
            {
                CallCount++;
                LastClientId = clientId;
                LastToken = token;
                return Completion.Task;
            }

            public void Dispose() { }
        }
    }
}
