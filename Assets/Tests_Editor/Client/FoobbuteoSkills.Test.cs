// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using System.Reflection;
using Imma.Foobbuteo.Client.Game;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEngine;

namespace Tests.Editor.Client
{
    [Category("FoobbuteoSkills")]
    public class FoobbuteoSkillsTests
    {
        [Test]
        public void ClientLoadingAndClaimUpdate_CopyEveryIndependentRating()
        {
            var expected = FoobbuteoSkillTestData.Player();
            var player = ClientPlayerState.FromContract(FoobbuteoSkillTestData.GameId, expected);
            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(player.ToContract()),
                "Initial client snapshots must retain all ten supplied ratings."
            );

            player = ClientPlayerState.FromContract(
                FoobbuteoSkillTestData.GameId,
                new GameDetails_PlayerContract { PlayerIdx = 2 }
            );
            player.OnPlayerControlClaimed(expected);

            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(player.ToContract()),
                "Claim updates must replace all ten ratings on an existing player."
            );
        }

        [Test]
        public void LineupSwap_MovingReserveToGoalkeeper_PreservesAllPlayerRatings()
        {
            var expected = FoobbuteoSkillTestData.Player();
            expected.PlayerId = 112;
            var squad = Enumerable
                .Range(1, 11)
                .Select(index => new GameDetails_PlayerContract { PlayerId = 100 + index })
                .Append(expected)
                .ToArray();
            var draft = new MvMLineupDraft(squad, Enumerable.Range(101, 11).ToArray());

            Assert.IsTrue(draft.Swap(112, 101), "A reserve must be assignable to the goalkeeper slot.");
            var assigned = draft.GetPlayer(112, TeamIndex.Home);

            CollectionAssert.AreEqual(
                FoobbuteoSkillTestData.Skills(expected),
                FoobbuteoSkillTestData.Skills(assigned),
                "Every rating must follow player identity when its role changes."
            );
            Assert.AreEqual(
                PlayerModeEnum.GkSave,
                assigned.PlayerMode,
                "The destination slot must still determine goalkeeper mode."
            );
        }

        [TestCase((byte)2, "20", "25", "27.1")]
        [TestCase((byte)1, "30", "30", "31.7")]
        public void TableAndTooltip_DisplayStoredPassingRegardlessOfCrossZone(
            byte slot,
            string lowPass,
            string highPass,
            string average
        )
        {
            var contract = FoobbuteoSkillTestData.Player(slot);
            contract.Position = new Vector2(550, 40);
            var player = ClientPlayerState.FromContract(FoobbuteoSkillTestData.GameId, contract);
            var row = new PlayerListRowVM(player, slot, null);

            Assert.AreEqual(lowPass, row.LowPass, "Displayed low passing must use the role's stored rating.");
            Assert.AreEqual(
                highPass,
                row.HighPass,
                "Displayed high passing must stay independent of the current cross zone."
            );
            Assert.AreEqual(
                average,
                row.Average,
                "Role-specific averages must retain the existing scaling and skill count."
            );

            var gameObject = new GameObject("Skill tooltip test");
            try
            {
                var component = gameObject.AddComponent<PlayerMB>();
                var team = new TeamVM(TeamIndex.Home, 1, "Home", null, null, ShirtColorEnum.White);
                typeof(PlayerMB)
                    .GetField("m_playerState", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(component, player);
                typeof(PlayerMB)
                    .GetField("m_playerVM", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(component, new PlayerVM(slot, team, player));
                var tooltip = component.GetTooltip().Description;

                StringAssert.Contains(
                    "Low Pass: " + lowPass + "%",
                    tooltip,
                    "The tooltip must show the same low-pass rating as the table."
                );
                StringAssert.Contains(
                    "High Pass: " + highPass + "%",
                    tooltip,
                    "The tooltip must show the same high-pass rating as the table."
                );
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
