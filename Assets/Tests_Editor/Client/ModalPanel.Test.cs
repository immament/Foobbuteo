// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Foobbuteo.Client.UI.Modals;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public class ModalPanelTests
    {
        private EditorWindow m_window;
        private VisualElement m_root;
        private ModalHostController m_host;
        private ModalPanelController m_modal;

        [TearDown]
        public void TearDown()
        {
            m_modal?.Dispose();
            m_host?.Dispose();
            if (m_window != null)
            {
                m_window.Close();
                Object.DestroyImmediate(m_window);
            }
        }

        [UnityTest]
        public IEnumerator Modal_BlocksBackgroundAndOwnsOutsidePickingWithoutBlockingPanelControls()
        {
            CreateUi();
            m_modal.TryOpen(m_root.Q("open-settings-btn"));
            yield return TestUtils.WaitForUi(
                IsOpenAndFocused,
                "Opening must settle visibility, background availability, and initial focus."
            );

            var point = m_root.worldBound.position + new Vector2(4, 4);
            Assert.AreSame(
                m_root.Q("modal-backdrop"),
                m_root.panel.Pick(point),
                "Outside the panel the shared backdrop must intercept pointer input."
            );
            var slider = m_root.Q<SliderInt>("sound-volume-slider");
            var picked = m_root.panel.Pick(slider.worldBound.center);
            Assert.IsTrue(
                picked == slider || slider.Contains(picked),
                "Inside the panel pointer input must still reach the slider."
            );
            Assert.IsTrue(
                m_root.Q("toast-container").enabledInHierarchy,
                "Modal background blocking must preserve notifications."
            );
            Assert.AreEqual(
                DisplayStyle.None,
                m_root.Q("contextual-popup-layer").resolvedStyle.display,
                "World-anchored actions must be suppressed while a modal is active."
            );

            SendClick(m_root.Q("game-settings-panel"));
            Assert.IsTrue(m_host.State.HasActivePanel, "Content clicks must not dismiss the active panel.");
            SendClick(m_root.Q("modal-backdrop"));
            yield return TestUtils.WaitForUi(
                () => !m_host.State.HasActivePanel && m_root.Q("screen-content").enabledInHierarchy,
                "Backdrop dismissal must restore background interaction."
            );
        }

        [UnityTest]
        public IEnumerator LineupGutter_PicksSharedBackdropAndDismissesWithoutReachingBackground()
        {
            CreateUi();
            using var lineup = new ModalPanelController(m_host, "mvm-lineup-overlay", () => true, null);
            lineup.Bind(m_root.Q("mvm-lineup-overlay"), m_root.Q("mvm-lineup-cancel"));
            int backgroundClicks = 0;
            m_root.Q("screen-content").RegisterCallback<ClickEvent>(_ => backgroundClicks++);
            lineup.TryOpen();
            yield return TestUtils.WaitForUi(
                () =>
                    lineup.Root.resolvedStyle.display == DisplayStyle.Flex
                    && m_root.Q("mvm-lineup-panel").worldBound.width > 0,
                "The lineup must finish layout before picking its outside gutter."
            );

            var picked = m_root.panel.Pick(m_root.worldBound.position + new Vector2(3, 3));
            Assert.AreSame(
                m_root.Q("modal-backdrop"),
                picked,
                "The transparent lineup gutter must expose the shared dismissal backdrop."
            );
            SendClick(picked);
            Assert.IsFalse(m_host.State.HasActivePanel, "A gutter click must dismiss the lineup.");
            Assert.AreEqual(
                0,
                backgroundClicks,
                "The dismissing click must never be redispatched to background controls."
            );
        }

        [UnityTest]
        public IEnumerator Escape_AllowsFocusedControlToCancelBeforeClosingModal()
        {
            CreateUi();
            m_modal.TryOpen();
            yield return TestUtils.WaitForUi(IsOpenAndFocused, "The dialog must own focus before cancellation.");
            var control = new VisualElement { focusable = true };
            m_root.Q("game-settings-panel").Add(control);
            bool consumed = false;
            EventCallback<KeyDownEvent> consume = evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    consumed = true;
                    evt.StopPropagation();
                }
            };
            // Unity also generates a semantic Cancel event from Escape.
            EventCallback<NavigationCancelEvent> consumeCancel = evt => evt.StopPropagation();
            control.RegisterCallback(consume);
            control.RegisterCallback(consumeCancel);
            control.Focus();
            SendEscape(control);
            Assert.IsTrue(consumed, "Escape must reach the focused control before the host handles it.");
            Assert.IsTrue(
                m_host.State.HasActivePanel,
                "A focused control consuming Escape/Cancel must retain its modal."
            );
            control.UnregisterCallback(consume);
            control.UnregisterCallback(consumeCancel);
            SendEscape(control);
            Assert.IsFalse(m_host.State.HasActivePanel, "An unconsumed Escape must close the modal.");
        }

        private static void SendEscape(VisualElement target)
        {
            using var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            key.target = target;
            target.SendEvent(key);
        }

        [UnityTest]
        public IEnumerator KeyboardAndNavigationCancel_StayInsideModalAndRestoreOpener()
        {
            CreateUi();
            var opener = m_root.Q<Button>("open-settings-btn");
            m_modal.TryOpen(opener);
            yield return TestUtils.WaitForUi(
                IsOpenAndFocused,
                "The modal must receive initial focus before navigation."
            );
            var close = m_root.Q<Button>("close-game-settings-btn");
            close.Focus();
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab }))
            {
                key.target = close;
                close.SendEvent(key);
            }
            Assert.IsTrue(
                m_modal.Root.Contains(m_root.focusController.focusedElement as VisualElement),
                "Tab from the last control must remain within the dialog."
            );
            using (var cancel = NavigationCancelEvent.GetPooled())
            {
                cancel.target = close;
                close.SendEvent(cancel);
            }
            yield return TestUtils.WaitForUi(
                () => m_root.focusController.focusedElement == opener,
                "Navigation Cancel must restore focus to the enabled opener."
            );
        }

        [UnityTest]
        public IEnumerator Rebind_PreservesActivePanelAndDetachesOldBackdrop()
        {
            CreateUi();
            m_modal.TryOpen(m_root.Q("open-settings-btn"));
            yield return TestUtils.WaitForUi(
                IsOpenAndFocused,
                "The original panel must be ready before a visual reload."
            );
            var oldBackdrop = m_root.Q("modal-backdrop");
            m_root.RemoveFromHierarchy();
            BindFreshTree();
            yield return TestUtils.WaitForUi(
                IsOpenAndFocused,
                "The replacement tree must preserve the active panel and focus scope."
            );

            SendClick(oldBackdrop);
            Assert.IsTrue(m_host.State.HasActivePanel, "A detached tree must no longer control the live modal.");
            SendClick(m_root.Q("modal-backdrop"));
            Assert.IsFalse(m_host.State.HasActivePanel, "The replacement backdrop must close the live panel once.");
        }

        private void CreateUi()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.position = new Rect(20, 20, 1000, 700);
            m_window.Show();
            m_host = new ModalHostController(new ModalHostState());
            m_modal = new ModalPanelController(m_host, "game-settings-overlay", () => true, _ => { });
            BindFreshTree();
        }

        [UnityTest]
        public IEnumerator ModalWithoutEnabledControls_ReceivesKeyboardFocusOnItsRoot()
        {
            CreateUi();
            m_root.Q("sound-volume-slider").SetEnabled(false);
            m_root.Q("close-game-settings-btn").SetEnabled(false);
            m_modal.TryOpen(m_root.Q("open-settings-btn"));

            yield return TestUtils.WaitForUi(
                () => m_root.focusController.focusedElement == m_modal.Root,
                "A busy dialog with no enabled controls must keep keyboard focus on its own root."
            );
        }

        [UnityTest]
        public IEnumerator ExtractedHelp_InheritsReadableModalTextColor()
        {
            CreateUi();
            m_root.style.color = Color.black;
            using var help = new ModalPanelController(m_host, "help-modal", () => true, null);
            help.Bind(m_root.Q("help-modal"), m_root.Q("HelpCloseButton"));
            var label = new Label("Readable help content");
            m_root.Q("help-panel").Add(label);
            help.TryOpen();

            yield return TestUtils.WaitForUi(
                () => label.worldBound.width > 0,
                "Help content must be laid out before checking its inherited color."
            );
            Assert.Greater(
                label.resolvedStyle.color.grayscale,
                0.8f,
                "Extracted Help must retain light text against the dark modal surface."
            );
        }

        private void BindFreshTree()
        {
            m_root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();
            TestUtils.ClearBindingsExcept(m_root);
            m_root.style.flexGrow = 1;
            m_window.rootVisualElement.Add(m_root);
            m_host.Bind(m_root);
            m_modal.Bind(m_root.Q("game-settings-overlay"), m_root.Q("sound-volume-slider"));
        }

        private bool IsOpenAndFocused() =>
            m_modal.Root.resolvedStyle.display == DisplayStyle.Flex
            && !m_root.Q("screen-content").enabledInHierarchy
            && m_modal.Root.Contains(m_root.focusController.focusedElement as VisualElement);

        private static void SendClick(VisualElement target)
        {
            using var click = ClickEvent.GetPooled();
            click.target = target;
            target.SendEvent(click);
        }
    }
}
