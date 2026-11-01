// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.NetState;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class GameRoomNB : BaseNetworkBehaviour
    {
        // Unchanged Game data
        private NetworkVariable<FixedGameInfoNetState> m_nv_fixedGameInfo;
        public int GameId => m_nv_fixedGameInfo.Value.GameId;

        private NetworkVariable<TurnStageNetState> m_nv_turnStage;
        private NetworkVariable<NetFoobbuteoTeam> m_nv_HomeTeam;
        private NetworkVariable<NetFoobbuteoTeam> m_nv_AwayTeam;

        private IToClientRpc_GameRoomNB ToClient => this;

        private IToServerRpc_GameRoomNB ToServer => this;

        new void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            InitClientVars();
        }

        new void OnDestroy()
        {
            // m_server?.Dispose();
            Dispose();
        }

        public void Dispose()
        {
            ClientDispose();
            ServerDispose();
        }

        public override void OnNetworkSpawn()
        {
            this.LogInfo($"{this.InfoTag()}, gameId: {GameId}");
            if (IsClient && !IsHost)
            {
                gameObject.name = $"GameRoom_{GameId}";
                ((IClientGameRoomNB)this).InitClient();
                AppCtx.Client.OpenGameScene(GameId);
            }
            if (IsServer)
            {
                StartServerGame();
            }

            base.OnNetworkSpawn();
        }

        protected override void OnNetworkPostSpawn()
        {
            if (IsClient && !IsHost)
            {
                ServiceLocator.Current.Get<NetworkObjectRegistry>().SetActive<IClientGameRoomNB>(this);
            }
            base.OnNetworkPostSpawn();
        }

        public override void OnNetworkDespawn()
        {
            m_clientDetailsReady = false;
            if (IsClient && !IsHost)
            {
                if (
                    ServiceLocator.Current.Get<NetworkObjectRegistry>().Unregister<IClientGameRoomNB>(this)
                    && NetworkManager.IsConnectedClient
                    && !NetworkManager.ShutdownInProgress
                )
                    AppCtx.Client.OpenLobbyScene();
            }
            if (IsServer)
            {
                this.ServerDispose();
            }
            base.OnNetworkDespawn();
        }
    }
}
