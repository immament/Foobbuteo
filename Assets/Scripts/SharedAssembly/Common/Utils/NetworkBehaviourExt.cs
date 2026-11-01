// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Common.Utils
{
    public static class NetworkBehaviourExt
    {
        public static string InfoTag(this NetworkBehaviour nb)
        {
            var clientId = nb.NetworkManager == null ? "null" : nb.NetworkManager.LocalClientId.ToString();
            string role =
                nb.IsHost ? "Host"
                : nb.IsServer ? "Server"
                : nb.IsClient ? "Client"
                : "None";

            return $"({role}, {nb.name}, ^{clientId})";
        }
    }
}
