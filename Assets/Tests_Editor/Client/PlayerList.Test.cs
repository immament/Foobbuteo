// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using System.Linq;
using System.Reflection;
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
    public class PlayerListTests
    {
        private const int k_gameId = 42;
        private const int k_homePlayerId = 101;
        private const int k_awayPlayerId = 201;
        private EditorWindow m_window;
        private VisualElement m_root;
        private PlayerListUiController m_controller;
        private FoobbuteoTeamState<ClientPlayerState> m_home;
        private FoobbuteoTeamState<ClientPlayerState> m_away;

        [TearDown]
        public void TearDown()
        {
            m_controller?.Dispose();
            m_controller = null;
            if (m_window != null)
            {
                m_window.Close();
                Object.DestroyImmediate(m_window);
            }
        }

        [Test]
        public void FieldPlayer_UsesSevenSuppliedSkillsAndKeepsFractionalAverage()
        {
            var row = Row(Player(TeamIndex.Home, 2, k_homePlayerId));

            Assert.AreEqual("50", row.Move, "Move should be scaled from 10 to 50.");
            Assert.AreEqual("65", row.Shooting, "Shooting must display the supplied Foobbuteo rating.");
            Assert.AreEqual("50", row.HighPass, "The field player's high pass must use LongPassSkill.");
            Assert.AreEqual(
                "52.1",
                row.Average,
                "The seven-skill average must retain one decimal without a percent sign."
            );
            Assert.AreEqual("—", row.Saves, "Goalkeeper saves must be inapplicable for a field player.");
            Assert.AreEqual(
                "—",
                row.OutOfArea,
                "Goalkeeper out-of-area skill must be inapplicable for a field player."
            );
        }

        [Test]
        public void Goalkeeper_UsesDisplayPassingAndSixSkillAverage()
        {
            var row = Row(Player(TeamIndex.Home, 1, k_homePlayerId));

            Assert.AreEqual("90", row.LowPass, "A goalkeeper must display GkPassSkill even while in save mode.");
            Assert.AreEqual("90", row.HighPass, "A goalkeeper's display high pass must use GkPassSkill.");
            Assert.AreEqual(
                "63.3",
                row.Average,
                "The goalkeeper average must include four base and two goalkeeper skills."
            );
            Assert.AreEqual("50", row.Saves, "Saves must be scaled to the 0–100 range.");
            Assert.AreEqual("50", row.OutOfArea, "Out of Area must be scaled to the 0–100 range.");
            Assert.AreEqual("—", row.Crosses, "Crosses must be inapplicable for a goalkeeper.");
            Assert.AreEqual("—", row.Shooting, "Shooting must be inapplicable for a goalkeeper.");
            Assert.AreEqual("—", row.LongShots, "Long Shots must be inapplicable for a goalkeeper.");
        }

        [Test]
        public void OptionalShirtNumber_DoesNotReplacePlayerIdentityAndSortsAfterPitchRows()
        {
            var player = Player(TeamIndex.Home, 2, k_homePlayerId);
            var bench = new PlayerListRowVM(player, null, null);
            var table = new PlayerTableVM();
            table.SetRows(new[] { bench, Row(player), Row(Player(TeamIndex.Home, 1, 102)) });

            Assert.AreEqual("", bench.Number, "A bench row must allow an absent shirt number.");
            Assert.AreEqual(k_homePlayerId, bench.PlayerId, "Player identity must be independent of pitch slots.");
            Assert.AreEqual("1", table.Rows[0].Number, "The goalkeeper must appear before higher shirt numbers.");
            Assert.AreSame(
                bench,
                table.Rows[2],
                "Unnumbered rows should follow pitch rows without inventing a number."
            );
        }

        [Test]
        public void Flags_LoadNumericPngAndAllowMissingCountries()
        {
            var flags = new CountryFlagCache(ClientImageCatalog.LoadForEditor());
            var flag = flags.Get(1);

            Assert.IsNotNull(flag, "The supplied country 1 flag must be available as a runtime texture.");
            Assert.AreSame(flag, flags.Get(1), "Repeated lookup should reuse the cached flag.");
            Assert.IsNull(flags.Get(int.MaxValue), "An unknown country must leave its flag cell empty.");
            Assert.IsNull(flags.Get(0), "A missing country ID must leave its flag cell empty.");
        }

        [UnityTest]
        public IEnumerator EveryHeaderTrigger_OpensItsOwnTeamAndCloseRestoresFocus()
        {
            CreateUi();
            foreach (string name in new[] { "TeamAColor", "pitch-home-name", "TeamBColor", "pitch-away-name" })
            {
                bool home = name == "TeamAColor" || name == "pitch-home-name";
                SendClick(m_root.Q(name));
                yield return TestUtils.WaitForUi(
                    () => IsOpen() && !m_root.Q("GameScreen").enabledInHierarchy,
                    "The team modal must be visible and disable the background for " + name
                );
                yield return TestUtils.WaitForUi(
                    () =>
                        m_root.Q<Label>("team-player-list-title").text == (home ? "Home — Players" : "Away — Players"),
                    "The title must identify the clicked team."
                );

                Assert.AreEqual(
                    home ? k_homePlayerId : k_awayPlayerId,
                    m_controller.VM.Table.Rows[0].PlayerId,
                    "The roster must belong to the clicked team trigger: " + name
                );
                Assert.AreSame(
                    PlayerTableViewDefinition.AllSkills,
                    m_controller.VM.Table.View,
                    "The initial table view must include all Foobbuteo skills."
                );
                var overlay = m_root.Q("team-player-list-overlay");
                var picked = m_root.panel.Pick(m_root.Q("team-player-list-panel").worldBound.center);
                Assert.IsTrue(
                    picked == overlay || overlay.Contains(picked),
                    "The modal must intercept pointer input above the pitch."
                );

                ClickButton(m_root.Q<Button>("team-player-list-close"));
                yield return TestUtils.WaitForUi(
                    () => !IsOpen() && m_root.Q("GameScreen").enabledInHierarchy,
                    "Close must restore the game controls."
                );
            }
            SendClick(m_root.Q("TeamAColor"));
            yield return TestUtils.WaitForUi(IsOpen, "Open the visible desktop trigger before testing focus.");
            ClickButton(m_root.Q<Button>("team-player-list-close"));
            yield return TestUtils.WaitForUi(
                () => m_root.focusController.focusedElement == m_root.Q("TeamAColor"),
                "Closing the modal must return keyboard focus to its opener."
            );
        }

        [UnityTest]
        public IEnumerator Views_KeepIdentityColumnsAndAverageEmphasisAcrossTeamsAndReloads()
        {
            CreateUi();
            SendClick(m_root.Q("TeamAColor"));
            yield return TestUtils.WaitForUi(() => IsOpen() && Rows().Count == 2, "The home roster must render.");
            yield return TestUtils.WaitForUi(
                () => Rows()[0].Q<Label>(className: "player-table__cell--average").text == "63.3",
                "The average cell must bind to its calculated row value."
            );
            Assert.AreEqual(14, Rows()[0].childCount, "All skills must include five identity and nine skill columns.");
            Assert.IsFalse(
                Rows()[0].ClassListContains("player-table__row--alternate"),
                "The first row uses the base color."
            );
            Assert.IsTrue(
                Rows()[1].ClassListContains("player-table__row--alternate"),
                "The second row uses the alternate color."
            );
            var average = Rows()[0].Q<Label>(className: "player-table__cell--average");
            Assert.AreEqual(
                FontStyle.Normal,
                average.resolvedStyle.unityFontStyleAndWeight,
                "Table values must use regular body text; the average column is emphasized by its background."
            );
            Assert.Greater(average.resolvedStyle.backgroundColor.a, 0, "The average column needs its own background.");
            Assert.IsNotNull(Rows()[0].Q<Image>().image, "A known country's flag must bind to its row image.");

            ClickView("Average only");
            yield return TestUtils.WaitForUi(
                () => Rows()[0].childCount == 5,
                "Average-only view must hide skill columns."
            );
            ClickButton(m_root.Q<Button>("team-player-list-close"));
            SendClick(m_root.Q("TeamBColor"));
            Assert.AreSame(
                PlayerTableViewDefinition.AverageOnly,
                m_controller.VM.Table.View,
                "View selection must persist when changing teams."
            );
            ReloadUi();
            yield return TestUtils.WaitForUi(
                () => IsOpen() && Rows()[0].childCount == 5,
                "UI reload must restore the selected team and average-only view."
            );
            ClickView("All skills");
            yield return TestUtils.WaitForUi(() => Rows()[0].childCount == 14, "One click must restore all skills.");
        }

        [UnityTest]
        public IEnumerator BackdropAndEscape_DismissWhilePanelClicksKeepModalOpen()
        {
            CreateUi();
            SendClick(m_root.Q("TeamAColor"));
            yield return TestUtils.WaitForUi(IsOpen, "The modal must open before dismissal checks.");
            SendClick(m_root.Q("team-player-list-title"));
            Assert.IsTrue(m_controller.VM.Team.HasValue, "Clicking inside the panel must not dismiss it.");
            SendClick(m_root.Q("modal-backdrop"));
            yield return TestUtils.WaitForUi(() => !IsOpen(), "Clicking the backdrop must dismiss the modal.");
            SendClick(m_root.Q("TeamBColor"));
            var close = m_root.Q<Button>("team-player-list-close");
            yield return TestUtils.WaitForUi(
                () => IsOpen() && m_root.focusController.focusedElement == close,
                "The reopened modal must receive keyboard focus before handling Escape."
            );
            using var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            escape.target = close;
            close.SendEvent(escape);
            yield return TestUtils.WaitForUi(() => !IsOpen(), "Escape must dismiss the modal from a child control.");
        }

        [UnityTest]
        public IEnumerator Refresh_ReplacesOpenRosterAndSupportsEmptyTeams()
        {
            CreateUi();
            SendClick(m_root.Q("TeamAColor"));
            yield return TestUtils.WaitForUi(IsOpen, "The home roster must open before updating.");
            const string replacementName = "<b>Replacement</b>";
            m_home = Team(TeamIndex.Home, Player(TeamIndex.Home, 2, 301, replacementName));
            m_controller.Refresh();
            yield return TestUtils.WaitForUi(
                () =>
                    Rows().Count == 1
                    && Rows()[0].Q<Label>(className: "player-table__cell--name").text == replacementName,
                "A state refresh must replace the currently visible roster."
            );
            Assert.IsFalse(
                Rows()[0].Q<Label>(className: "player-table__cell--name").enableRichText,
                "Player names must be displayed literally instead of interpreting markup."
            );
            m_home = null;
            m_controller.Refresh();
            Assert.AreEqual(0, m_controller.VM.Table.Rows.Count, "An unloaded team must expose an empty roster.");
            Assert.IsNotNull(
                m_root.Q("player-table-empty"),
                "An unloaded roster needs an explicit empty-state message."
            );
        }

        [UnityTest]
        public IEnumerator RepeatedBinding_DetachesOldTriggersAndDoesNotDuplicateCallbacks()
        {
            CreateUi();
            var oldTrigger = m_root.Q("TeamAColor");
            ReloadUi();
            m_controller.Bind(m_root);
            m_controller.Bind(m_root);
            SendClick(oldTrigger);
            Assert.IsFalse(m_controller.VM.Team.HasValue, "Detached UI must no longer open a player list.");
            int changes = 0;
            m_controller.VM.Table.propertyChanged += (_, _) => changes++;
            SendClick(m_root.Q("TeamAColor"));
            Assert.AreEqual(1, changes, "A single click must refresh once after repeated bindings.");
            yield return TestUtils.WaitForUi(IsOpen, "The current UI must still open normally after repeated binding.");
        }

        [UnityTest]
        public IEnumerator CompactLandscape_ScrollsWithoutLosingHeaderAlignment()
        {
            CreateUi();
            m_window.position = new Rect(50, 50, 700, 380);
            m_home = Team(
                TeamIndex.Home,
                Enumerable
                    .Range(1, 11)
                    .Select(number =>
                        Player(
                            TeamIndex.Home,
                            (byte)number,
                            k_homePlayerId + number,
                            "A very long player name that must not obscure the average column"
                        )
                    )
                    .ToArray()
            );
            SendClick(m_root.Q("TeamAColor"));
            var scroll = m_root.Q<ScrollView>("player-table-scroll");
            yield return TestUtils.WaitForUi(
                () => IsOpen() && scroll.horizontalScroller.highValue > 0 && scroll.verticalScroller.highValue > 0,
                "The wide, long table must scroll on both axes in compact landscape."
            );
            var header = m_root.Q("player-table-content")[0];
            var row = Rows()[0];
            for (int i = 0; i < row.childCount; i++)
                Assert.AreEqual(
                    header[i].worldBound.x,
                    row[i].worldBound.x,
                    0.1f,
                    "Header and player cells must align for column " + i
                );
            Assert.LessOrEqual(
                m_root.Q("team-player-list-panel").worldBound.yMax,
                m_root.Q("team-player-list-overlay").worldBound.yMax,
                "The panel must fit vertically inside the compact viewport."
            );
        }

        private void CreateUi()
        {
            m_home = Team(TeamIndex.Home, Player(TeamIndex.Home, 1, k_homePlayerId), Player(TeamIndex.Home, 2, 102));
            m_away = Team(TeamIndex.Away, Player(TeamIndex.Away, 1, k_awayPlayerId));
            m_controller = new PlayerListUiController(
                team => team == TeamIndex.Home ? m_home : m_away,
                team => team == TeamIndex.Home ? "Home" : "Away",
                ClientImageCatalog.LoadForEditor()
            );
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.position = new Rect(50, 50, 1200, 700);
            m_window.Show();
            ReloadUi();
        }

        private void ReloadUi()
        {
            m_root?.RemoveFromHierarchy();
            m_root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();
            m_root.style.flexGrow = 1;
            TestUtils.ClearBindingsExcept(m_root, m_root.Q("team-player-list-overlay"));
            m_window.rootVisualElement.Add(m_root);
            m_controller.Bind(m_root);
        }

        private bool IsOpen() => m_root.Q("team-player-list-overlay").resolvedStyle.display == DisplayStyle.Flex;

        private System.Collections.Generic.List<VisualElement> Rows() =>
            m_root.Q("player-table-content").Children().Where(element => element.userData is int).ToList();

        private void ClickView(string text) =>
            ClickButton(
                m_root.Q("player-table-views").Children().OfType<Button>().Single(button => button.text == text)
            );

        private static void SendClick(VisualElement target)
        {
            using var click = ClickEvent.GetPooled();
            click.target = target;
            target.SendEvent(click);
        }

        private static void ClickButton(Button button) =>
            typeof(Clickable)
                .GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });

        private static PlayerListRowVM Row(ClientPlayerState player) => new(player, player.Slot.PlayerIdx, null);

        private static FoobbuteoTeamState<ClientPlayerState> Team(TeamIndex team, params ClientPlayerState[] players) =>
            new(team, 1, team.ToString(), ShirtColorEnum.White, players);

        private static ClientPlayerState Player(TeamIndex team, byte number, int id, string name = "Player") =>
            ClientPlayerState.FromContract(
                k_gameId,
                new GameDetails_PlayerContract
                {
                    TeamIdx = team,
                    PlayerIdx = number,
                    PlayerId = id,
                    Name = name,
                    CountryId = 1,
                    PlayerMode = number == 1 ? PlayerModeEnum.GkSave : PlayerModeEnum.Confrontation,
                    MoveSkill = 10,
                    ConfrontationSkill = 10,
                    LowPassSkill = 10,
                    LongPassSkill = 10,
                    GkPassSkill = 18,
                    CrossesSkill = 10,
                    ShootingSkill = 13,
                    LongShotsSkill = 10,
                    GkSavesSkill = 10,
                    OutOfAreaSkill = 10,
                }
            );
    }
}
