// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct MvMJoinResult : INetworkSerializable
    {
        public bool Success;
        public string Error;

        public MvMJoinResult(bool success, string error = "")
        {
            Success = success;
            Error = error ?? "";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref Error);
        }
    }
}
