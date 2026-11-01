// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct NetFoobbuteoTeam : INetworkSerializable, IEquatable<NetFoobbuteoTeam>
    {
        public int Id;
        public string Name;
        public ShirtColorEnum Color;
        public TeamKey Team;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Color);
            Team.NetworkSerialize(serializer);
        }

        public bool Equals(NetFoobbuteoTeam other)
        {
            return Id == other.Id && Name == other.Name && Color == other.Color && Team == other.Team;
        }
    }
}
