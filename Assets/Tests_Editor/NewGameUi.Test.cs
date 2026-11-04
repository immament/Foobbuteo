// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Reflection;
using Imma.Foobbuteo.Client.Lobby;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class NewGameUiTests
    {
        private FakeLobbyApi m_api;
        private NewGameUiController m_controller;
        private VisualElement m_root;
        private bool m_canCreate;
        private EditorWindow m_window;

        [SetUp]
        public void SetUp()
        {
            m_canCreate = true;
            m_api = new FakeLobbyApi();
            m_controller = new NewGameUiController(m_api, () => m_canCreate);
            m_root = CreateRoot();
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.Show();
            m_window.rootVisualElement.Add(m_root);
            m_controller.Bind(m_root);
        }

        [TearDown]
        public void TearDown()
        {
            m_controller.Dispose();
            m_window.Close();
            UnityEngine.Object.DestroyImmediate(m_window);
        }

        [UnityTest]
        public IEnumerator OpenAndCancel_SendNoRequestAndFreshOpeningResetsDefaults()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            m_root.Q<DropdownField>("new-game-position").index = 8;
            yield return TestUtils.WaitForUi(
                () => ((NewGameVM)m_root.Q("new-game-modal").dataSource).Position == "9 - FC",
                "The changed position must reach the view model before cancellation."
            );
            Click(m_root, "new-game-cancel");
            yield return TestUtils.WaitForUi(() => IsHidden(m_root), "Cancel must hide the form.");

            Assert.AreEqual(0, m_api.Calls, "Opening and cancelling must never create a game.");
            Assert.IsTrue(IsHidden(m_root), "Cancel must hide the modal.");

            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");

            Assert.AreEqual(
                "1 - GK",
                m_root.Q<DropdownField>("new-game-position").value,
                "Each fresh opening must reset the creator position to goalkeeper."
            );
        }

        [UnityTest]
        public IEnumerator HeaderClose_CancelsWithoutSubmittingAndRemainsLockedWhileCreating()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The form must open before header cancellation.");

            Click(m_root, "new-game-close");

            yield return TestUtils.WaitForUi(() => IsHidden(m_root), "The header close action must hide the form.");
            Assert.AreEqual(0, m_api.Calls, "Closing from the header must not create a game.");

            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The form must reopen before submission.");
            Click(m_root, "new-game-create");
            yield return TestUtils.WaitForUi(
                () => !m_root.Q<Button>("new-game-close").enabledSelf,
                "The header close action must be disabled while creation is pending."
            );

            Click(m_root, "new-game-close");

            Assert.IsFalse(IsHidden(m_root), "A header close click must not dismiss a pending creation request.");
            Assert.AreEqual(1, m_api.Calls, "A pending header close click must not send another creation request.");
        }

        [UnityTest]
        public IEnumerator Submit_PassesAllSelectionsAndPreventsDuplicateSubmissionOrCancellation()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            SelectNonDefaultOptions(m_root);
            yield return WaitForSelections(m_root);

            Click(m_root, "new-game-create");
            Click(m_root, "new-game-create");
            Click(m_root, "new-game-cancel");

            yield return TestUtils.WaitForUi(
                () => !m_root.Q("new-game-fields").enabledSelf,
                "The binding must lock pending selections."
            );
            Assert.AreEqual(1, m_api.Calls, "Repeated clicks must send exactly one creation request.");
            Assert.AreEqual(SelectedOptions(), m_api.LastOptions, "The API must receive every selection.");
            Assert.IsFalse(m_root.Q("new-game-fields").enabledSelf, "Selections must be locked while creating.");
            Assert.IsFalse(m_root.Q<Button>("new-game-cancel").enabledSelf, "Pending creation cannot be cancelled.");
            Assert.IsFalse(IsHidden(m_root), "The form must wait for the creation result.");
        }

        [UnityTest]
        public IEnumerator SameColors_ShowsValidationErrorWithoutSubmitting()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            m_root.Q<DropdownField>("new-game-away").value = ShirtColorEnum.Blue.ToString();

            yield return TestUtils.WaitForUi(
                () => !string.IsNullOrEmpty(m_root.Q<Label>("new-game-error").text),
                "Color validation must reach the UI."
            );
            Click(m_root, "new-game-create");

            Assert.AreEqual(0, m_api.Calls, "Identical shirt colors must not be submitted.");
            Assert.That(
                m_root.Q<Label>("new-game-error").text,
                Does.Contain("different"),
                "The form must explain that shirt colors must differ."
            );
        }

        [UnityTest]
        public IEnumerator Failure_RetainsSelectionsAndAllowsRetryThenSuccessCloses()
        {
            const string failure = "Too many requests. Please try again.";
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            SelectNonDefaultOptions(m_root);
            yield return WaitForSelections(m_root);
            Click(m_root, "new-game-create");

            m_api.Complete(new NewGameResult(false, error: failure));
            yield return TestUtils.WaitForUi(
                () =>
                    m_root.Q<Label>("new-game-error").text == failure
                    && m_root.Q<Button>("new-game-create").enabledSelf,
                "A failed request must allow retry."
            );

            Assert.AreEqual(
                failure,
                m_root.Q<Label>("new-game-error").text,
                "The server rejection must appear inline."
            );
            Assert.IsTrue(m_root.Q<Button>("new-game-create").enabledSelf, "A rejected request must allow retry.");
            Click(m_root, "new-game-create");
            Assert.AreEqual(SelectedOptions(), m_api.LastOptions, "Retry must preserve all selections.");
            m_api.Complete(new NewGameResult(true, gameId: 2001));
            yield return TestUtils.WaitForUi(() => IsHidden(m_root), "Successful creation must hide the form.");

            Assert.IsTrue(IsHidden(m_root), "Successful creation must close the modal.");
        }

        [UnityTest]
        public IEnumerator Reload_RetainsSelectionsAndUnbindsOldControls() => ReloadForm(false);

        [UnityTest]
        public IEnumerator Reload_RetainsPendingStateAndUnbindsOldControls() => ReloadForm(true);

        private IEnumerator ReloadForm(bool pending)
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            SelectNonDefaultOptions(m_root);
            yield return WaitForSelections(m_root);
            if (pending)
                Click(m_root, "new-game-create");
            var oldRoot = m_root;
            oldRoot.RemoveFromHierarchy();
            m_root = CreateRoot();
            m_window.rootVisualElement.Add(m_root);

            m_controller.Bind(m_root);
            yield return TestUtils.WaitForUi(
                () =>
                    !IsHidden(m_root)
                    && m_root.Q<DropdownField>("new-game-position").value == "11 - ML"
                    && m_root.Q("new-game-fields").enabledSelf == !pending,
                "The replacement tree must reflect retained form state."
            );
            Click(oldRoot, "new-game-create");
            Click(oldRoot, "new-game-cancel");
            Click(oldRoot, "new-game-close");

            Assert.AreEqual(pending ? 1 : 0, m_api.Calls, "Detached controls must not send requests.");
            Assert.IsFalse(IsHidden(m_root), "A UI reload must retain the open form.");
            Assert.AreEqual(
                "11 - ML",
                m_root.Q<DropdownField>("new-game-position").value,
                "A UI reload must retain the selected position."
            );
            Assert.AreEqual(
                !pending,
                m_root.Q("new-game-fields").enabledSelf,
                "A UI reload must retain the pending request lock."
            );
            if (pending)
            {
                m_api.Complete(new NewGameResult(false, error: "Retry"));
                yield return TestUtils.WaitForUi(
                    () => m_root.Q<Button>("new-game-create").enabledSelf,
                    "A pending response must unlock the replacement tree."
                );
                Assert.IsTrue(
                    m_root.Q<Button>("new-game-create").enabledSelf,
                    "A pending response must update the replacement visual tree."
                );
            }
        }

        [UnityTest]
        public IEnumerator DisconnectReset_ClearsPendingStateAndIgnoresLateResponse()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            Click(m_root, "new-game-create");
            m_canCreate = false;

            m_controller.Reset();
            m_api.Complete(new NewGameResult(false, error: "Late result"));
            yield return TestUtils.WaitForUi(
                () => IsHidden(m_root) && m_root.Q<Label>("new-game-error").text == "",
                "Disconnect must clear pending state and errors."
            );

            Assert.IsTrue(IsHidden(m_root), "Disconnect must close the form even while creation is pending.");
            Assert.IsEmpty(
                m_root.Q<Label>("new-game-error").text,
                "A response after disconnect must not restore stale errors."
            );
            m_canCreate = true;
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            Assert.IsTrue(
                m_root.Q<Button>("new-game-create").enabledSelf,
                "A new connection must not inherit a pending request lock."
            );
        }

        [UnityTest]
        public IEnumerator Dispose_UnsubscribesResultsAndControls()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");

            m_controller.Dispose();
            m_controller.Dispose();
            Click(m_root, "new-game-create");

            Assert.AreEqual(0, m_api.Subscribers, "Disposal must unsubscribe the creation-result event.");
            Assert.AreEqual(0, m_api.Calls, "Disposed UI controls must not submit requests.");
        }

        [UnityTest]
        public IEnumerator SceneDetachment_ResponseUpdatesRetainedStateWithoutTouchingOldTree()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            Click(m_root, "new-game-create");
            yield return TestUtils.WaitForUi(
                () => !m_root.Q("new-game-fields").enabledSelf,
                "Creation must be pending before scene detachment."
            );
            string pendingText = m_root.Q<Button>("new-game-create").text;

            m_controller.Unbind();
            m_root.RemoveFromHierarchy();
            m_api.Complete(new NewGameResult(true, gameId: 2001));

            Assert.AreEqual(
                pendingText,
                m_root.Q<Button>("new-game-create").text,
                "A result after scene detachment must not mutate the old visual tree."
            );
            var nextRoot = CreateRoot();
            m_window.rootVisualElement.Add(nextRoot);
            m_controller.Bind(nextRoot);
            yield return TestUtils.WaitForUi(() => IsHidden(nextRoot), "The returned lobby must reflect completion.");
            Assert.IsTrue(IsHidden(nextRoot), "Returning to the lobby must reflect completed creation.");
        }

        [UnityTest]
        public IEnumerator Layout_WideAndNarrowViewports_KeepDialogAndActionsInsideViewport()
        {
            const float narrowViewportWidth = 390f;
            const float shortViewportHeight = 360f;
            var sizes = new[]
            {
                new Vector2(1440, 900),
                new Vector2(narrowViewportWidth, 640),
                new Vector2(700, shortViewportHeight),
            };
            m_root.name = "main-view";
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            foreach (var size in sizes)
            {
                m_root.EnableInClassList("narrow-screen", size.x == narrowViewportWidth);
                m_root.EnableInClassList("lobby--short", size.y == shortViewportHeight);
                m_root.style.width = size.x;
                m_root.style.height = size.y;
                m_window.position = new Rect(m_window.position.position, size);
                var modal = m_root.Q("new-game-modal");
                yield return TestUtils.WaitForUi(
                    () => Mathf.Abs(modal.layout.width - size.x) <= 1f && Mathf.Abs(modal.layout.height - size.y) <= 1f,
                    $"The modal must resize to the {size.x} by {size.y}px viewport."
                );

                Assert.That(
                    modal.layout.width,
                    Is.EqualTo(size.x).Within(1f),
                    $"The modal must fill the {size.x}px viewport."
                );
                var dialog = m_root.Q(className: "new-game-dialog");
                Assert.GreaterOrEqual(
                    dialog.worldBound.xMin,
                    modal.worldBound.xMin,
                    "The dialog's left edge must remain within the viewport."
                );
                Assert.LessOrEqual(
                    dialog.worldBound.xMax,
                    modal.worldBound.xMax,
                    "The dialog's right edge must remain within the viewport."
                );
                Assert.LessOrEqual(
                    dialog.worldBound.yMax,
                    modal.worldBound.yMax,
                    "The dialog must fit vertically and use scrolling when needed."
                );
                var header = m_root.Q(className: "fbt-card-header");
                Assert.That(
                    m_root.Q<Button>("new-game-close").worldBound.height,
                    Is.EqualTo(header.worldBound.height).Within(1f),
                    $"The close button must fill the header at {size.x} by {size.y}px."
                );
                foreach (string actionName in new[] { "new-game-create", "new-game-cancel" })
                {
                    var action = m_root.Q<Button>(actionName);
                    Assert.LessOrEqual(
                        action.worldBound.yMax,
                        dialog.worldBound.yMax,
                        $"The {actionName} action must remain accessible below the scrollable form."
                    );
                    Assert.GreaterOrEqual(
                        action.worldBound.xMin,
                        dialog.worldBound.xMin,
                        $"The {actionName} action's left edge must remain inside the panel."
                    );
                    Assert.LessOrEqual(
                        action.worldBound.xMax,
                        dialog.worldBound.xMax,
                        $"The {actionName} action's right edge must remain inside the panel."
                    );
                    Assert.Greater(action.worldBound.height, 0f, $"The {actionName} action must remain visible.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Keyboard_TabWrapsInsideModalAndEscapeCancelsWithoutSubmitting()
        {
            const float focusTimeoutSeconds = 5f;
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The open form must become visible.");
            var start = m_root.Q<DropdownField>("new-game-start");
            start.Focus();
            float deadline = Time.realtimeSinceStartup + focusTimeoutSeconds;
            while (
                (m_root.panel.focusController.focusedElement != start || start.layout.width <= 0f)
                && Time.realtimeSinceStartup < deadline
            )
                yield return null;
            Assert.AreSame(
                start,
                m_root.panel.focusController.focusedElement,
                "Opening the form must allow keyboard focus on the first option."
            );
            var create = m_root.Q<Button>("new-game-create");
            create.Focus();
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }))
                create.SendEvent(evt);
            Assert.AreSame(
                m_root.Q<DropdownField>("new-game-type"),
                m_root.panel.focusController.focusedElement,
                "Tab from Create must wrap to the first option instead of entering the background lobby."
            );
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
                start.SendEvent(evt);
            yield return TestUtils.WaitForUi(() => IsHidden(m_root), "Escape must hide the form.");
            Assert.IsTrue(IsHidden(m_root), "Escape must close a form that has not been submitted.");
            Assert.AreEqual(0, m_api.Calls, "Keyboard cancellation must never create a game.");
        }

        [UnityTest]
        public IEnumerator Unbind_DoesNotResetOldVisualState()
        {
            m_controller.Open();
            var background = m_root.Q("main-view__lobby");
            yield return TestUtils.WaitForUi(
                () => !IsHidden(m_root) && !background.enabledSelf,
                "The open form must disable the background before teardown."
            );

            m_controller.Unbind();
            m_controller.Unbind();

            Assert.IsFalse(
                background.enabledSelf,
                "Unbinding must leave old visual state untouched because the panel may have released its resources."
            );
            Assert.IsFalse(IsHidden(m_root), "Teardown must not change the old modal's display.");
            Click(m_root, "new-game-create");
            Assert.AreEqual(0, m_api.Calls, "Unbound controls must no longer submit creation requests.");
        }

        [UnityTest]
        public IEnumerator PanelClosed_PendingResponseAndRepeatedTeardownAreSafe()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The form must open before the request.");
            Click(m_root, "new-game-create");
            yield return TestUtils.WaitForUi(
                () => !m_root.Q("new-game-fields").enabledSelf,
                "The request must be pending before the panel is closed."
            );

            m_window.Close();
            m_controller.Unbind();
            m_api.Complete(new NewGameResult(true, gameId: 2001));
            m_controller.Dispose();
            m_controller.Dispose();

            Assert.AreEqual(
                0,
                m_api.Subscribers,
                "Closing a panel before the response must allow complete, repeatable cleanup."
            );
        }

        [UnityTest]
        public IEnumerator Bindings_UpdateSwatchesAndCreationAvailability()
        {
            m_controller.Open();
            yield return TestUtils.WaitForUi(() => !IsHidden(m_root), "The form must open before changing options.");
            SelectNonDefaultOptions(m_root);
            yield return WaitForSelections(m_root);
            m_canCreate = false;

            m_controller.Refresh();

            yield return TestUtils.WaitForUi(
                () =>
                    !m_root.Q<Button>("new-game-create").enabledSelf
                    && m_root.Q<Image>("new-game-home-swatch").tintColor
                        == Imma.Foobbuteo.Client.Game.ShirtColors.GetColor(SelectedOptions().HomeTeamColor)
                    && m_root.Q<Image>("new-game-away-swatch").tintColor
                        == Imma.Foobbuteo.Client.Game.ShirtColors.GetColor(SelectedOptions().AwayTeamColor),
                "Property notifications must update both swatches and creation availability."
            );
            Click(m_root, "new-game-create");
            Assert.AreEqual(0, m_api.Calls, "Losing creation eligibility must prevent submission.");
        }

        private static VisualElement CreateRoot()
        {
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI Toolkit/Lobby/NewGameView.tmpl.uxml"
            );
            var root = template.CloneTree();
            root.style.flexGrow = 1;
            var lobby = new VisualElement { name = "main-view__lobby" };
            lobby.Add(new Button { name = "main-view__new-game-btn" });
            root.Add(lobby);
            return root;
        }

        private static IEnumerator WaitForSelections(VisualElement root)
        {
            var vm = (NewGameVM)root.Q("new-game-modal").dataSource;
            yield return TestUtils.WaitForUi(
                () =>
                    vm.StartTime == "in 15 minutes"
                    && vm.Turns == "60"
                    && vm.HomeColor == "Green"
                    && vm.AwayColor == "Purple"
                    && vm.Position == "11 - ML",
                "Two-way bindings must transfer every selected option before submission."
            );
        }

        private static NewGameOptions SelectedOptions() => new(15, 60, ShirtColorEnum.Green, ShirtColorEnum.Purple, 11);

        private static void SelectNonDefaultOptions(VisualElement root)
        {
            root.Q<DropdownField>("new-game-start").index =
                SelectedOptions().StartInMinutes - NewGameOptionsRules.MinStartInMinutes;
            root.Q<DropdownField>("new-game-turns").value = "60";
            root.Q<DropdownField>("new-game-home").value = "Green";
            root.Q<DropdownField>("new-game-away").value = "Purple";
            root.Q<DropdownField>("new-game-position").index =
                SelectedOptions().PlayerPosition - NewGameOptionsRules.MinPlayerPosition;
        }

        private static bool IsHidden(VisualElement root) =>
            root.Q("new-game-modal").resolvedStyle.display == DisplayStyle.None;

        private static void Click(VisualElement root, string name) =>
            typeof(Clickable)
                .GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(root.Q<Button>(name).clickable, new object[] { null, 0 });

        private sealed class FakeLobbyApi : ILobbyApiClient
        {
            public event Action<ServerInfoContract> ServerInfoUpdated
            {
                add { }
                remove { }
            }
            public event Action<string> AlertReceived
            {
                add { }
                remove { }
            }
            public event Action<NewGameResult> NewGameCompleted;
            public int Subscribers => NewGameCompleted?.GetInvocationList().Length ?? 0;
            public int Calls { get; private set; }
            public NewGameOptions LastOptions { get; private set; }

            public void NewGame(NewGameOptions options)
            {
                Calls++;
                LastOptions = options;
            }

            public void Complete(NewGameResult result) => NewGameCompleted?.Invoke(result);

            public void SendGameJoinRequest(int gameId) { }

            public void RequestServerInfo() { }

            public void DeleteGame(int gameId) { }

            public void Dispose() { }
        }
    }
}
