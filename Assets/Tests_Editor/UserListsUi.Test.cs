// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Reflection;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class UserListsUiTests
    {
        private const string k_GlobalTemplate = "Assets/UI Toolkit/Users/OnlineUsersPanel.tmpl.uxml";
        private const string k_ListTemplate = "Assets/UI Toolkit/Users/UserListView.tmpl.uxml";
        private const string k_Styles = "Assets/UI Toolkit/Users/UserLists.style.uss";
        private const int k_RoomId = 1201;
        private const int k_OtherRoomId = 1202;

        private EditorWindow m_window;
        private UserListsUiController m_controller;

        [TestCase("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml", false)]
        [TestCase("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml", true)]
        public void ScreenTemplates_ContainUserControlsAndResolvedRowTemplate(string path, bool room)
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path).CloneTree();

            var overlay = root.Q("onlineUsersOverlay");
            if (room)
            {
                Assert.IsNull(
                    root.Q<Button>("onlineUsersButton"),
                    "The game room must not expose global user browsing."
                );
                Assert.IsNull(overlay, "The game room must not instantiate the global online-user panel.");
                Assert.IsNull(
                    root.Q("onlineUsersLayer"),
                    "The game room must contain no leftover global overlay wrapper."
                );
                Assert.IsNotNull(root.Q("roomUsersContent"), "The room must contain its separate roster view.");
                Assert.IsNotNull(
                    root.Q("roomMessagesContent").Q("MatchReportScroll"),
                    "Existing match commentary must remain in the default Messages tab."
                );
            }
            else
            {
                Assert.IsNotNull(
                    root.Q<Button>("onlineUsersButton"),
                    "The lobby must still expose global user browsing."
                );
                Assert.IsNotNull(overlay, "The lobby must retain its online-user panel.");
                Assert.AreEqual(
                    PickingMode.Position,
                    root.Q("onlineUsersLayer").pickingMode,
                    "The lobby backdrop must intercept clicks outside the online-user panel."
                );
                Assert.IsNotNull(
                    overlay.Q<ListView>("usersList").itemTemplate,
                    "Unity must resolve the lobby row template and its room-link and administrator controls."
                );
            }
        }

        [UnityTest]
        public IEnumerator LobbyOverlay_FillsAvailableHeight_AndBackdropBlocksAndDismisses()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            var api = new UserListSessionTests.FakePresenceApi();
            m_controller = new UserListsUiController(api, () => false);

            m_window.Show();
            const string path = "Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml";
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path).CloneTree();
            root.style.flexGrow = 1;
            // Only presence bindings belong to this isolated screen-layout test.
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                bool presence = false;
                for (var ancestor = element; ancestor != null; ancestor = ancestor.parent)
                    if (ancestor.name == "userListRoot")
                        presence = true;
                if (!presence)
                    element.ClearBindings();
            }
            var main = root.Q("main-view");
            main.dataSource = null;
            main.style.position = Position.Absolute;
            main.style.left = main.style.right = main.style.top = main.style.bottom = 0;
            root.Q("main-view__login")?.RemoveFromHierarchy();
            m_window.rootVisualElement.Add(root);
            m_controller.Bind(root);
            Click(root, "onlineUsersButton");
            api.Reply(api.Requests[0], Row());
            var overlay = root.Q("onlineUsersOverlay");
            var layer = root.Q("onlineUsersLayer");
            foreach (float width in new[] { 360f, 900f })
            {
                m_window.position = new Rect(100, 100, width, 600);
                yield return TestUtils.WaitForUi(
                    () => Math.Abs(root.layout.width - width) < 3f && overlay.layout.height > root.layout.height * 0.8f,
                    $"The online panel must fill the screen above its content at width {width}: {path}."
                );
                Assert.AreEqual(
                    Position.Absolute,
                    layer.resolvedStyle.position,
                    "The template wrapper must overlay the screen without consuming sibling layout space."
                );
                Assert.Greater(
                    overlay.Q<ListView>("usersList").layout.height,
                    root.layout.height * 0.6f,
                    $"The online list must retain substantial scrollable height in {path} at width {width}."
                );
                Assert.GreaterOrEqual(
                    overlay.worldBound.yMin,
                    root.worldBound.yMin,
                    "The online panel must start inside the visible screen."
                );
                Assert.LessOrEqual(
                    overlay.worldBound.yMax,
                    root.worldBound.yMax,
                    "The online panel must end inside the visible screen."
                );
                var bounds = layer.worldBound;
                var backdropPoints = new[]
                {
                    new Vector2((bounds.xMin + overlay.worldBound.xMin) / 2, bounds.center.y),
                    new Vector2((overlay.worldBound.xMax + bounds.xMax) / 2, bounds.center.y),
                    new Vector2(bounds.center.x, (bounds.yMin + overlay.worldBound.yMin) / 2),
                    new Vector2(bounds.center.x, (overlay.worldBound.yMax + bounds.yMax) / 2),
                };
                foreach (var point in backdropPoints)
                    Assert.AreSame(
                        layer,
                        root.panel.Pick(point),
                        $"The backdrop must block underlying controls at {point} in the {width}px viewport."
                    );
                Assert.That(
                    layer.resolvedStyle.backgroundColor.a,
                    Is.InRange(0.01f, 0.99f),
                    "The backdrop must tint the underlying screen while keeping it visible."
                );

                SendClick(overlay.Q<Label>(className: "user-list-title"));
                Assert.IsFalse(
                    layer.ClassListContains("user-list-hidden"),
                    "Clicks inside the dialog must not dismiss the online-user panel."
                );
                bool clickEscaped = false;
                EventCallback<ClickEvent> onRootClicked = _ => clickEscaped = true;
                root.RegisterCallback(onRootClicked);
                SendClick(layer);
                root.UnregisterCallback(onRootClicked);
                Assert.IsFalse(clickEscaped, "A dismissing backdrop click must not reach screen handlers.");
                yield return TestUtils.WaitForUi(
                    () => layer.resolvedStyle.display == DisplayStyle.None,
                    "Clicking the backdrop must hide the panel and its full-screen wrapper."
                );
                Assert.AreNotSame(
                    layer,
                    root.panel.Pick(backdropPoints[0]),
                    "Closing the panel must restore input to the underlying screen."
                );
                Click(root, "onlineUsersButton");
                yield return TestUtils.WaitForUi(
                    () => layer.resolvedStyle.display != DisplayStyle.None,
                    "The online-user panel must reopen after backdrop dismissal."
                );
            }
            Click(root, "closeOnlineUsersButton");
            yield return TestUtils.WaitForUi(
                () => layer.resolvedStyle.display == DisplayStyle.None,
                "Closing the online panel must hide its full-screen wrapper."
            );
            m_controller.Unbind();
            root.RemoveFromHierarchy();
        }

        [UnityTest]
        public IEnumerator RoomRoster_HasNoRoomLinkAndKeepsSlotPresentation()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            var api = new UserListSessionTests.FakePresenceApi();
            m_controller = new UserListsUiController(api, () => true, k_RoomId);

            m_window.Show();
            m_window.position = new Rect(100, 100, 480, 600);
            var root = BuildRoot(room: true);
            m_window.rootVisualElement.Add(root);
            m_controller.Bind(root);
            Click(root, "usersTabButton");
            var row = Row();
            row.InRoomId = k_RoomId;
            api.Reply(api.Requests[0], row);
            var roster = root.Q("roomUsersContent");
            yield return TestUtils.WaitForUi(
                () => roster.Q<Label>(className: "user-list-name")?.text == row.PlayerName,
                "The roster must bind its user row before verifying controls."
            );
            Assert.AreEqual(
                roster.Q<Button>("userInRoomBtn").resolvedStyle.display,
                DisplayStyle.None,
                "Room roster rows must contain no room information or join control."
            );
            Assert.IsNotNull(
                roster.Q(className: "user-shirt"),
                "Removing room links must preserve the existing slot shirt presentation."
            );
            Assert.IsEmpty(api.Joins, "Displaying the current room roster must not issue room joins.");
        }

        [UnityTest]
        public IEnumerator RoomTabs_PauseHiddenRoster_AndReloadRemovesOldCallbacks()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            var api = new UserListSessionTests.FakePresenceApi();
            m_controller = new UserListsUiController(api, () => true, k_RoomId);

            m_window.Show();
            m_window.position = new Rect(100, 100, 480, 600);
            var root = BuildRoot(room: true);
            m_window.rootVisualElement.Add(root);
            m_controller.Bind(root);
            yield return TestUtils.WaitForUi(() => root.panel != null, "The user-list test panel must attach.");
            Assert.IsEmpty(api.Requests, "Messages is the default tab and must produce no roster traffic.");
            Click(root, "usersTabButton");
            Assert.AreEqual(1, api.Requests.Count, "Opening Users should fetch the room roster immediately.");
            var hiddenRequest = api.Requests[0];

            Assert.AreEqual(k_RoomId, api.Requests[0].RoomId, "The room controller must request only its room scope.");
            Click(root, "messagesTabButton");
            api.Reply(hiddenRequest, Row());
            var roomSession = (UserListSession)root.Q("roomUsersContent").Q("userListRoot").dataSource;
            Assert.IsEmpty(roomSession.Rows, "A hidden roster must reject its pending response.");
            Click(root, "usersTabButton");
            Assert.AreEqual(2, api.Requests.Count, "Reopening Users must refresh the room roster immediately.");

            var oldOpen = root.Q<Button>("usersTabButton");
            root.RemoveFromHierarchy();
            int countAfterDetach = api.Requests.Count;
            Click(oldOpen);
            Assert.AreEqual(
                countAfterDetach,
                api.Requests.Count,
                "Detaching a view must immediately remove callbacks before Unity releases its controls."
            );
            var replacement = BuildRoot(room: true);
            m_window.rootVisualElement.Add(replacement);
            m_controller.Bind(replacement);
            int countAfterReload = api.Requests.Count;
            Click(oldOpen);
            Assert.AreEqual(
                countAfterReload,
                api.Requests.Count,
                "Detached controls must have no active callbacks after reload."
            );
            m_controller.Dispose();
            Click(replacement, "usersTabButton");
            Assert.AreEqual(0, api.SubscriberCount, "Disposal must remove all list and UI session subscriptions.");
        }

        [UnityTest]
        public IEnumerator BoundRows_DisplayLiteralNamesAndWorkingRoomLinks_AcrossNarrowLayouts()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            var api = new UserListSessionTests.FakePresenceApi();
            m_controller = new UserListsUiController(api, () => true);

            m_window.Show();
            m_window.position = new Rect(100, 100, 480, 600);
            var root = BuildRoot();
            m_window.rootVisualElement.Add(root);
            m_controller.Bind(root);
            Click(root, "onlineUsersButton");
            api.Reply(api.Requests[0], Row());
            var overlay = root.Q("onlineUsersOverlay");

            yield return TestUtils.WaitForUi(
                () => overlay.Q<Button>("userInRoomBtn")?.text == $"In room #{k_OtherRoomId}",
                () =>
                    $"Row bindings must populate the room link from the received snapshot. Expected: '{$"In room #{k_OtherRoomId}"}', got: '{overlay.Q<Button>("userInRoomBtn")?.text ?? "[null]"}'"
            );

            var name = overlay.Q<Label>(className: "user-list-name");
            Assert.AreEqual("<b>Literal player</b>", name.text, "Names must remain literal user content.");
            Assert.IsFalse(name.enableRichText, "Presence rows must not interpret player names as markup.");
            Click(overlay, "userInRoomBtn");
            Assert.That(
                api.Joins,
                Is.EqualTo(new[] { k_OtherRoomId }),
                "The room link must call the existing join boundary."
            );
            Click(overlay, "disconnectUserButton");
            Assert.AreEqual(1, api.Disconnects.Count, "The displayed administrator action must submit its target.");

            foreach (float width in new[] { 360f, 900f })
            {
                m_window.position = new Rect(100, 100, width, 600);
                yield return TestUtils.WaitForUi(
                    () => Math.Abs(root.layout.width - width) < 3f,
                    "The test root must resize before checking row layout."
                );
                yield return TestUtils.WaitForUi(
                    () => overlay.Q<Button>("userInRoomBtn").worldBound.xMax <= overlay.worldBound.xMax,
                    "Room actions must remain inside the user panel after resizing."
                );
                Assert.Greater(
                    overlay.Q<ListView>("usersList").layout.height,
                    0,
                    $"The list must retain scrollable space at width {width}."
                );
            }
        }

        private static VisualElement BuildRoot(bool room = false)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_Styles));
            if (room)
            {
                root.Add(new Button { name = "messagesTabButton", text = "Messages" });
                root.Add(new Button { name = "usersTabButton", text = "Users" });
                root.Add(new VisualElement { name = "roomMessagesContent" });
                var roster = new VisualElement { name = "roomUsersContent" };
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ListTemplate).CloneTree(roster);
                root.Add(roster);
                return root;
            }
            root.Add(new Button { name = "onlineUsersButton", text = "Online users" });
            var layer = new VisualElement { name = "onlineUsersLayer" };
            layer.AddToClassList("online-users-layer");
            layer.AddToClassList("user-list-hidden");
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_GlobalTemplate).CloneTree(layer);
            root.Add(layer);
            return root;
        }

        private static UserPresenceRow Row() =>
            new()
            {
                IsConnected = true,
                ClientId = 1301,
                UserId = 1401,
                PlayerId = 1501,
                PlayerName = "<b>Literal player</b>",
                InRoomId = k_OtherRoomId,
            };

        private static void Click(VisualElement root, string name) => Click(root.Q<Button>(name));

        private static void SendClick(VisualElement target)
        {
            using var evt = ClickEvent.GetPooled();
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void Click(Button button) =>
            typeof(Clickable)
                .GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });

        [TearDown]
        public void TearDown()
        {
            m_controller?.Dispose();

            if (m_window != null)
            {
                m_window.Close();
                UnityEngine.Object.DestroyImmediate(m_window);
            }

            m_controller = null;
            m_window = null;
        }
    }
}
