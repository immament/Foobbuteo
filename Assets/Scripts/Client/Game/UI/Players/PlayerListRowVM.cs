// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Globalization;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerListRowVM
    {
        private const int k_displayScale = 5;
        private const string k_notApplicable = "—";

        public PlayerListRowVM(FoobbuteoPlayerState player, byte? shirtNumber, Texture2D flag)
        {
            PlayerId = player.PlayerId;
            IsUser = player.IsUser;
            ShirtNumber = shirtNumber;
            Position = PlayerPositionLabels.Get(shirtNumber);
            Number = shirtNumber?.ToString(CultureInfo.InvariantCulture) ?? "";
            Name = player.Name;
            Flag = flag;
            int move = player.MoveSkill * k_displayScale;
            int confrontation = player.ConfrontationSkill * k_displayScale;
            int lowPass = (player.IsGk ? player.GkPassSkill : player.LowPassSkill) * k_displayScale;
            int highPass = (player.IsGk ? player.GkPassSkill : player.LongPassSkill) * k_displayScale;
            Move = Format(move);
            Confrontation = Format(confrontation);
            LowPass = Format(lowPass);
            HighPass = Format(highPass);
            int total = move + confrontation + lowPass + highPass;
            if (player.IsGk)
            {
                int saves = player.GkSavesSkill * k_displayScale;
                int outOfArea = player.OutOfAreaSkill * k_displayScale;
                Saves = Format(saves);
                OutOfArea = Format(outOfArea);
                Crosses = Shooting = LongShots = k_notApplicable;
                AverageValue = (total + saves + outOfArea) / 6f;
            }
            else
            {
                int crosses = player.CrossesSkill * k_displayScale;
                int shooting = player.ShootingSkill * k_displayScale;
                int longShots = player.LongShotsSkill * k_displayScale;
                Crosses = Format(crosses);
                Shooting = Format(shooting);
                LongShots = Format(longShots);
                Saves = OutOfArea = k_notApplicable;
                AverageValue = (total + crosses + shooting + longShots) / 7f;
            }
            Average = AverageValue.ToString("F1", CultureInfo.InvariantCulture);
        }

        public int PlayerId { get; }
        public bool IsUser { get; }
        public byte? ShirtNumber { get; }
        public float AverageValue { get; }

        [CreateProperty]
        public string Number { get; }

        [CreateProperty]
        public string Position { get; }

        [CreateProperty]
        public string Name { get; }

        [CreateProperty]
        public Texture2D Flag { get; }

        [CreateProperty]
        public string Average { get; }

        [CreateProperty]
        public string Move { get; }

        [CreateProperty]
        public string Confrontation { get; }

        [CreateProperty]
        public string LowPass { get; }

        [CreateProperty]
        public string HighPass { get; }

        [CreateProperty]
        public string Crosses { get; }

        [CreateProperty]
        public string Shooting { get; }

        [CreateProperty]
        public string LongShots { get; }

        [CreateProperty]
        public string Saves { get; }

        [CreateProperty]
        public string OutOfArea { get; }

        private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
