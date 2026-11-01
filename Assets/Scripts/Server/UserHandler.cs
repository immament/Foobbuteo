// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.dev;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Server
{
    internal class UserHandler : BaseMonoBehaviour
    {
        private const int k_maxPendingApprovalChecks = 16;
        private const string k_authenticationServiceUnavailableReason = ClientDisconnectReason.ServiceUnavailable;
        private int m_pendingApprovalChecks;
        private int m_approvalSessionVersion;
        private ServerUser m_preparedHostUser;
        private string m_preparedHostToken;
        private const string k_connectionReplacedReason = ClientDisconnectReason.SessionReplaced;

        [SerializeField]
        private NetworkManager m_NetworkManager;

        [SerializeField]
        private GameObject m_lobbyPrefab;
        private IAuthenticationRepository m_authenticationRepository;

        [SerializeReference]
        private IUserRegistry m_userRegistry;

        new void OnEnable()
        {
            // m_NetworkManager = NetworkManager.Singleton;
            Init();
        }

        private void Init()
        {
            CancelPendingApprovals();
            this.LDebug(() => $"++ {(m_NetworkManager == null ? " is null" : "")}");
            m_authenticationRepository = ServiceLocator.Current.Get<IAuthenticationRepository>();
            m_userRegistry = ServiceLocator.Current.Get<IUserRegistry>();

            if (m_NetworkManager == null)
            {
                this.LogError("m_NetworkManager==null");
                return;
            }

            // Move to server bootstrap
            m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
            m_NetworkManager.OnPreShutdown += OnPreShutdown;
            m_NetworkManager.OnServerStopped += OnServerStopped;

            if (m_debug)
            {
                RegisterDevCallbacks();
            }
        }

        private void RegisterDevCallbacks()
        {
            if (m_NetworkManager != null)
            {
                // m_NetworkManager.ConnectionApprovalCallback = ApprovalCheck;
                // m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
                m_NetworkManager.OnClientStarted += OnClientStarted;
                m_NetworkManager.OnClientStopped += OnClientStopped;
                m_NetworkManager.OnConnectionEvent += OnConnectionEvent;
                // m_NetworkManager.OnServerStarted += OnServerStarted;
                m_NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
                m_NetworkManager.OnTransportFailure += OnTransportFailure;
                // m_NetworkManager.OnReanticipate += OnReanticipate;
            }
        }

        private void ApprovalCheck(
            NetworkManager.ConnectionApprovalRequest req,
            NetworkManager.ConnectionApprovalResponse resp
        )
        {
            this.LogDebug(() => $"[^{req.ClientNetworkId}] ++ Start ApprovalCheck");
            if (ParsePayload(req, out string reason) is not ConnectionPayload payload)
            {
                resp.Approved = false;
                resp.Reason = reason;
                resp.Pending = false;
                this.LogWarn($"[^{req.ClientNetworkId}] Parse connection payload problem: {reason}");
                return;
            }

            if (req.IsHostClient() && m_preparedHostUser != null && m_preparedHostToken == payload.token)
            {
                var user = m_preparedHostUser;
                ClearPreparedHostApproval();
                RegisterUserOnLogin(req.ClientNetworkId, user, resp);
                return;
            }

#if UNITY_EDITOR
            if (DevEditorApprovalCheck(req, resp, payload))
                return;

#endif

            if (req.IsHostClient())
            {
                resp.Approved = false;
                resp.Reason = "Host authentication must complete before starting the host.";
                return;
            }

            resp.Pending = true;
            if (Interlocked.Increment(ref m_pendingApprovalChecks) > k_maxPendingApprovalChecks)
            {
                Interlocked.Decrement(ref m_pendingApprovalChecks);
                resp.Approved = false;
                resp.Reason = ClientDisconnectReason.ServerBusy;
                resp.Pending = false;
                this.LWarn($"[^{req.ClientNetworkId}] Connection approval limit reached");
                return;
            }
            _ = ApprovalCheckAsync(resp, payload.token, req.ClientNetworkId);
        }

        private static ConnectionPayload? ParsePayload(NetworkManager.ConnectionApprovalRequest req, out string reason)
        {
            string json = System.Text.Encoding.UTF8.GetString(req.Payload);
            if (string.IsNullOrEmpty(json))
            {
                reason = "Missing Connection Payload";
                return null;
            }

            ConnectionPayload? payload;
            try
            {
                payload = JsonUtility.FromJson<ConnectionPayload>(json);
            }
            catch (Exception ex)
            {
                reason = $"Parse connection payload problem: {ex.Message}";
                return null;
            }

            if (!payload.HasValue)
            {
                reason = "Invalid Connection Payload";
                return null;
            }

            if (string.IsNullOrEmpty(payload.Value.token))
            {
                reason = "Missing Connection Token";
                return null;
            }

            reason = null;
            return payload;
        }

        // TODO: why it simulate request why not just prepare user and token?
        internal async Task<bool> TryPrepareHostApprovalAsync()
        {
            ClearPreparedHostApproval();
            var request = new NetworkManager.ConnectionApprovalRequest
            {
                ClientNetworkId = NetworkManager.ServerClientId,
                Payload = m_NetworkManager.NetworkConfig.ConnectionData,
            };

            if (ParsePayload(request, out string reason) is not ConnectionPayload payload)
            {
                this.LogWarn($"Host authentication failed: {reason}");
                return false;
            }

            int approvalSessionVersion = m_approvalSessionVersion;
            var user = await m_authenticationRepository.ValidateAndConsumeTokenAsync(
                NetworkManager.ServerClientId,
                payload.token
            );
            if (this == null || approvalSessionVersion != m_approvalSessionVersion)
                return false;
            if (user == null)
            {
                this.LogWarn("Host authentication failed: token not found.");
                return false;
            }

            // NGO does not wait for Pending on the local host. Its callback must finish synchronously.
            m_preparedHostUser = user;
            m_preparedHostToken = payload.token;
            return true;
        }

        internal void ClearPreparedHostApproval()
        {
            m_preparedHostUser = null;
            m_preparedHostToken = null;
        }

        internal void CancelPendingApprovals()
        {
            m_approvalSessionVersion++;
            ClearPreparedHostApproval();
        }

#if UNITY_EDITOR
        // Returns true if approval processed finished
        private bool DevEditorApprovalCheck(
            NetworkManager.ConnectionApprovalRequest req,
            NetworkManager.ConnectionApprovalResponse resp,
            ConnectionPayload payload
        )
        {
            this.LogDebug(() => $"[^{req.ClientNetworkId}]");

            if (ServerConfig.Config.GetDataProvider() != DataProvider.Mock)
                return false;

            ServerUser user = DevEditorValidate(req.ClientNetworkId, payload.token);
            if (user != null)
            {
                RegisterUserOnLogin(req.ClientNetworkId, user, resp);
                return true;
            }

            this.LogWarn($"[^{req.ClientNetworkId}]: Dev Editor connection token not recognized");
            return false;
        }

        internal static ServerUser DevEditorValidate(ulong clientId, string token)
        {
            var configuredUser = DevAuthSettings.FindByToken(token);

            if (configuredUser == null)
                return null;

            return new ServerUser(
                clientId,
                configuredUser.userId,
                configuredUser.playerId,
                configuredUser.name,
                configuredUser.role,
                teams: configuredUser.managedTeams
            );
        }
#endif

        private async Task ApprovalCheckAsync(
            NetworkManager.ConnectionApprovalResponse resp,
            string token,
            ulong clientId
        )
        {
            int approvalSessionVersion = m_approvalSessionVersion;
            try
            {
                var user = await m_authenticationRepository.ValidateAndConsumeTokenAsync(clientId, token);

                // Authentication can outlive the approval timeout or the entire server session.
                if (!IsApprovalCurrent(clientId, approvalSessionVersion))
                {
                    resp.Approved = false;
                    return;
                }

                if (user == null)
                {
                    resp.Approved = false;
                    resp.Reason = ClientDisconnectReason.InvalidToken;
                    this.LWarn($"[^{clientId}] Connection rejected. Token not found. token length: {token?.Length}");
                    return;
                }
                this.LInfo($"[^{clientId}] ServerUser approved: @{user.UserId}");

                RegisterUserOnLogin(clientId, user, resp);
            }
            catch (RepositoryUnavailableException e)
            {
                this.LWarn($"[^{clientId}] Authentication repository unavailable");
                this.LogException(e);
                resp.Reason = k_authenticationServiceUnavailableReason;
                resp.Approved = false;
            }
            catch (Exception e)
            {
                this.LWarn($"[^{clientId}] Exception in ApprovalCheckAsync");
                this.LogException(e);
                resp.Reason = k_authenticationServiceUnavailableReason;
                resp.Approved = false;
            }
            finally
            {
                Interlocked.Decrement(ref m_pendingApprovalChecks);
                resp.Pending = false;
            }
        }

        private bool IsApprovalCurrent(ulong clientId, int approvalSessionVersion)
        {
            if (approvalSessionVersion != m_approvalSessionVersion || m_NetworkManager == null)
                return false;

            // Netcode approves the local host outside its remote pending-client collection.
            return clientId == NetworkManager.ServerClientId || m_NetworkManager.PendingClients.ContainsKey(clientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            // TODO: check if client is part of game
            if (m_userRegistry.TryGetUser(clientId, out var user))
            {
                this.LogInfo($"[^{clientId}] User @{user.UserId} connected.");
            }
            else
            {
                this.LogError($"[^{clientId}] User for Client not registered");
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            this.LogDebug(() => $"[^{clientId}] {TraceTag(m_NetworkManager)}");
            if (!m_NetworkManager.IsServer && m_NetworkManager.DisconnectReason != string.Empty)
            {
                this.LogWarn(
                    $"[^{clientId}] Approval Declined Reason: {m_NetworkManager.DisconnectReason} {TraceTag(m_NetworkManager)}"
                );
            }
            ServerRpcRateLimiter.RemoveClient(clientId);
            m_userRegistry.UnregisterUserOnDisconnect(clientId);
        }

        private static object TraceTag(NetworkManager nm)
        {
            return $"{(nm.IsClient ? "Client" : "")} {(nm.IsServer ? "Server" : "")}";
            // return new
            // {
            //     nm.IsClient,
            //     nm.IsServer,
            //     // nm.IsHost,
            //     // nm.IsListening,
            //     // nm.IsApproved,
            //     // nm.IsConnectedClient,
            //     // nm.isActiveAndEnabled,
            // };
        }

        private new void OnDisable()
        {
            CancelPendingApprovals();
            ServiceLocator.Current.Unregister<IUserRegistry>();
            base.OnDisable();
            if (m_NetworkManager != null)
            {
                this.LogDebug(() => $"-- {m_NetworkManager.IsListening}");
                m_NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                m_NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                m_NetworkManager.OnClientStarted -= OnClientStarted;
                m_NetworkManager.OnClientStopped -= OnClientStopped;
                m_NetworkManager.OnConnectionEvent -= OnConnectionEvent;
                m_NetworkManager.OnPreShutdown -= OnPreShutdown;
                // m_NetworkManager.OnReanticipate -= OnReanticipate;
                m_NetworkManager.OnServerStarted -= OnServerStarted;
                m_NetworkManager.OnServerStopped -= OnServerStopped;
                m_NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
                m_NetworkManager.OnTransportFailure -= OnTransportFailure;
            }
        }

        // Returns Error Message or null if success

        private void RegisterUserOnLogin(
            ulong clientId,
            ServerUser user,
            NetworkManager.ConnectionApprovalResponse resp
        )
        {
            if (clientId != user.ClientId)
                throw new ArgumentException(
                    $"Wrong user client id ^{user.ClientId}, expect ^{clientId} [UserId: @{user.UserId}]"
                );
            if (m_userRegistry.RegisterUser(user, out ulong? replacedClientId) is string reason)
            {
                resp.Reason = reason;
                resp.Approved = false;
                return;
            }
            if (replacedClientId.HasValue)
                m_NetworkManager.DisconnectClient(replacedClientId.Value, k_connectionReplacedReason);
            resp.Approved = true;
        }

        private void OnServerStarted()
        {
            ServerRpcRateLimiter.Clear();
            this.LogInfo(
                $"On - IsListening: {m_NetworkManager.IsListening} /{m_lobbyPrefab}/ {TraceTag(m_NetworkManager)}"
            );
            SpawnLobby();
        }

        private void SpawnLobby()
        {
            var lobbyObj = Instantiate(m_lobbyPrefab);
            var netObj = lobbyObj.GetComponent<NetworkObject>();
            netObj.Spawn();
        }

        #region ++ DEV event callbacks ++

        private void OnTransportFailure()
        {
            this.LogWarn($"On {TraceTag(m_NetworkManager)}");
        }

        private void OnSessionOwnerPromoted(ulong sessionOwnerPromoted)
        {
            this.LogDebug(() => $"On {sessionOwnerPromoted} {TraceTag(m_NetworkManager)}");
        }

        private void OnServerStopped(bool isClient)
        {
            CancelPendingApprovals();
            this.LogDebug(() => $"On isClient: {isClient} {TraceTag(m_NetworkManager)}");
        }

        private void OnPreShutdown()
        {
            CancelPendingApprovals();
            this.LogDebug("On");
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            this.LogDebug(() =>
                $"On - {new { m_NetworkManager.LocalClientId, ev_manager_LocalClientId = manager.LocalClientId, ev_data = new { data.ClientId, data.EventType, data.PeerClientIds } }}"
            );
        }

        private void OnClientStopped(bool isServer)
        {
            this.LogDebug(() => $"On isServer: {isServer}");
        }

        private void OnClientStarted()
        {
            this.LogDebug("On");
        }

        #endregion ++ DEV event callbacks ++
    }
}
