// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    [Serializable]
    public struct TeamKey : INetworkSerializable, IEquatable<TeamKey>
    {
        public SourceGame SourceGame;
        public TeamType TeamType;
        public int TeamId;
        public AgeCategory AgeCategory;

        public TeamKey(SourceGame sourceGame, TeamType teamType, int teamId, AgeCategory ageCategory = AgeCategory.Main)
        {
            SourceGame = sourceGame;
            TeamType = teamType;
            TeamId = teamId;
            AgeCategory = ageCategory;
        }

        public bool IsSupported =>
            SourceGame == SourceGame.Footstar
            && TeamId > 0
            && (
                TeamType switch
                {
                    TeamType.Club => AgeCategory is AgeCategory.Main or AgeCategory.U19,
                    TeamType.NationalTeam or TeamType.CityTeam => AgeCategory
                        is AgeCategory.Main
                            or AgeCategory.U21
                            or AgeCategory.U18,
                    _ => false,
                }
            );

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref SourceGame);
            serializer.SerializeValue(ref TeamType);
            serializer.SerializeValue(ref TeamId);
            serializer.SerializeValue(ref AgeCategory);
        }

        public bool Equals(TeamKey other) =>
            SourceGame == other.SourceGame
            && TeamType == other.TeamType
            && TeamId == other.TeamId
            && AgeCategory == other.AgeCategory;

        public override bool Equals(object obj) => obj is TeamKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SourceGame, TeamType, TeamId, AgeCategory);

        public override string ToString() => $"{(int)SourceGame}:{(int)TeamType}:{TeamId}:{(int)AgeCategory}";

        public static bool operator ==(TeamKey left, TeamKey right) => left.Equals(right);

        public static bool operator !=(TeamKey left, TeamKey right) => !left.Equals(right);
    }
}
