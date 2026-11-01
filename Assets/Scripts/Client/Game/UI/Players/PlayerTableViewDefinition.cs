// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using System.Linq;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerTableViewDefinition
    {
        public PlayerTableViewDefinition(string name, IEnumerable<PlayerTableColumn> columns)
        {
            Name = name;
            Columns = columns.ToArray();
        }

        public string Name { get; }
        public IReadOnlyList<PlayerTableColumn> Columns { get; }

        private static readonly PlayerTableColumn[] s_identityColumns =
        {
            new("#", nameof(PlayerListRowVM.Number), "number"),
            new("Pos", nameof(PlayerListRowVM.Position), "position", "Position"),
            new("", nameof(PlayerListRowVM.Flag), "flag"),
            new("Name", nameof(PlayerListRowVM.Name), "name"),
            new("AVG", nameof(PlayerListRowVM.Average), "average", "Average"),
        };

        public static readonly PlayerTableViewDefinition AllSkills = new(
            "All skills",
            s_identityColumns.Concat(
                new[]
                {
                    new PlayerTableColumn("MOV", nameof(PlayerListRowVM.Move), tooltip: "Move"),
                    new PlayerTableColumn("CON", nameof(PlayerListRowVM.Confrontation), tooltip: "Confrontation"),
                    new PlayerTableColumn("LPA", nameof(PlayerListRowVM.LowPass), tooltip: "Low Pass"),
                    new PlayerTableColumn("HPA", nameof(PlayerListRowVM.HighPass), tooltip: "High Pass"),
                    new PlayerTableColumn("CRO", nameof(PlayerListRowVM.Crosses), tooltip: "Crosses"),
                    new PlayerTableColumn("SHO", nameof(PlayerListRowVM.Shooting), tooltip: "Shooting"),
                    new PlayerTableColumn("LSH", nameof(PlayerListRowVM.LongShots), tooltip: "Long Shots"),
                    new PlayerTableColumn("SAV", nameof(PlayerListRowVM.Saves), tooltip: "Saves"),
                    new PlayerTableColumn("OOA", nameof(PlayerListRowVM.OutOfArea), tooltip: "Out of Area"),
                }
            )
        );

        public static readonly PlayerTableViewDefinition AverageOnly = new("Average only", s_identityColumns);
        public static readonly IReadOnlyList<PlayerTableViewDefinition> Defaults = new[] { AllSkills, AverageOnly };
    }
}
