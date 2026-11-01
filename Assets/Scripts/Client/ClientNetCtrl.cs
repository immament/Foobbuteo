// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Common.dev;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Imma.Foobbuteo.Client
{
    internal class ClientNetCtrl
    {
        private readonly NetworkManager m_networkManager;
        private bool m_isConnecting;
        private bool m_logoutRequested;
        private ClientConnectionConfig m_clientConfig;

        public bool IsConnecting => m_isConnecting;
        public string DisconnectReason { get; private set; } = "";
        internal bool HasUsableToken => m_clientConfig?.HasUsableToken(DateTimeOffset.UtcNow) ?? false;
        public event Action ConnectionStateChanged;

        public ClientNetCtrl(NetworkManager networkManager)
        {
            if (networkManager == null)
                throw new AppException("ClientNetCtrl::networkManager is null");
            m_networkManager = networkManager;
            RegisterNetworkEvents();
        }

        public void StartClient()
        {
            if (m_networkManager.IsListening || m_isConnecting)
                return;
            m_logoutRequested = false;
            DisconnectReason = "";
            this.LInfo($"++ Net StartClient ++++++++++++++++");
#if UNITY_EDITOR
            if (m_clientConfig == null)
            {
                SetAuthToken(DevAuthSettings.GetProfileToken(DevAuthSettings.Config.clientProfile));
            }
            else if (!HasUsableToken)
            {
                ClearAuthToken();
                ConnectionStateChanged?.Invoke();
                return;
            }
#else
            if (!HasUsableToken)
            {
                this.LError("Cannot start client before receiving valid client configuration.");
                ClearAuthToken();
                ConnectionStateChanged?.Invoke();
                return;
            }
#endif
            UnityTransport transport;
            try
            {
                transport = ConfigureUnityTransport((UnityTransport)m_networkManager.NetworkConfig.NetworkTransport);
                this.LInfo(
                    $"{new { transport.ConnectionData.Address, transport.ConnectionData.Port, transport.ConnectionData.WebSocketPath, transport.UseEncryption, transport.UseWebSockets }}"
                );
                m_isConnecting = m_networkManager.StartClient() && !m_networkManager.IsConnectedClient;
                if (!m_isConnecting && !m_networkManager.IsConnectedClient)
                    ClearAuthToken();
            }
            catch (Exception ex)
            {
                this.LWarn(ex);
                ClearAuthToken();
                m_isConnecting = false;
                ConnectionStateChanged?.Invoke();
                throw;
            }

            ConnectionStateChanged?.Invoke();
        }

        private UnityTransport ConfigureUnityTransport(UnityTransport transport)
        {
            ClientConnectionConfig config = m_clientConfig;
#if UNITY_EDITOR
            if (config == null)
            {
                this.LDebug("Use editor.settings.json connection settings (Editor only)");
                config = ClientConnectionConfig.LoadEditorConnection();
            }
#endif
            transport.UseWebSockets = true;
            transport.SetConnectionData(config.host, (ushort)config.port);
            transport.ConnectionData.WebSocketPath = config.webSocketPath;
            transport.UseEncryption = !Debug.isDebugBuild || config.useEncryption;

            if (transport.UseEncryption)
                transport.SetClientSecrets(config.host);

            return transport;
        }

        internal void ClearAuthToken()
        {
            if (m_clientConfig != null)
                m_clientConfig.token = null;
            byte[] data = m_networkManager.NetworkConfig.ConnectionData;
            if (data != null)
                Array.Clear(data, 0, data.Length);
            m_networkManager.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        public void SetClientConfig(ClientConnectionConfig config)
        {
            m_clientConfig = config;
            SetAuthToken(config.token);
        }

        private void SetAuthToken(string token)
        {
            var payload = new ConnectionPayload { token = token };
            string json = JsonUtility.ToJson(payload);
            m_networkManager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void RegisterNetworkEvents()
        {
            if (m_networkManager == null)
            {
                this.LError("Missing NetworkManager");
                return;
            }
            if (m_networkManager.IsServer && !m_networkManager.IsHost)
            {
                this.LDebug(() =>
                    $"Stop register events: {new { m_networkManager.IsHost, m_networkManager.IsServer }}"
                );
                return;
            }
            m_networkManager.OnClientConnectedCallback += OnClientConnected;
            m_networkManager.OnClientDisconnectCallback += OnClientDisconnect;
            m_networkManager.OnClientStarted += OnClientStarted;
            m_networkManager.OnClientStopped += OnClientStopped;
            m_networkManager.OnConnectionEvent += OnConnectionEvent;
            m_networkManager.OnPreShutdown += OnPreShutdown;
            // m_NetworkManager.OnServerStarted += OnServerStarted;
            // m_NetworkManager.OnServerStopped += OnServerStopped;
            // m_NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
            m_networkManager.OnTransportFailure += OnTransportFailure;
            // m_NetworkManager.OnReanticipate += OnReanticipate;
        }

        public void UnregisterNetworkEvents()
        {
            if (m_networkManager == null)
                return;

            m_networkManager.OnClientConnectedCallback -= OnClientConnected;
            m_networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            m_networkManager.OnClientStarted -= OnClientStarted;
            m_networkManager.OnClientStopped -= OnClientStopped;
            m_networkManager.OnConnectionEvent -= OnConnectionEvent;
            m_networkManager.OnPreShutdown -= OnPreShutdown;
            // m_NetworkManager.OnServerStarted -= OnServerStarted;
            // m_NetworkManager.OnServerStopped -= OnServerStopped;
            // m_NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
            m_networkManager.OnTransportFailure -= OnTransportFailure;
            // m_NetworkManager.OnReanticipate -= OnReanticipate;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != m_networkManager.LocalClientId)
                return;
            m_isConnecting = false;
            ClearAuthToken();
            ConnectionStateChanged?.Invoke();
            this.LDebug(() =>
                $"[^{clientId}/^{m_networkManager.LocalClientId}] Client connected / {TraceTag(m_networkManager)}"
            );
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (clientId != m_networkManager.LocalClientId)
                return;

            DisconnectReason = m_networkManager.DisconnectReason ?? "";
            m_isConnecting = false;
            ClearAuthToken();
            ConnectionStateChanged?.Invoke();
            if (m_logoutRequested)
            {
                this.LDebug(() => $"Logged out / {TraceTag(m_networkManager)}");
                return;
            }
            this.LWarn(
                $"[^{m_networkManager.LocalClientId}] {m_networkManager.DisconnectReason} / {TraceTag(m_networkManager)}"
            );
            if (!string.IsNullOrEmpty(DisconnectReason))
                AppCtx.Client.ShowToast(ClientDisconnectReason.Message(DisconnectReason));
        }

        private void OnTransportFailure()
        {
            if (m_networkManager.IsServer && !m_networkManager.IsHost)
                return;
            m_isConnecting = false;
            ClearAuthToken();
            ConnectionStateChanged?.Invoke();
            this.LWarn($"On / {TraceTag(m_networkManager)}, reason: {m_networkManager.DisconnectReason}");
        }

        // private void OnSessionOwnerPromoted(ulong sessionOwnerPromoted)
        // {
        //     this.LDebug($"On {sessionOwnerPromoted} / {TraceTag(m_NetworkManager)}");
        // }

        private void OnPreShutdown()
        {
            if (m_networkManager.IsServer && !m_networkManager.IsHost)
                return;
            ClearAuthToken();
            this.LDebug(() => $"On / {TraceTag(m_networkManager)}");
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (m_networkManager.IsServer)
                return;
            this.LDebug(() => $"On: {new { data.ClientId, data.EventType }} / {TraceTag(m_networkManager)}");
            if (manager != m_networkManager)
            {
                this.LDebug("manager!=m_NetworkManager");
            }
        }

        private void OnClientStopped(bool isServer)
        {
            m_isConnecting = false;
            ClearAuthToken();
            ConnectionStateChanged?.Invoke();
            this.LDebug(() => $"On isServer: {isServer} / {TraceTag(m_networkManager)}");
        }

        private void OnClientStarted()
        {
            if (m_networkManager.IsServer)
                return;
            this.LDebug(() => $"On / {TraceTag(m_networkManager)}");
        }

        private object TraceTag(NetworkManager nm)
        {
            return new
            {
                // nm.IsClient,
                // nm.IsServer,
                nm.LocalClientId,
                // nm.ConnectedHostname,
                // // nm.DAHost,
                nm.IsApproved,
                // nm.IsConnectedClient,
                // nm.IsHost,
                // nm.IsListening,
                // nm.isActiveAndEnabled,
            };
        }

        internal void Logout()
        {
            m_logoutRequested = true;
            this.ClearAuthToken();
            m_networkManager.Shutdown();
        }
    }
}
