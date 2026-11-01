// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct MatchCommentaryCue : INetworkSerializable
    {
        public MatchCommentaryKind Kind;
        public PlayerKey Player;
        public int EventIndex;
        public bool AfterEvent;
        public int Zone;
        public int Teammates;
        public int Opponents;

        public MatchCommentaryCue(
            MatchCommentaryKind kind,
            PlayerKey player,
            int eventIndex,
            bool afterEvent = false,
            int zone = 0,
            int teammates = 0,
            int opponents = 0
        )
        {
            Kind = kind;
            Player = player;
            EventIndex = eventIndex;
            AfterEvent = afterEvent;
            Zone = zone;
            Teammates = teammates;
            Opponents = opponents;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            s.SerializeValue(ref Kind);
            Player.NetworkSerialize(s);
            s.SerializeValue(ref EventIndex);
            s.SerializeValue(ref AfterEvent);
            s.SerializeValue(ref Zone);
            s.SerializeValue(ref Teammates);
            s.SerializeValue(ref Opponents);
        }
    }
}
