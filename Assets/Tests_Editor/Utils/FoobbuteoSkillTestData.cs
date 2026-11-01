// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Tests.Editor.Utils
{
    internal static class FoobbuteoSkillTestData
    {
        internal const int GameId = 42;

        internal static GameDetails_PlayerContract Player(byte slot = 2) =>
            new()
            {
                TeamIdx = TeamIndex.Home,
                PlayerIdx = slot,
                PlayerId = 101,
                Name = "Skill test player",
                Position = new Vector2(300, 210),
                PlayerMode = slot == 1 ? PlayerModeEnum.GkSave : PlayerModeEnum.Confrontation,
                ActionPoints = 100,
                MoveSkill = 2,
                ConfrontationSkill = 3,
                LowPassSkill = 4,
                LongPassSkill = 5,
                GkPassSkill = 6,
                CrossesSkill = 7,
                ShootingSkill = 8,
                LongShotsSkill = 9,
                GkSavesSkill = 10,
                OutOfAreaSkill = 11,
            };

        internal static int[] Skills(GameDetails_PlayerContract player) =>
            new[]
            {
                player.MoveSkill,
                player.ConfrontationSkill,
                player.LowPassSkill,
                player.LongPassSkill,
                player.GkPassSkill,
                player.CrossesSkill,
                player.ShootingSkill,
                player.LongShotsSkill,
                player.GkSavesSkill,
                player.OutOfAreaSkill,
            };
    }
}
