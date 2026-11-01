// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Core
{
    public interface IServerAppCtrl
    {
        void StartServer();
        void StartHost();

        // FoobbuteoMainServer CreateFoobbuteoServer(bool debug, GameObject gamePrefab);
        void DevShutdown();
    }
}
