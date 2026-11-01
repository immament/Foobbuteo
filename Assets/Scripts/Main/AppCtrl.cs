// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Common;
using Unity.Multiplayer;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR || !UNITY_SERVER
using Imma.Foobbuteo.Client;
#endif

#if UNITY_EDITOR || (!UNITY_WEBGL && !UNITY_ANDROID)
using Imma.Foobbuteo.Server;
#endif

namespace Imma.Foobbuteo.Core
{
    // Controllers are assigned in the scene; RequireComponent would prevent
    // multiplayer-role stripping from removing the opposite role in Play Mode.
    internal class AppCtrl : BaseMonoBehaviour, IAppCtrl
    {
        [SerializeField]
        private NetworkManager m_NetworkManager;

        new void Awake()
        {
            base.Awake();
            if (AppCtx.App != null)
            {
                Debug.Log("AppCtx.App!=null");
                if (!ReferenceEquals(this, AppCtx.App))
                {
                    Debug.Log("!ReferenceEquals(this, AppCtx.App)");
                    Destroy(gameObject);
                    return;
                }
                return;
            }
            Bootstrap();
            AppCtx.App = this;
            DontDestroyOnLoad(gameObject);
        }

        new void OnDestroy()
        {
            CleanUp();
        }

        new void Start()
        {
            base.Start();
            // m_NetworkManager = NetworkManager.Singleton;
            this.LogAssert(m_NetworkManager != null, "NetworkManager==null");
            if (m_NetworkManager != null)
            {
                if (m_NetworkManager.isActiveAndEnabled)
                {
                    AutoStart();
                }
            }
        }

        private void Bootstrap()
        {
            this.LogInfo($"++ v: {Application.version}");
            ServiceLocator.Initialize();
            ServiceLocator.Current.Register(new NetworkObjectRegistry());
        }

        private void CleanUp()
        {
            // ServiceLocator.Current.Unregister<UiRegistry>();
            ServiceLocator.Current.Unregister<NetworkObjectRegistry>();
            AppCtx.App = null;
            AppCtx.Server = null;
            AppCtx.Client = null;
        }

        private void AutoStart()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            StartServer();
#elif UNITY_WEBGL && !UNITY_EDITOR
            // The browser supplies connection configuration before starting the client.
            GetComponent<ClientAppCtrl>().Init();
            AppCtx.Client.StartClient();
#elif UNITY_ANDROID && !UNITY_EDITOR
            GetComponent<ClientAppCtrl>().StartAndroidClient();
#else
            var role = MultiplayerRolesManager.ActiveMultiplayerRoleMask;
            this.LogDebug(() => $"Active multiplayer role: {role}, Tags: {string.Join(",", CurrentPlayer.Tags)}");

            switch (role)
            {
                case MultiplayerRoleFlags.Server:
                    StartServer();
                    break;

                case MultiplayerRoleFlags.Client:
#if UNITY_WEBGL

                    GetComponent<ClientAppCtrl>().Init();
#else
                    StartClient();
#endif
                    break;

                case MultiplayerRoleFlags.ClientAndServer:
                    StartHost();
                    break;
            }
#endif

            //             if (CurrentPlayer.Tags.Contains("Server"))
            //             {
            //                 StartServer();
            //             }
            //             else if (CurrentPlayer.Tags.Contains("Host"))
            //             {
            //                 StartHost();
            //             }

            // #if UNITY_EDITOR
            //             if (CurrentPlayer.Tags.Contains("Client"))
            //             {
            //                 StartClient();
            //                 //
            //             }
            // #elif UNITY_WEBGL

            //             GetComponent<ClientAppCtrl>().Init();
            // #elif UNITY_SERVER
            //             this.LogInfo("UNITY_SERVER. StartServer");
            //             StartServer();
            // #endif
        }

        public void StartClient()
        {
#if UNITY_EDITOR || !UNITY_SERVER
            GetComponent<ClientAppCtrl>().Init();
            AppCtx.Client.StartClient();
#else
            throw new System.NotSupportedException("Client mode is not available in a dedicated server build.");
#endif
        }

        public void StartHost()
        {
#if UNITY_EDITOR || (!UNITY_WEBGL && !UNITY_ANDROID && !UNITY_SERVER)
            GetComponent<ServerAppCtrl>().Init();
            GetComponent<ClientAppCtrl>().Init();
            AppCtx.Server.StartHost();
#else
            throw new System.NotSupportedException("Host mode is not available in a Web or dedicated server build.");
#endif
        }

        public void StartServer()
        {
#if UNITY_EDITOR || (!UNITY_WEBGL && !UNITY_ANDROID)
            GetComponent<ServerAppCtrl>().Init();
            AppCtx.Server.StartServer();
#else
            throw new System.NotSupportedException("Server mode is not available in a Web build.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SetVersion(string version);
#endif

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetVersion(Application.version);
#endif
        }
    }
}
