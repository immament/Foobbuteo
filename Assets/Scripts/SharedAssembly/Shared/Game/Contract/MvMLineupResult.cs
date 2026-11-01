// SPDX-License-Identifier: AGPL-3.0-only
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct MvMLineupResult : INetworkSerializable
    {
        public bool Success;
        public string Error;
        public GameDetails_PlayerContract[] Squad;
        public int[] Lineup;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref Error);
            serializer.SerializeValue(ref Squad);
            serializer.SerializeValue(ref Lineup);
        }
    }
}
