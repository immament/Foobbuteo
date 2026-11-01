// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Linq;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.Lobby;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    [Category("MvM")]
    public class MvMUiTests
    {
        private const int UserId = 8101;
        private const int GameId = 7601;
        private static readonly TeamKey FirstKey = new(
            SourceGame.Footstar,
            TeamType.NationalTeam,
            224,
            AgeCategory.U18
        );
        private static readonly TeamKey SecondKey = new(
            SourceGame.Footstar,
            TeamType.NationalTeam,
            225,
            AgeCategory.U18
        );

        [UnityTest]
        public IEnumerator CreationBindings_SelectTeamByIndexAndHidePlayerPosition()
        {
            var user = CreateUser();
            var api = new TestLobbyApi();
            using var controller = new NewGameUiController(api, () => true, () => user);
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var root = new VisualElement();
                root.Add(new VisualElement { name = "main-view__lobby" });
                root.Add(new Button { name = "main-view__new-game-btn" });
                AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/NewGameView.tmpl.uxml")
                    .CloneTree(root);
                window.Show();
                window.rootVisualElement.Add(root);
                controller.Bind(root);
                controller.Open();
                var vm = (NewGameVM)root.Q("new-game-modal").dataSource;
                yield return TestUtils.WaitForUi(
                    () => root.Q<DropdownField>("new-game-type").choices.Count == 2,
                    "Eligible managers must see PvP and MvM through bound choices."
                );
                root.Q<DropdownField>("new-game-type").value = "MvM";
                yield return TestUtils.WaitForUi(
                    () =>
                        vm.GameType == "MvM"
                        && root.Q("new-game-team").resolvedStyle.display == DisplayStyle.Flex
                        && root.Q("new-game-position").resolvedStyle.display == DisplayStyle.None,
                    "Choosing MvM must show teams and hide player positions."
                );
                var team = root.Q<DropdownField>("new-game-team");
                yield return TestUtils.WaitForUi(
                    () => team.choices.Count == 2,
                    "Every eligible team key must have a selectable entry."
                );
                team.index = 1;
                yield return TestUtils.WaitForUi(
                    () => vm.SelectedTeamIndex == 1,
                    "Duplicate labels must still transfer the selected team index."
                );
                vm.Submit();

                Assert.AreEqual(FoobbuteoGameType.MvM, api.Options.GameType, "The request must create MvM.");
                Assert.AreEqual(
                    SecondKey,
                    api.Options.Team,
                    "Selection must use the complete key even when two NT teams have identical display names."
                );
                Assert.AreEqual(1, api.Calls, "The bound form must send one creation request.");
            }
            finally
            {
                controller.Unbind();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TeamSelectors_CountryTag_ShowTranslatedNameAndPreserveSelectionKey()
        {
            var user = CreateUser();
            user.RefreshTeams(new[] { new ManagedTeam(FirstKey, "LANG_PORTUGAL") });
            using var creation = new NewGameVM(new TestLobbyApi(), () => true, () => user);
            using var joining = new MvMJoinVM(new TestRoomApi(), CreatePendingGame(), () => user);

            creation.Open();
            joining.Refresh();

            Assert.AreEqual(
                "Portugal U18 (NT)",
                creation.FormatTeamChoice(FirstKey.ToString()),
                "The creation selector must translate a national-team country tag."
            );
            Assert.AreEqual(
                "Portugal U18 (NT)",
                joining.FormatTeamChoice(FirstKey.ToString()),
                "The joining selector must translate a national-team country tag."
            );
            CollectionAssert.AreEqual(
                new[] { FirstKey.ToString() },
                creation.TeamChoices,
                "Creation choices must preserve the complete team key despite translated labels."
            );
            CollectionAssert.AreEqual(
                new[] { FirstKey.ToString() },
                joining.TeamChoices,
                "Joining choices must preserve the complete team key despite translated labels."
            );
        }

        [Test]
        public void UserWithoutTeams_OffersOnlyPvPAndRejectsMvMSelection()
        {
            var api = new TestLobbyApi();
            var user = new ServerUser(101, UserId, 9001, "Player", UserRoles.Player);
            using var vm = new NewGameVM(api, () => true, () => user);
            vm.Open();
            vm.GameType = "MvM";
            CollectionAssert.AreEqual(
                new[] { "PvP" },
                vm.GameTypeChoices,
                "Users without managed teams must retain only PvP creation."
            );
            Assert.AreEqual("PvP", vm.GameType, "An unavailable game type must not become selected.");
        }

        [Test]
        public void TeamEligibilityRefresh_RemovesMvMWhenTeamsAreLost()
        {
            var user = CreateUser();
            using var vm = new NewGameVM(new TestLobbyApi(), () => true, () => user);
            vm.Open();
            vm.GameType = "MvM";
            user.RefreshTeams(Array.Empty<ManagedTeam>());
            vm.RefreshAvailability();
            Assert.AreEqual("PvP", vm.GameType, "Fresh authentication without teams must remove stale MvM selection.");
            Assert.AreEqual(0, vm.TeamChoices.Count, "Lost management permission must remove stale team choices.");
        }

        [Test]
        public void ManagerPresence_ShowsTeamAndRoleWithoutPitchNumber()
        {
            var team = new TeamVM(TeamIndex.Home, UserId, "Poland U18 (NT)", null, null, ShirtColorEnum.Blue);
            var row = new UserPresenceRow
            {
                UserId = UserId,
                PlayerName = "Manager",
                IsConnected = true,
                InRoomId = GameId,
                HasGameSlot = true,
                GameSlot = GameSlot.ForManager(GameId, TeamIndex.Home),
            };
            var vm = new UserPresenceRowVM(row, false, GameId, team);
            Assert.AreEqual("", vm.ShirtNumber, "A manager must never display pitch number zero.");
            StringAssert.Contains("Manager", vm.Status, "Presence must identify the manager role.");
            StringAssert.Contains(team.Name, vm.Status, "Presence must identify the manager's team.");
        }

        [UnityTest]
        public IEnumerator JoinBindings_RetryAndReloadRetainPendingSelection()
        {
            var user = CreateUser();
            user.EnterRoom(GameId);
            var api = new TestRoomApi();
            var state = CreatePendingGame();
            using var controller = new MvMJoinUiController(api, state, () => user);
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var root = CreateJoinRoot();
                window.Show();
                window.rootVisualElement.Add(root);
                controller.Bind(root);
                var vm = (MvMJoinVM)root.Q("mvm-join-panel").dataSource;
                yield return TestUtils.WaitForUi(
                    () =>
                        root.Q<DropdownField>("mvm-join-team").choices.Count == 2
                        && root.Q<Button>("mvm-join-button").enabledSelf,
                    "An eligible spectator must be able to choose a team and join."
                );
                root.Q<DropdownField>("mvm-join-team").index = 1;
                yield return TestUtils.WaitForUi(
                    () => vm.SelectedTeamIndex == 1,
                    "The join selector must retain the chosen complete team key."
                );
                vm.Submit();
                vm.Submit();
                Assert.AreEqual(1, api.Calls, "A pending join must suppress duplicate submission.");
                Assert.AreEqual(SecondKey, api.Team, "Joining must submit the selected team identity.");
                controller.Unbind();
                root.RemoveFromHierarchy();
                var replacement = CreateJoinRoot();
                window.rootVisualElement.Add(replacement);
                controller.Bind(replacement);
                yield return TestUtils.WaitForUi(
                    () => !replacement.Q<Button>("mvm-join-button").enabledSelf,
                    "Reloading a pending form must preserve its disabled state."
                );
                api.Complete(new MvMJoinResult(false, "Away side unavailable"));
                yield return TestUtils.WaitForUi(
                    () =>
                        replacement.Q<Label>("mvm-join-error").text == "Away side unavailable"
                        && replacement.Q<Button>("mvm-join-button").enabledSelf,
                    "A failed join must show its reason and allow retry after reload."
                );
                vm.Submit();
                Assert.AreEqual(2, api.Calls, "Retry must send exactly one additional request.");
                Assert.AreEqual(SecondKey, api.Team, "Retry must retain the selected team key.");
                user.RestoreGameSlot(GameSlot.ForManager(GameId, TeamIndex.Away));
                api.Complete(new MvMJoinResult(true));
                yield return TestUtils.WaitForUi(
                    () => replacement.Q("mvm-join-panel").resolvedStyle.display == DisplayStyle.None,
                    "Successful assignment must hide the join form."
                );
            }
            finally
            {
                controller.Unbind();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator LineupModal_DragsPitchAndTableRowsPreservesViewsAndCancelsDraft()
        {
            var game = CreatePendingGame();
            var manager = new ServerUser(101, UserId + 1, 0, "Home manager", UserRoles.Player);
            manager.RestoreGameSlot(GameSlot.ForManager(GameId, TeamIndex.Home));
            manager.EnterRoom(GameId);
            var api = new TestRoomApi
            {
                SquadResult = new MvMLineupResult
                {
                    Success = true,
                    Squad = game
                        .HomeTeam.Players.Select(player => player.ToContract())
                        .Concat(
                            new[]
                            {
                                new GameDetails_PlayerContract { PlayerId = 99, Name = "Reserve" },
                            }
                        )
                        .ToArray(),
                    Lineup = game.HomeTeam.Players.Select(player => player.PlayerId).ToArray(),
                },
            };
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var shirt = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.zero);
            var team = new TeamVM(TeamIndex.Home, UserId + 1, "Home", null, shirt, ShirtColorEnum.Blue);
            using var controller = new MvMLineupUiController(
                api,
                game,
                () => manager,
                side => team,
                ClientImageCatalog.LoadForEditor()
            );
            try
            {
                window.position = new Rect(50, 50, 1200, 800);
                window.Show();
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                    .CloneTree();
                root.style.flexGrow = 1;
                TestUtils.ClearBindingsExcept(
                    root,
                    root.Q("mvm-lineup-overlay"),
                    root.Q("home-edit-lineup"),
                    root.Q("away-edit-lineup"),
                    root.Q("mobile-home-edit-lineup"),
                    root.Q("mobile-away-edit-lineup")
                );
                window.rootVisualElement.Add(root);
                controller.Bind(root);
                var button = root.Q<Button>("home-edit-lineup");
                yield return TestUtils.WaitForUi(
                    () => button.resolvedStyle.display == DisplayStyle.Flex,
                    "Only the assigned manager's lineup trigger must become visible."
                );
                ClickButton(button);
                var overlay = root.Q("mvm-lineup-overlay");
                var pitch = root.Q("mvm-lineup-pitch");
                yield return TestUtils.WaitForUi(
                    () =>
                        overlay.resolvedStyle.display == DisplayStyle.Flex
                        && !root.Q("GameScreen").enabledInHierarchy
                        && pitch.Query(className: "lineup-editor__player").ToList().Count == 11,
                    "Opening must render eleven draggable shirts from the saved squad."
                );
                Assert.IsFalse(
                    root.Q("GameScreen").enabledInHierarchy,
                    "The modal must disable background game controls."
                );
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q("away-edit-lineup").resolvedStyle.display,
                    "The opposing team's edit trigger must remain hidden."
                );
                var starting = root.Q("mvm-lineup-starting");
                ClickButton(
                    overlay
                        .Q("mvm-lineup-views")
                        .Children()
                        .OfType<Button>()
                        .Single(item => item.text == "Average only")
                );
                yield return TestUtils.WaitForUi(
                    () =>
                        starting.Q("player-table-content")[1].childCount == 5
                        && root.Q("mvm-lineup-reserves").Q("player-table-content")[1].childCount == 5,
                    "The shared average-only tab must change both lineup tables."
                );
                Assert.AreEqual(
                    2,
                    overlay.Query<Button>(className: "player-table__view").ToList().Count,
                    "The lineup editor must have one pair of view tabs shared by both lists."
                );
                ClickButton(
                    overlay.Q("mvm-lineup-views").Children().OfType<Button>().Single(item => item.text == "All skills")
                );
                yield return TestUtils.WaitForUi(
                    () =>
                        starting.Q("player-table-content")[1].childCount == 14
                        && root.Q("mvm-lineup-reserves").Q("player-table-content")[1].childCount == 14,
                    "The shared all-skills tab must restore skill columns in both lists."
                );
                ClickButton(
                    overlay
                        .Q("mvm-lineup-views")
                        .Children()
                        .OfType<Button>()
                        .Single(item => item.text == "Average only")
                );
                var goalkeeper = pitch.Q(className: "lineup-editor__player--slot-1");
                var defender = pitch.Q(className: "lineup-editor__player--slot-2");
                yield return Drag(goalkeeper, defender, preview => AssertPlayerPreview(preview, team, "1", "Player"));
                yield return TestUtils.WaitForUi(
                    () =>
                        (int)pitch.Q(className: "lineup-editor__player--slot-1").userData == 2
                        && pitch.Q(className: "lineup-editor__player--slot-1").worldBound.width > 0
                        && root.Q("mvm-lineup-reserves").Q("player-table-content")[1].worldBound.width > 0,
                    "A pitch-to-pitch drop must swap the two assignments."
                );

                var reserveRow = root.Q("mvm-lineup-reserves").Q("player-table-content")[1];
                yield return Drag(
                    reserveRow,
                    pitch.Q(className: "lineup-editor__player--slot-1"),
                    preview => AssertPlayerPreview(preview, team, "", "Reserve")
                );
                yield return TestUtils.WaitForUi(
                    () => (int)pitch.Q(className: "lineup-editor__player--slot-1").userData == 99,
                    "Dragging a reserve table row onto the pitch must replace its occupant."
                );
                Assert.AreEqual(
                    5,
                    starting.Q("player-table-content")[1].childCount,
                    "Rebuilding draggable rows must retain average-only view selection."
                );
                Assert.AreEqual(
                    2,
                    root.Q("mvm-lineup-reserves").Q("player-table-content")[1].userData,
                    "The displaced player must appear in the reserve table."
                );
                ClickButton(root.Q<Button>("mvm-lineup-cancel"));
                yield return TestUtils.WaitForUi(
                    () => overlay.resolvedStyle.display == DisplayStyle.None && root.Q("GameScreen").enabledInHierarchy,
                    "Cancel must close the modal and restore background input."
                );
                Assert.IsNull(api.SavedDesired, "Cancel must not send a lineup save.");
                ClickButton(button);
                yield return TestUtils.WaitForUi(
                    () =>
                        (int)pitch.Q(className: "lineup-editor__player--slot-1").userData == 1
                        && pitch.Q(className: "lineup-editor__player--slot-1").worldBound.width > 0,
                    "Reopening must discard canceled edits and load the authoritative XI."
                );
                team.Shirt = null;
                yield return Drag(
                    pitch.Q(className: "lineup-editor__player--slot-1"),
                    pitch.Q(className: "lineup-editor__player--slot-2"),
                    preview =>
                        Assert.AreEqual(
                            "✥",
                            preview.Q<Label>(className: "lineup-editor__shirt").text,
                            "Missing shirt assets must use a movement icon in the player preview."
                        )
                );
                ClickButton(root.Q<Button>("mvm-lineup-apply"));
                yield return TestUtils.WaitForUi(
                    () => overlay.resolvedStyle.display == DisplayStyle.None,
                    "An accepted Apply must close the editor."
                );
                Assert.AreEqual(2, api.SavedDesired[0], "Apply must submit the draft's new goalkeeper.");
                Assert.AreEqual(
                    1,
                    api.SavedExpected[0],
                    "Apply must submit the original XI as its concurrency baseline."
                );
            }
            finally
            {
                controller.Unbind();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(shirt);
            }
        }

        private static void ClickButton(Button button) =>
            typeof(Clickable)
                .GetMethod(
                    "SimulateSingleClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )
                .Invoke(button.clickable, new object[] { null, 0 });

        private static IEnumerator Drag(
            VisualElement source,
            VisualElement destination,
            Action<VisualElement> inspectPreview = null
        )
        {
            var visibleBounds = source.worldBound;
            var scroll = source.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null)
            {
                // Table rows can extend beyond the viewport when their columns overflow horizontally.
                var viewport = scroll.contentViewport.worldBound;
                visibleBounds = Rect.MinMaxRect(
                    Mathf.Max(visibleBounds.xMin, viewport.xMin),
                    Mathf.Max(visibleBounds.yMin, viewport.yMin),
                    Mathf.Min(visibleBounds.xMax, viewport.xMax),
                    Mathf.Min(visibleBounds.yMax, viewport.yMax)
                );
            }
            Assert.Greater(visibleBounds.width, 0, "The drag source must have a visible horizontal area.");
            Assert.Greater(visibleBounds.height, 0, "The drag source must have a visible vertical area.");
            using (
                var down = PointerDownEvent.GetPooled(
                    new Event
                    {
                        type = EventType.MouseDown,
                        button = 0,
                        mousePosition = visibleBounds.center,
                    }
                )
            )
                source.SendEvent(down);
            using (
                var move = PointerMoveEvent.GetPooled(
                    new Event
                    {
                        type = EventType.MouseDrag,
                        button = 0,
                        mousePosition = destination.worldBound.center,
                    }
                )
            )
                source.SendEvent(move);
            var overlay = source.GetFirstAncestorOfType<VisualElement>();
            while (overlay != null && overlay.name != "mvm-lineup-overlay")
                overlay = overlay.parent;
            var preview = overlay?.Q(className: "lineup-editor__ghost");
            Assert.IsNotNull(preview, "Moving a player must display its drag card.");
            yield return TestUtils.WaitForUi(
                () =>
                {
                    var shirt = preview.Q<Label>(className: "lineup-editor__shirt");
                    return preview.worldBound.width > 0
                        && (
                            shirt.GetBinding("style.backgroundImage") == null
                            || shirt.resolvedStyle.backgroundImage.sprite != null
                        );
                },
                "The drag card must have settled layout and bindings before inspection."
            );
            inspectPreview?.Invoke(preview);
            using (
                var up = PointerUpEvent.GetPooled(
                    new Event
                    {
                        type = EventType.MouseUp,
                        button = 0,
                        mousePosition = destination.worldBound.center,
                    }
                )
            )
                (source.panel.GetCapturingElement(up.pointerId) ?? source).SendEvent(up);
        }

        private static void AssertPlayerPreview(VisualElement preview, TeamVM team, string number, string name)
        {
            Assert.IsTrue(
                preview.ClassListContains("lineup-editor__player"),
                "The drag card must share pitch-player styling."
            );
            var shirt = preview.Q<Label>(className: "lineup-editor__shirt");
            Assert.AreEqual(number, shirt.text, "The preview must show the draft number, or no number for a reserve.");
            Assert.AreSame(
                team.Shirt,
                shirt.resolvedStyle.backgroundImage.sprite,
                "The preview must display the selected team's bound shirt asset."
            );
            Assert.AreEqual(
                team.ShirtTextColor,
                shirt.resolvedStyle.color,
                "The preview number must use the team's contrasting text colour."
            );
            Assert.AreEqual(
                name,
                preview.Q<Label>(className: "lineup-editor__name").text,
                "The player name must remain visible below the shirt."
            );
            Assert.IsTrue(
                preview.Query<VisualElement>().ToList().All(element => element.pickingMode == PickingMode.Ignore),
                "Neither the preview nor its children may intercept drop-target picking."
            );
        }

        private static VisualElement CreateJoinRoot()
        {
            var template = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();
            var panel = template.Q("mvm-join-panel");
            panel.RemoveFromHierarchy();
            var root = new VisualElement();
            root.Add(panel);
            return root;
        }

        private static ClientGameState CreatePendingGame()
        {
            var homeKey = new TeamKey(SourceGame.Footstar, TeamType.Club, 777);
            var info = new BaseGameInfo(
                new FixedGameInfoNetState(GameId, FoobbuteoGameType.MvM, 30, DateTimeOffset.UtcNow, 0),
                new TurnStageNetState(FoobbuteoGameStatus.Pending, 0, TurnStageEnum.Pending, 0, 0),
                new NetFoobbuteoTeam
                {
                    Id = UserId + 1,
                    Name = "Home",
                    Team = homeKey,
                },
                new NetFoobbuteoTeam { Id = 0, Name = "Waiting for opponent" }
            );
            var state = ClientGameState.FromContract(info);
            GameDetails_PlayerContract[] Players(TeamIndex side) =>
                Enumerable
                    .Range(1, 11)
                    .Select(index => new GameDetails_PlayerContract
                    {
                        TeamIdx = side,
                        PlayerIdx = (byte)index,
                        PlayerId = index,
                        Name = "Player",
                    })
                    .ToArray();
            state.LoadDetails(
                new GameDetailsContract(
                    GameId,
                    Vector2.zero,
                    new GameDetails_TeamContract(UserId + 1, Players(TeamIndex.Home), 0, homeKey),
                    new GameDetails_TeamContract(0, Players(TeamIndex.Away), 0)
                )
            );
            return state;
        }

        private static ServerUser CreateUser() =>
            new(
                101,
                UserId,
                0,
                "Manager",
                UserRoles.Player,
                teams: new[] { new ManagedTeam(FirstKey, "Poland"), new ManagedTeam(SecondKey, "Poland") }
            );

        private sealed class TestLobbyApi : ILobbyApiClient
        {
            internal NewGameOptions Options { get; private set; }
            internal int Calls { get; private set; }
            public event Action<ServerInfoContract> ServerInfoUpdated
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<string> AlertReceived
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<NewGameResult> NewGameCompleted
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }

            public void NewGame(NewGameOptions options)
            {
                Options = options;
                Calls++;
            }

            public void SendGameJoinRequest(int gameId) { }

            public void RequestServerInfo() { }

            public void DeleteGame(int gameId) { }

            public void Dispose() { }
        }

        private sealed class TestRoomApi : IClientGameRoomNB
        {
            public event Action<MvMLineupResult> MvMLineupLoaded;
            public event Action<MvMLineupResult> MvMLineupSaved;
            public event Action<DateTimeOffset> StartTimeChanged
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            internal MvMLineupResult SquadResult;
            internal int[] SavedExpected;
            internal int[] SavedDesired;

            public void LoadMvMLineup() => MvMLineupLoaded?.Invoke(SquadResult);

            public void SaveMvMLineup(int[] expected, int[] desired)
            {
                SavedExpected = expected;
                SavedDesired = desired;
                MvMLineupSaved?.Invoke(new MvMLineupResult { Success = true });
            }

            public bool IsHost => false;
            public bool IsSpawned => true;
            internal TeamKey Team { get; private set; }
            internal int Calls { get; private set; }
            public event Action<MvMJoinResult> MvMJoinCompleted;
            public event Action<TurnStateChangedMessage> TurnStarted
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<TurnStateChangedMessage> ProcessingStarted
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<TurnResultReadyMessage> TurnResultReady
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<GameDetailsContract> GameDetailsReady
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<PlayerCommandResponse> CommandResponseReceived
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<GameDetails_PlayerContract> PlayerControlClaimed
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<GameDetails_PlayerContract> PlayerControlStateChanged
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<TeamIndex, NetFoobbuteoTeam> TeamInfoChanged
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }
            public event Action<TurnStateChangedMessage> GameEnded
            {
                add
                { /* This test stub intentionally ignores subscriptions.*/
                }
                remove
                { /*// This test stub does not retain handlers.*/
                }
            }

            public void JoinMvMGame(TeamKey team)
            {
                Team = team;
                Calls++;
            }

            internal void Complete(MvMJoinResult result) => MvMJoinCompleted?.Invoke(result);

            public void InitClient() { }

            public void UserJoinedRoom() { }

            public void RequestLeaveGame() { }

            public BaseGameInfo GetBaseGameInfo() => default;

            public void SendMoveCmd(PlayerCommand cmd) { }

            public void SendModeCmd(PlayerModeCommand cmd) { }

            public void RequestPlayerControl(PlayerKey key) { }

            public void Dispose() { }
        }
    }
}
