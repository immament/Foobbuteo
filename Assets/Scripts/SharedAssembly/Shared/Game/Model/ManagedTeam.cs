// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Localization;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    [Serializable]
    public struct ManagedTeam : INetworkSerializable
    {
        public TeamKey Key;
        public string Name;

        public ManagedTeam(TeamKey key, string name)
        {
            Key = key;
            Name = name;
        }

        public string DisplayName
        {
            get
            {
                string name = Key.TeamType == TeamType.NationalTeam ? CountryNameTranslator.Translate(Name) : Name;
                string age = Key.AgeCategory == AgeCategory.Main ? "" : $" U{(int)Key.AgeCategory}";
                string suffix = Key.TeamType switch
                {
                    TeamType.NationalTeam => "NT",
                    TeamType.CityTeam => "CT",
                    _ => Key.TeamId.ToString(),
                };
                return $"{name}{age} ({suffix})";
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            Key.NetworkSerialize(serializer);
            serializer.SerializeValue(ref Name);
        }
    }
}
