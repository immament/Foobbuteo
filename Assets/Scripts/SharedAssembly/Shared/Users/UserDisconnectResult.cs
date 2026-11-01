// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Users
{
    public struct UserDisconnectResult : INetworkSerializable
    {
        public ulong RequestId;
        public string Error;

        public UserDisconnectResult(ulong requestId, string error = "")
        {
            RequestId = requestId;
            Error = error ?? "";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref RequestId);
            serializer.SerializeValue(ref Error);
        }
    }
}
