// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tests.Editor.SharedAssembly
{
    [Category("FoobbuteoSkills")]
    public class FoobbuteoSkillsTests
    {
        [Test]
        public void NetworkRoundTrip_PreservesEveryIndependentRating()
        {
            var expected = FoobbuteoSkillTestData.Player();
            using var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out GameDetails_PlayerContract actual);

            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(actual),
                "Network snapshots must preserve each of the ten distinct Foobbuteo ratings."
            );
        }

        [TestCase(2, PlayerModeEnum.Confrontation, false, 4, 5)]
        [TestCase(2, PlayerModeEnum.Confrontation, true, 4, 7)]
        [TestCase(2, PlayerModeEnum.GkFieldPlayer, true, 4, 7)]
        [TestCase(1, PlayerModeEnum.GkSave, false, 4, 5)]
        [TestCase(1, PlayerModeEnum.GkSave, true, 4, 7)]
        [TestCase(1, PlayerModeEnum.GkOutOfArea, true, 4, 7)]
        [TestCase(1, PlayerModeEnum.GkFieldPlayer, false, 6, 6)]
        [TestCase(1, PlayerModeEnum.GkFieldPlayer, true, 6, 6)]
        public void Passing_SelectsRatingByRoleModeAndZone(
            byte slot,
            PlayerModeEnum mode,
            bool crossZone,
            int lowPass,
            int highPass
        )
        {
            var contract = FoobbuteoSkillTestData.Player(slot);
            contract.PlayerMode = mode;
            contract.Position = crossZone ? new Vector2(550, 40) : new Vector2(300, 210);
            var player = new Player(contract);

            Assert.AreEqual(
                lowPass,
                player.GetEffectiveLowPassSkill(),
                "Low-pass selection must respect goalkeeper field mode."
            );
            Assert.AreEqual(
                highPass,
                player.GetEffectiveHighPassSkill(),
                "Goalkeeper passing must take precedence over crosses and long passes."
            );
            Assert.AreEqual(
                100 + lowPass * 6,
                player.MaxLowPassDistance,
                "Low-pass range must use the selected rating with the existing multiplier."
            );
            Assert.AreEqual(
                230 + highPass * 4,
                player.MaxHighPassDistance,
                "High-pass range must use the selected rating with the existing multiplier."
            );
        }

        [TestCase(129f, false, 8)]
        [TestCase(130f, false, 8)]
        [TestCase(131f, false, 9)]
        [TestCase(129f, true, 8)]
        [TestCase(130f, true, 8)]
        [TestCase(131f, true, 9)]
        public void Shooting_UsesSameRatingForCostAndResolution(float distance, bool penaltyArea, int expected)
        {
            var player = new Player(FoobbuteoSkillTestData.Player());

            Assert.AreEqual(
                expected,
                player.ShootingSkillForDistance(distance),
                "Only shots beyond the threshold use LongShotsSkill."
            );
            Assert.AreEqual(
                (10f - 0.25f * expected) * 10f,
                player.ShootCost(distance),
                "Shot cost must use the supplied distance-specific rating."
            );
            Assert.AreEqual(
                Mathf.Pow(expected + 1f, penaltyArea ? 0.7f : 0.4f),
                player.GetFinishingSkill(distance, penaltyArea),
                "Shot resolution must use the same rating and preserve the area exponent."
            );
        }

        [TestCase(PlayerModeEnum.GkSave, true, 10)]
        [TestCase(PlayerModeEnum.GkOutOfArea, true, 0)]
        [TestCase(PlayerModeEnum.GkFieldPlayer, true, 0)]
        [TestCase(PlayerModeEnum.GkSave, false, 0)]
        [TestCase(PlayerModeEnum.GkOutOfArea, false, 11)]
        [TestCase(PlayerModeEnum.GkFieldPlayer, false, 0)]
        public void GoalkeeperInterception_UsesSuppliedRatingOnlyInMatchingMode(
            PlayerModeEnum mode,
            bool goalkeeperArea,
            int expected
        )
        {
            var contract = FoobbuteoSkillTestData.Player(1);
            contract.PlayerMode = mode;
            contract.Position = goalkeeperArea ? new Vector2(10, 210) : new Vector2(80, 210);
            var player = new Player(contract);

            Assert.AreEqual(
                expected,
                player.GkInterceptSkill,
                "Interception must preserve area/mode gating while using direct goalkeeper ratings."
            );
        }

        private sealed class Player : FoobbuteoPlayerState
        {
            internal Player(GameDetails_PlayerContract contract)
                : base(new GameSlot(FoobbuteoSkillTestData.GameId, contract.TeamIdx, contract.PlayerIdx))
            {
                Position = contract.Position;
                OnPlayerControlClaimed(contract);
            }
        }
    }
}
