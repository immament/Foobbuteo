// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Imma.Foobbuteo.Common.dev
{
    /// <summary>
    /// Connection Approval Handler Component
    /// </summary>
    /// <remarks>
    /// This should be placed on the same GameObject as the NetworkManager.
    /// It automatically declines the client connection for example purposes.
    /// </remarks>
    public class DevNetworkGUI : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager m_NetworkManager;

        // #if UNITY_EDITOR

        private GUIStyle m_buttonStyle;

        private void Start()
        {
            // m_NetworkManager = NetworkManager.Singleton;
            m_buttonStyle = null;
        }

        private void OnGUI()
        {
            m_buttonStyle ??= new GUIStyle(GUI.skin.button);
            m_buttonStyle.fontSize = Mathf.Clamp(Screen.height / 20, 15, 30);
            var width = Mathf.Clamp(Screen.width - 320f, 50, Screen.width / 4);
            var height = Mathf.Clamp((m_buttonStyle.fontSize + 10) * 5, 50, Screen.height / 2.5f);
            GUILayout.BeginArea(new Rect(Screen.width - width - 30, Screen.height - height - 30, width, height));
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                StartButtons();
            }
            else
            {
                StatusLabels();
                // SubmitNewPosition();
            }
            GUILayout.EndArea();
        }

        private void StartButtons()
        {
            if (GUILayout.Button("Client", m_buttonStyle))
            {
                AppCtx.App.StartClient();
            }
            if (GUILayout.Button("Server", m_buttonStyle))
            {
                AppCtx.App.StartServer();
            }
            if (GUILayout.Button("Host", m_buttonStyle))
            {
                AppCtx.App.StartHost();
            }
        }

        private void StatusLabels()
        {
            var mode =
                m_NetworkManager.IsHost ? "Host"
                : m_NetworkManager.IsServer ? "Server"
                : "Client";

            GUILayout.Label("Transport: " + m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
            GUILayout.Label("Mode: " + mode);
            if (GUILayout.Button($"Stop {mode}", m_buttonStyle))
            {
                // m_NetworkManager.Shutdown();
                switch (mode)
                {
                    case "Client":
                        AppCtx.Client.DevDisconnect();
                        break;
                    default:
                        AppCtx.Server.DevShutdown();
                        break;
                }
            }
            if (GUILayout.Button($"Open Dev Scene", m_buttonStyle))
            {
                SceneManager.LoadScene("DevScene");
            }
            if (GUILayout.Button($"Open Lobby Scene", m_buttonStyle))
            {
                SceneManager.LoadScene("LobbyScene");
            }
        }
        // #endif
    }
}
