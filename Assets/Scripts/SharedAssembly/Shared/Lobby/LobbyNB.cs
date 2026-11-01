// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Lobby
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class LobbyNB : BaseNetworkBehaviour, INetworkBehaviour
    {
        new void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                InitServer();
            }
            else if (IsClient)
            {
                OnNetworkSpawnClient();
            }
            this.LogDebug(() => $"++ {this.InfoTag()}");
            base.OnNetworkSpawn();
        }

        protected override void OnNetworkPostSpawn()
        {
            this.LogDebug(() => $"++ {this.InfoTag()}");

            if (IsClient)
            {
                OnNetworkPostSpawnClient();
            }

            base.OnNetworkPostSpawn();
        }

        public override void OnNetworkDespawn()
        {
            this.LogDebug(() => $"-- {this.InfoTag()}");
            if (IsClient)
            {
                DisposeClient();
            }
            base.OnNetworkDespawn();
        }

        new void OnDestroy()
        {
            m_serverLobbyNBCtrl?.Dispose();
            base.OnDestroy();
        }
    }
}
