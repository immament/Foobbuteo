// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Lobby.Contract
{
    public struct NewGameResult : INetworkSerializable
    {
        public bool Success;
        public int GameId;
        public string Error;

        public NewGameResult(bool success, int gameId = 0, string error = "")
        {
            Success = success;
            GameId = gameId;
            Error = error ?? "";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref GameId);
            serializer.SerializeValue(ref Error);
        }
    }
}
