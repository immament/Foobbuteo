// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public class LobbyLayoutTests
    {
        private const string LongPlayerName = "Karol Kołodziej-Hajdo (@600177/135932)";
        private static readonly Vector2[] Viewports =
        {
            new(393, 839),
            new(839, 393),
            new(320, 568),
            new(1280, 720),
            new(649, 720),
            new(650, 720),
        };
        private EditorWindow m_window;
        private VisualElement m_root;
        private VisualElement m_main;
        private readonly List<string> m_warnings = new();

        [SetUp]
        public void SetUp()
        {
            m_warnings.Clear();
            Application.logMessageReceived += OnLog;
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.position = new Rect(30, 30, 1300, 950);
            m_root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                .CloneTree();
            m_main = m_root.Q("main-view");
            m_main.style.position = Position.Absolute;
        }

        [TearDown]
        public void TearDown()
        {
            m_window.Close();
            Object.DestroyImmediate(m_window);
            Application.logMessageReceived -= OnLog;
            Assert.IsEmpty(
                m_warnings,
                "Lobby layout and bindings must not produce warnings: " + string.Join("\n", m_warnings)
            );
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
                m_warnings.Add(message);
        }

        private void Show()
        {
            m_window.rootVisualElement.Add(m_root);
            m_window.Show();
        }

        private IEnumerator Resize(Vector2 size)
        {
            Assert.IsTrue(
                UiScreenLayout.TryCreate(size, true, true, out var layout),
                "The test viewport must be valid."
            );
            LobbyUiController.ApplyLayout(m_main, layout);
            m_main.style.width = size.x;
            m_main.style.height = size.y;
            // Device density can yield fractional UI units that the panel rounds to pixels.
            const float layoutRoundingTolerance = 0.5f;
            yield return TestUtils.WaitForUi(
                () =>
                    Mathf.Abs(m_main.layout.width - size.x) <= layoutRoundingTolerance
                    && Mathf.Abs(m_main.layout.height - size.y) <= layoutRoundingTolerance,
                "Lobby geometry must settle at " + size
            );
        }

        [UnityTest]
        public IEnumerator UnselectedGameRows_DeviceWidthChangesKeepMetadataBelowNames()
        {
            TestUtils.ClearBindingsExcept(m_root, m_root.Q("main-view__lobby"));
            m_root.Q("main-view__login").style.display = DisplayStyle.None;
            var games = new List<GameListItemVM>();
            foreach (var status in new[] { FoobbuteoGameStatus.Pending, FoobbuteoGameStatus.Ongoing })
                games.Add(
                    GameListItemVM.FromContract(
                        new GameInfoContract
                        {
                            GameId = 94003 - games.Count,
                            OwnerName = "Miguel Canelas 92001",
                            OpponentName = "Aleksander Kołodziej-Hajdo",
                            Status = status,
                            NumberOfTurns = 40,
                            StartDateTime = new DateTimeOffset(2026, 10, 26, 20, 55, 0, TimeSpan.Zero),
                        }
                    )
                );
            var vm = new LobbyVM(games) { PlayerDesc = LongPlayerName };
            vm.UpdateConnectionState(true, false);
            vm.UpdateSessionState(ClientSessionView.Session, "", true);
            m_main.dataSource = vm;
            var list = m_root.Q<ListView>("main-view__game-list");
            list.bindItem = (row, index) =>
            {
                row.dataSourcePath = new PropertyPath();
                row.dataSource = games[index];
            };
            list.unbindItem = (row, index) => row.dataSource = null;
            Show();
            yield return Resize(new Vector2(1280, 720));
            yield return TestUtils.WaitForUi(
                () => list.Q<Label>("list-entry__date")?.text == games[0].StartDateText,
                "The virtualized list must measure its rows before switching to a phone"
            );
            const float androidReferenceDpi = 160f;
            var punchHoleLeft = new Vector2(1080, 2340) / (440f / androidReferenceDpi);
            var punchHoleRight = new Vector2(1080, 2280) / (480f / androidReferenceDpi);
            foreach (var size in new[] { punchHoleLeft, punchHoleRight, punchHoleLeft })
            {
                yield return Resize(size);
                yield return TestUtils.WaitForUi(
                    () =>
                        list.Query<Label>("list-entry__date")
                            .ToList()
                            .Count(label => label.text == games[0].StartDateText) == games.Count,
                    "Both bound game rows must be visible at " + size
                );
                foreach (var row in list.Query<VisualElement>("list-entry").ToList())
                {
                    var names = row.Q("list-entry__names-col");
                    var metadata = row.Q(className: "lobby-game-row__metadata");
                    Assert.GreaterOrEqual(
                        metadata.worldBound.yMin,
                        names.worldBound.yMax,
                        "Metadata must remain below the team names at " + size + "; metadata=" + metadata.worldBound
                    );
                    foreach (
                        var name in new[]
                        {
                            "list-entry__id",
                            "list-entry__date",
                            "list-entry_time",
                            "list-entry__status",
                            "list-entry__turn",
                        }
                    )
                    {
                        var label = row.Q<Label>(name);
                        float textWidth = label
                            .MeasureTextSize(
                                label.text,
                                0,
                                VisualElement.MeasureMode.Undefined,
                                0,
                                VisualElement.MeasureMode.Undefined
                            )
                            .x;
                        Assert.GreaterOrEqual(
                            label.contentRect.width + 1,
                            textWidth,
                            name + " must remain readable at " + size
                        );
                        Assert.LessOrEqual(
                            label.worldBound.xMax,
                            row.worldBound.xMax + 1,
                            name + " must stay inside its row at " + size
                        );
                        Assert.LessOrEqual(
                            label.worldBound.yMax,
                            row.worldBound.yMax + 1,
                            name + " must fit vertically inside its row at " + size
                        );
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator PopulatedLobby_ResizeKeepsRowsDetailsAndActionsReachable()
        {
            TestUtils.ClearBindingsExcept(m_root, m_root.Q("main-view__lobby"));
            m_root.Q("main-view__login").style.display = DisplayStyle.None;
            var game = GameListItemVM.FromContract(
                new GameInfoContract
                {
                    GameId = 99001,
                    OwnerName = "FC HUN B with a particularly long team name",
                    OpponentName = "Waiting for opponent",
                    Status = FoobbuteoGameStatus.Pending,
                    NumberOfTurns = 30,
                    StartDateTime = new DateTimeOffset(2026, 10, 22, 22, 22, 0, TimeSpan.Zero),
                }
            );
            var vm = new LobbyVM(new List<GameListItemVM> { game })
            {
                SelectedGame = game,
                PlayerDesc = LongPlayerName,
            };
            vm.UpdateConnectionState(true, false);
            vm.UpdateSessionState(ClientSessionView.Session, "", true);
            m_main.dataSource = vm;
            var list = m_root.Q<ListView>("main-view__game-list");
            list.bindItem = (row, index) =>
            {
                row.dataSourcePath = new PropertyPath();
                row.dataSource = vm.Games[index];
            };
            list.unbindItem = (row, index) => row.dataSource = null;
            Show();
            foreach (var size in Viewports)
            {
                yield return Resize(size);
                yield return TestUtils.WaitForUi(
                    () =>
                        list.Q<Label>("list-entry__date")?.text == game.StartDateText
                        && list.Q("list-entry")?.layout.height > 0
                        && m_root.Q<ScrollView>("details-container__game-data").contentViewport.layout.height > 0,
                    "Bound game content must settle at " + size
                );
                var row = list.Q("list-entry");
                foreach (
                    var name in new[]
                    {
                        "list-entry__id",
                        "list-entry__date",
                        "list-entry_time",
                        "list-entry__status",
                        "list-entry__turn",
                    }
                )
                {
                    var label = row.Q<Label>(name);
                    float textWidth = label
                        .MeasureTextSize(
                            label.text,
                            0,
                            VisualElement.MeasureMode.Undefined,
                            0,
                            VisualElement.MeasureMode.Undefined
                        )
                        .x;
                    Assert.GreaterOrEqual(
                        label.contentRect.width + 1,
                        textWidth,
                        name + " must remain readable without clipping at " + size
                    );
                }
                var listViewport = list.Q<ScrollView>().contentViewport;
                Assert.LessOrEqual(
                    row.worldBound.yMax,
                    listViewport.worldBound.yMax + 1,
                    "At least one complete game row must be visible at " + size
                );
                Assert.LessOrEqual(
                    row.worldBound.xMax,
                    listViewport.worldBound.xMax + 1,
                    "Game rows must fit the list width at " + size
                );
                var details = m_root.Q<ScrollView>("details-container__game-data");
                Assert.LessOrEqual(
                    details.contentContainer.layout.width,
                    details.contentViewport.layout.width + 1,
                    "Selected-game details must not require horizontal scrolling at " + size
                );
                foreach (
                    var name in new[]
                    {
                        "details-container__join-btn",
                        "onlineUsersButton",
                        "profile-menu-toggle",
                        "main-view__new-game-btn",
                    }
                )
                {
                    var action = m_root.Q<Button>(name);
                    Assert.LessOrEqual(
                        action.worldBound.xMax,
                        m_main.worldBound.xMax + 1,
                        name + " must fit horizontally at " + size
                    );
                    Assert.LessOrEqual(
                        action.worldBound.yMax,
                        m_main.worldBound.yMax + 1,
                        name + " must fit vertically at " + size
                    );
                    Assert.GreaterOrEqual(action.layout.height, 36, name + " must retain its touch target at " + size);
                    Assert.AreEqual(
                        16f,
                        action.resolvedStyle.fontSize,
                        name + " must use shared typography at " + size
                    );
                }
                var left = m_root.Q("main-view__left-container");
                var right = m_root.Q("main-view__right-container");
                if (size.x < UiScreenLayout.NarrowWidth)
                    Assert.GreaterOrEqual(
                        right.worldBound.yMin,
                        left.worldBound.yMax - 1,
                        "Narrow viewports must stack details below the game list at " + size
                    );
                else
                    Assert.GreaterOrEqual(
                        right.worldBound.xMin,
                        left.worldBound.xMax - 1,
                        "Wide viewports must place details beside the game list at " + size
                    );
                Assert.AreSame(game, vm.SelectedGame, "Resizing must preserve the selected game at " + size);
            }
            vm.SelectedGame = null;
            yield return TestUtils.WaitForUi(
                () =>
                    m_root.Q("main-view__right-container").resolvedStyle.display == DisplayStyle.None
                    && m_root.Q("main-view__left-container").layout.width > m_main.layout.width * 0.9f,
                "Clearing selection must release the details space"
            );
            Assert.Greater(
                m_root.Q("main-view__left-container").layout.width,
                m_main.layout.width * 0.9f,
                "Without a selection the list must use the available width."
            );
        }

        [UnityTest]
        public IEnumerator LobbyDialogs_ResizeKeepsHeadingsAndActionsInsideViewport()
        {
            TestUtils.ClearBindingsExcept(m_root);
            m_root.Q("main-view__login").style.display = DisplayStyle.None;
            var modal = m_root.Q("new-game-modal");
            var dialog = m_root.Q(className: "new-game-dialog");
            modal.style.display = DisplayStyle.Flex;
            Show();
            foreach (var size in Viewports)
            {
                yield return Resize(size);
                var create = m_root.Q<Button>("new-game-create");
                yield return TestUtils.WaitForUi(
                    () => create.layout.height >= 40 && dialog.layout.height > 0,
                    $"The New Game dialog must settle at {size}, but has: {create.layout.height}"
                );
                Assert.GreaterOrEqual(
                    dialog.worldBound.yMin,
                    m_main.worldBound.yMin,
                    "The New Game heading must remain inside the viewport at " + size
                );
                Assert.LessOrEqual(
                    create.worldBound.yMax,
                    m_main.worldBound.yMax,
                    "The Create action must remain inside the viewport at " + size
                );
                Assert.LessOrEqual(
                    dialog.worldBound.xMax,
                    m_main.worldBound.xMax,
                    "The New Game dialog must fit horizontally at " + size
                );
                Assert.Greater(
                    m_root.Q<ScrollView>(className: "new-game-scroll").contentViewport.layout.height,
                    44,
                    "The form must retain a usable scrolling area at " + size
                );
                Assert.AreEqual(
                    20f,
                    m_root.Q(className: "new-game-title").resolvedStyle.fontSize,
                    "New Game must use the shared dialog heading size."
                );
            }
            modal.style.display = DisplayStyle.None;
            var layer = m_root.Q("onlineUsersLayer");
            var online = m_root.Q("onlineUsersOverlay");
            layer.RemoveFromClassList("user-list-hidden");
            online.RemoveFromClassList("user-list-hidden");
            foreach (var size in Viewports)
            {
                yield return Resize(size);
                var close = m_root.Q<Button>("closeOnlineUsersButton");
                yield return TestUtils.WaitForUi(
                    () => close.layout.height >= 44 && online.layout.height > 0,
                    "The Online Users dialog must settle at " + size
                );
                Assert.LessOrEqual(
                    close.worldBound.xMax,
                    m_main.worldBound.xMax,
                    "The Close action must fit horizontally at " + size
                );
                Assert.LessOrEqual(
                    online.worldBound.yMax,
                    m_main.worldBound.yMax,
                    "Online Users must remain inside the viewport at " + size
                );
                Assert.AreEqual(
                    20f,
                    online.Q(className: "user-list-title").resolvedStyle.fontSize,
                    "Online Users must use the shared dialog heading size."
                );
                Assert.AreEqual(
                    16f,
                    close.resolvedStyle.fontSize,
                    "Online Users must use the shared action font size."
                );
            }
        }

        [UnityTest]
        public IEnumerator Login_ResizePreservesInputAndKeepsLongActionsWithinWidth()
        {
            TestUtils.ClearBindingsExcept(m_root);
            m_root.Q("main-view__lobby").style.display = DisplayStyle.None;
            var login = m_root.Q<TextField>("footstar-login");
            login.value = "remembered-user";
            m_root.Q<Label>("login-status").text = "Disconnected. Please sign in again or retry remembered login.";
            Show();
            var scroll = m_root.Q<ScrollView>("main-view__login");
            scroll.style.display = DisplayStyle.Flex;
            foreach (var size in Viewports)
            {
                yield return Resize(size);
                var last = m_root.Q<Button>("signup-link");
                yield return TestUtils.WaitForUi(
                    () => last.layout.height > 0,
                    "Login content must be laid out at " + size
                );
                scroll.ScrollTo(last);
                yield return TestUtils.WaitForUi(
                    () => last.worldBound.yMax <= scroll.contentViewport.worldBound.yMax + 1,
                    "The signup action must be reachable by scrolling at " + size
                );
                var retry = m_root.Q<Button>("login-retry");
                Assert.LessOrEqual(
                    retry.worldBound.xMax,
                    scroll.contentViewport.worldBound.xMax + 1,
                    "The remembered-login action must fit at " + size
                );
                Assert.AreEqual(16f, retry.resolvedStyle.fontSize, "Login actions must share the lobby's font size.");
                Assert.LessOrEqual(
                    m_root.Q("login-panel").layout.width,
                    440,
                    "The login form must respect its logical maximum width."
                );
                Assert.AreEqual("remembered-user", login.value, "Rotating must preserve entered login text.");
            }
        }
    }
}
