// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Unity.Netcode;

namespace Imma.Foobbuteo.Common.Utils
{
    public static class NetcodeExtensions
    {
        public static void SerializeNullableDateTime<T>(
            this BufferSerializer<T> serializer,
            ref DateTime? nullableDateTime
        )
            where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                // --- WRITING TO THE NETWORK ---
                bool hasValue = nullableDateTime.HasValue;
                serializer.SerializeValue(ref hasValue);

                if (hasValue)
                {
                    DateTime value = nullableDateTime.Value;
                    serializer.SerializeValue(ref value);
                }
            }
            else if (serializer.IsReader)
            {
                // --- READING FROM THE NETWORK ---
                bool hasValue = false;
                serializer.SerializeValue(ref hasValue);

                if (hasValue)
                {
                    DateTime value = default;
                    serializer.SerializeValue(ref value);
                    nullableDateTime = value;
                }
                else
                {
                    nullableDateTime = null;
                }
            }
        }

        public static bool IsHostClientId(ulong clientId) => (NetworkManager.ServerClientId == clientId);

        public static bool IsHostClient(this NetworkManager.ConnectionApprovalRequest req) =>
            (NetworkManager.ServerClientId == req.ClientNetworkId);
    }
}
