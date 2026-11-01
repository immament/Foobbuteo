// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Linq;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class MvMLineupTests
    {
        private const int GameId = 42;
        private const int FirstReserveId = 112;
        private const int SecondReserveId = 113;

        private static GameDetails_PlayerContract[] Squad() =>
            Enumerable
                .Range(1, 13)
                .Select(index => new GameDetails_PlayerContract
                {
                    PlayerId = 100 + index,
                    Name = "Player " + index,
                    CountryId = 0,
                    MoveSkill = 10,
                    ConfrontationSkill = index == 1 ? 0 : 5,
                    LowPassSkill = 3,
                    LongPassSkill = 3,
                    GkPassSkill = 18,
                    CrossesSkill = 3,
                    ShootingSkill = 3,
                    LongShotsSkill = 3,
                    GkSavesSkill = 18,
                    OutOfAreaSkill = 0,
                })
                .ToArray();

        private static int[] Lineup() => Squad().Take(11).Select(player => player.PlayerId).ToArray();

        private static MvMLineupResult Loaded() =>
            new()
            {
                Success = true,
                Squad = Squad(),
                Lineup = Lineup(),
            };

        [TestCase(101, 102)]
        [TestCase(FirstReserveId, 103)]
        [TestCase(104, FirstReserveId)]
        [TestCase(FirstReserveId, SecondReserveId)]
        public void Draft_SwapsEachPairAndPreservesExactlyElevenStarters(int source, int destination)
        {
            var draft = new MvMLineupDraft(Squad(), Lineup());
            var before = draft.Starting.Concat(draft.Reserves).ToArray();
            int sourceIndex = Array.IndexOf(before, source);
            int targetIndex = Array.IndexOf(before, destination);
            Assert.IsTrue(draft.Swap(source, destination), "Two distinct squad players must exchange assignments.");
            var after = draft.Starting.Concat(draft.Reserves).ToArray();
            Assert.AreEqual(destination, after[sourceIndex], "The target player must occupy the source assignment.");
            Assert.AreEqual(source, after[targetIndex], "The source player must occupy the target assignment.");
            Assert.AreEqual(11, draft.Starting.Length, "Every exchange must leave eleven starting players.");
            CollectionAssert.AreEquivalent(before, after, "A swap must neither lose nor duplicate a player.");
            CollectionAssert.AreEqual(
                Lineup(),
                draft.Original,
                "Editing must retain the baseline for stale-save detection."
            );
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(12)]
        public void Validation_RejectsIncorrectStartingCount(int count)
        {
            var ids = Squad().Select(player => player.PlayerId).ToArray();
            Assert.IsNotNull(
                MvMLineupRules.Validate(ids.Take(count).ToArray(), ids),
                "Only exactly eleven starting assignments may be submitted."
            );
        }

        [Test]
        public void Validation_RejectsDuplicateAndMissingSquadPlayers()
        {
            var ids = Squad().Select(player => player.PlayerId).ToArray();
            var lineup = Lineup();
            lineup[2] = lineup[1];
            StringAssert.Contains(
                "Position 3",
                MvMLineupRules.Validate(lineup, ids),
                "A duplicate must identify its repeated assignment."
            );
            lineup[2] = -1;
            StringAssert.Contains(
                "position 3",
                MvMLineupRules.Validate(lineup, ids),
                "A missing squad ID must identify its assignment."
            );
        }

        [Test]
        public void Draft_InvalidDropsDoNotChangeAssignments()
        {
            var draft = new MvMLineupDraft(Squad(), Lineup());
            Assert.IsFalse(draft.Swap(101, 101), "Dropping onto the same player must be a no-op.");
            Assert.IsFalse(draft.Swap(101, -1), "Dropping outside the squad must be a no-op.");
            CollectionAssert.AreEqual(Lineup(), draft.Starting, "Invalid drops must preserve the XI.");
        }

        [Test]
        public void ViewModel_SwapUpdatesBothTablesAndPreservesTheirSelectedViews()
        {
            var vm = new MvMLineupVM(GameId, ClientImageCatalog.LoadForEditor());
            vm.SetTeam(TeamIndex.Home);
            vm.Load(Loaded());
            vm.Starting.SetView(PlayerTableViewDefinition.AverageOnly);
            vm.Reserves.SetView(PlayerTableViewDefinition.AverageOnly);
            vm.Open("Home");
            Assert.IsTrue(vm.Swap(101, FirstReserveId), "The editor must accept a starter/reserve exchange.");
            Assert.AreEqual(
                FirstReserveId,
                vm.Starting.Rows[0].PlayerId,
                "The starting table must show the promoted reserve."
            );
            Assert.AreEqual(
                "GK",
                vm.Starting.Rows[0].Position,
                "The promoted player must display the destination position."
            );
            Assert.AreEqual(
                "90",
                vm.Starting.Rows[0].Saves,
                "The goalkeeper row must recalculate role-dependent skills."
            );
            Assert.AreEqual(101, vm.Reserves.Rows[0].PlayerId, "The reserve row must show the displaced starter.");
            Assert.AreEqual(string.Empty, vm.Reserves.Rows[0].Position, "Unassigned players must display Reserve.");
            Assert.AreSame(
                PlayerTableViewDefinition.AverageOnly,
                vm.Starting.View,
                "A swap must retain the starting table view."
            );
            Assert.AreSame(
                PlayerTableViewDefinition.AverageOnly,
                vm.Reserves.View,
                "A swap must retain the reserve table view."
            );
            vm.Close();
            Assert.IsNull(vm.Draft, "Cancel must discard the entire unsaved draft.");
            Assert.IsFalse(vm.Swap(101, 102), "A closed editor must reject edits.");
        }

        [Test]
        public void ViewModel_SaveFailureRetainsDraftAndDisablesChangesWhileBusy()
        {
            var vm = new MvMLineupVM(GameId, ClientImageCatalog.LoadForEditor());
            vm.SetTeam(TeamIndex.Home);
            vm.Load(Loaded());
            vm.Open("Home");
            vm.Swap(101, FirstReserveId);
            var desired = vm.Draft.Starting;
            vm.SetBusy();
            Assert.IsFalse(vm.Swap(102, 103), "Saving must block further edits.");
            Assert.IsFalse(vm.CanApply, "Saving must block duplicate Apply requests.");
            vm.SaveCompleted(new MvMLineupResult { Error = "Offline" });
            Assert.AreEqual("Offline", vm.Error, "Save errors must remain visible in the editor.");
            CollectionAssert.AreEqual(desired, vm.Draft.Starting, "Save failure must preserve the manager's draft.");
            Assert.IsTrue(vm.CanApply, "A failed save must allow a retry.");
            vm.SaveCompleted(new MvMLineupResult { Success = true });
            Assert.AreEqual(DisplayStyle.None, vm.Display, "An accepted save must close the modal.");
        }

        [TestCase(1, "GK")]
        [TestCase(5, "DL")]
        [TestCase(6, "MC")]
        [TestCase(11, "ML")]
        public void PositionLabels_AreSharedByCreationAndEveryTableView(byte slot, string expected)
        {
            Assert.AreEqual(
                expected,
                PlayerPositionLabels.Get(slot),
                "Slot labels must match the existing 4-4-2 mapping."
            );
            foreach (var view in PlayerTableViewDefinition.Defaults)
                Assert.IsTrue(
                    view.Columns.Any(column => column.Property == nameof(PlayerListRowVM.Position)),
                    "Position must remain visible in view " + view.Name
                );
        }

        [UnityTest]
        public IEnumerator LineupTemplate_BindsBothReusedTablesAndFitsCompactLandscape()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var starting = new PlayerTableView();
            var reserves = new PlayerTableView();
            try
            {
                window.position = new Rect(50, 50, 844, 390);
                window.Show();
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/Players/MvMLineup.tmpl.uxml")
                    .CloneTree();
                root.style.flexGrow = 1;
                window.rootVisualElement.Add(root);
                var overlay = root.Q("mvm-lineup-overlay");
                overlay.AddToClassList("lineup-editor--short");
                var vm = new MvMLineupVM(GameId, ClientImageCatalog.LoadForEditor());
                overlay.dataSource = vm;
                vm.SetTeam(TeamIndex.Home);
                vm.Load(Loaded());
                vm.Open("Home");
                starting.Bind(root.Q("mvm-lineup-starting"), vm.Starting);
                reserves.Bind(root.Q("mvm-lineup-reserves"), vm.Reserves);
                yield return TestUtils.WaitForUi(
                    () =>
                        overlay.resolvedStyle.display == DisplayStyle.Flex
                        && root.Q("mvm-lineup-starting").worldBound.height > 0,
                    "The bound lineup editor must render inside the compact viewport."
                );
                Assert.AreEqual(
                    2,
                    root.Query(name: "player-table-views").ToList().Count,
                    "Both lists must contain the existing table's view toolbar."
                );
                Assert.LessOrEqual(
                    root.Q("mvm-lineup-panel").worldBound.yMax,
                    overlay.worldBound.yMax + 1,
                    "The editor panel must fit vertically in landscape."
                );
                vm.Starting.SetView(PlayerTableViewDefinition.AverageOnly);
                vm.Swap(101, FirstReserveId);
                yield return TestUtils.WaitForUi(
                    () =>
                        root.Q("mvm-lineup-starting")
                            .Q("player-table-content")[1]
                            .Q<Label>(className: "player-table__cell--position")
                            .text == "GK",
                    "The updated average-only row must bind the current assigned position."
                );
            }
            finally
            {
                starting.Dispose();
                reserves.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
