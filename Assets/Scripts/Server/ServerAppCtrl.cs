// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.dev;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Server.dev;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Lobby;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Imma.Foobbuteo.Server
{
    [RequireComponent(typeof(UserHandler))]
    public class ServerAppCtrl : BaseMonoBehaviour, IServerAppCtrl
    {
        [SerializeField]
        private NetworkManager m_NetworkManager;

        private FoobbuteoMainServer m_mainServer;
        private bool m_isStartingHost;

        public void Init()
        {
            if (AppCtx.Server != null)
            {
                Debug.Log("AppCtx.Server!=null");
                if (!ReferenceEquals(this, AppCtx.Server))
                {
                    Debug.Log("this != (Object)AppCtx.Server");
                    Destroy(gameObject);
                }
                return;
            }
            Bootstrap();
            AppCtx.Server = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Bootstrap()
        {
            this.LInfo("++");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            RegisterRepositories();
            var users = new UserRegistry();
            m_mainServer = new FoobbuteoMainServer(users, ServerConfig.Config.GetClientRuntimeSettings());
            ServiceLocator.Current.Register<IServerLobbyNBCtrl>(m_mainServer);
            ServiceLocator.Current.Register<IUserRegistry>(users);

            // var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
            //     "Assets/Prefabs/Net/GameRoomPrefab.prefab"
            // );

            // var serialized = new UnityEditor.SerializedObject(prefab.GetComponent<Unity.Netcode.NetworkObject>());
            // UnityEngine.Debug.Log("GlobalObjectIdHash");
            // UnityEngine.Debug.Log(serialized.FindProperty("GlobalObjectIdHash").longValue);
        }

        private static void RegisterRepositories()
        {
            ServerConfig config = ServerConfig.Config;
            ImmLogger.LInfo<ServerAppCtrl>($"DataProvider: {config.GetDataProvider()}");

            switch (config.GetDataProvider())
            {
                case DataProvider.SqlServer:
                    string connectionString = config.GetSqlConnectionString();
                    int commandTimeoutSeconds = config.DataAccess.SqlCommandTimeoutSeconds;
                    ServiceLocator.Current.Register<IAuthenticationRepository>(
                        new SqlAuthenticationRepository(connectionString, commandTimeoutSeconds)
                    );
                    ServiceLocator.Current.Register<IGamesRepository>(
                        new SqlGamesRepository(connectionString, commandTimeoutSeconds)
                    );
                    ServiceLocator.Current.Register<IGamePersistenceRepository>(
                        new SqlGamePersistenceRepository(connectionString, commandTimeoutSeconds)
                    );
                    break;

                case DataProvider.Mock:
                    ServiceLocator.Current.Register<IAuthenticationRepository>(
                        new MockAuthenticationRepository(config.DataAccess.MockUsers)
                    );
                    ServiceLocator.Current.Register<IGamesRepository>(
                        new MockGamesRepository(
                            () =>
                                new DevServerMockData(config.DataAccess.LoadInitialGames).CreateStateGameList(
                                    ServiceLocator.Current.Get<IUserRegistry>()
                                ),
                            rosters: config.DataAccess.MockTeamRosters
                        )
                    );
                    ServiceLocator.Current.Register<IGamePersistenceRepository>(new MockGamePersistenceRepository());
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException();
            }
        }

        public void StartServer()
        {
            var transport = ((UnityTransport)m_NetworkManager.NetworkConfig.NetworkTransport);
            var cd = transport.ConnectionData;
            this.LogInfo(
                $"++ StartServer ++ {new { cd.ServerListenAddress, cd.Port, cd.Address, transport.UseEncryption }}"
            );
            this.LogInfo($"++ StartServer ++++++++++++++++, {NetworkManager.Singleton}");
            GetComponent<UserHandler>().enabled = true;
            if (!m_NetworkManager.StartServer())
            {
                this.LogWarn($"StartServer Problem! IsListening: {m_NetworkManager.IsListening}");
            }
        }

        public async void StartHost()
        {
            if (m_isStartingHost || m_NetworkManager.IsListening)
                return;
            m_isStartingHost = true;
            var userHandler = GetComponent<UserHandler>();
            try
            {
                await StartHostAsync(userHandler);
            }
            catch (System.Exception exception)
            {
                this.LogException(exception);
                this.LogWarn("Host startup failed during authentication or network initialization.");
            }
            finally
            {
                if (userHandler != null)
                    userHandler.ClearPreparedHostApproval();
                m_isStartingHost = false;
            }
        }

        private async System.Threading.Tasks.Task StartHostAsync(UserHandler userHandler)
        {
            var transport = ((UnityTransport)m_NetworkManager.NetworkConfig.NetworkTransport);
            var cd = transport.ConnectionData;
            this.LogInfo(
                $"++ StartHost ++ {new { cd.ServerListenAddress, cd.Port, cd.Address, transport.UseEncryption, transport.UseWebSockets }} ++"
            );
            userHandler.enabled = true;
#if UNITY_EDITOR
            SetAuthToken(DevAuthSettings.GetProfileToken(DevAuthSettings.Config.hostProfile));
            bool requiresHostAuthentication = ServerConfig.Config.GetDataProvider() != DataProvider.Mock;
#else
            bool requiresHostAuthentication = true;
#endif
            if (requiresHostAuthentication && !await userHandler.TryPrepareHostApprovalAsync())
                return;
            if (this == null || !isActiveAndEnabled || m_NetworkManager == null || m_NetworkManager.IsListening)
                return;
            if (!m_NetworkManager.StartHost())
            {
                this.LogWarn($"StartHost Problem! IsListening: {m_NetworkManager.IsListening}");
            }
        }

        public void DevShutdown()
        {
            this.LogInfo("-- Dev Shutdown -----------");
            // NGO emits no shutdown callbacks while host authentication is still awaiting SQL.
            GetComponent<UserHandler>().CancelPendingApprovals();
            ServiceLocator.Current.Unregister<IServerLobbyNBCtrl>();
            m_NetworkManager.Shutdown();
        }

        private void SetAuthToken(string token)
        {
            var payload = new ConnectionPayload { token = token };
            string json = JsonUtility.ToJson(payload);
            m_NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(json);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            if (m_mainServer == null)
                return;

            var color = Gizmos.color;
            try
            {
                m_mainServer.OnDrawGizmos();
            }
            finally
            {
                Gizmos.color = color;
            }
        }
#endif
    }
}
