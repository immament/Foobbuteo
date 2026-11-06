// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using System.Reflection;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Common.dev;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class GemeSettingsTests
    {
        private bool m_hadVolume;
        private int m_savedVolume;
        private EditorWindow m_window;
        private VisualElement m_root;
        private GameSettingsVM m_settings;
        private GameSettingsUiController m_controller;
        private bool m_savedNetworkGuiVisible;

        [SetUp]
        public void SetUp()
        {
            m_savedNetworkGuiVisible = DevNetworkGUI.IsVisible;
            DevNetworkGUI.IsVisible = false;
            m_hadVolume = PlayerPrefs.HasKey(GameSettingsVM.VolumeKey);
            m_savedVolume = PlayerPrefs.GetInt(GameSettingsVM.VolumeKey);
            PlayerPrefs.DeleteKey(GameSettingsVM.VolumeKey);
        }

        [TearDown]
        public void TearDown()
        {
            DevNetworkGUI.IsVisible = m_savedNetworkGuiVisible;
            m_controller?.Dispose();
            if (m_window != null)
            {
                m_window.Close();
                Object.DestroyImmediate(m_window);
            }
            m_controller = null;
            if (m_hadVolume)
                PlayerPrefs.SetInt(GameSettingsVM.VolumeKey, m_savedVolume);
            else
                PlayerPrefs.DeleteKey(GameSettingsVM.VolumeKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshInstallation_DefaultsToHalfVolume()
        {
            var settings = new GameSettingsVM();

            Assert.AreEqual(50, settings.Volume, "A new installation should start at moderate volume.");
            Assert.AreEqual("50%", settings.VolumeText, "The displayed percentage should match the volume.");
        }

        [TestCase(-10, 0)]
        [TestCase(35, 35)]
        [TestCase(120, 100)]
        public void ChangedVolume_IsClampedAndRestored(int requested, int expected)
        {
            var settings = new GameSettingsVM { Volume = requested };

            Assert.AreEqual(expected, settings.Volume, "The slider value must stay within the supported range.");
            Assert.AreEqual(
                expected,
                new GameSettingsVM().Volume,
                "A new settings instance should restore the saved percentage."
            );
        }

        [Test]
        public void StoredOutOfRangeVolume_IsClampedOnLoad()
        {
            PlayerPrefs.SetInt(GameSettingsVM.VolumeKey, 200);

            Assert.AreEqual(
                100,
                new GameSettingsVM().Volume,
                "An invalid stored percentage must not exceed full volume."
            );
        }

        [UnityTest]
        public IEnumerator NetworkGuiToggle_IsHiddenForNonAdmin()
        {
            CreateUi();
            Click(m_root, "open-settings-btn");
            yield return TestUtils.WaitForUi(() => !IsHidden(), "Settings must open for a nonadmin user.");

            var toggle = m_root.Q<Toggle>("dev-network-gui-toggle");
            Assert.IsNotNull(toggle, "Settings must contain the new network GUI option.");
            yield return TestUtils.WaitForUi(
                () => toggle.resolvedStyle.display == DisplayStyle.None,
                "Nonadmin users must not see the network GUI toggle."
            );
        }

        [UnityTest]
        public IEnumerator NetworkGuiToggle_AdminCanEnableDisableAndReloadUi()
        {
            CreateUi();
            m_settings.SetAdmin(true);
            Click(m_root, "open-settings-btn");
            var toggle = m_root.Q<Toggle>("dev-network-gui-toggle");
            yield return TestUtils.WaitForUi(
                () => toggle.resolvedStyle.display == DisplayStyle.Flex && !IsHidden(),
                "Admins must see the network GUI option in open settings."
            );
            Assert.IsFalse(toggle.value, "The network GUI toggle must initially be off.");

            toggle.value = true;
            yield return TestUtils.WaitForUi(
                () => DevNetworkGUI.IsVisible,
                "Enabling the toggle must immediately show the network GUI."
            );

            m_root.RemoveFromHierarchy();
            m_root = CloneUi();
            m_window.rootVisualElement.Add(m_root);
            m_controller.Bind(m_root);
            toggle = m_root.Q<Toggle>("dev-network-gui-toggle");
            yield return TestUtils.WaitForUi(
                () => toggle.value,
                "Rebinding settings must retain the enabled choice for this app run."
            );

            toggle.value = false;
            yield return TestUtils.WaitForUi(
                () => !DevNetworkGUI.IsVisible,
                "Disabling the toggle must immediately hide the network GUI."
            );

            m_settings.SetAdmin(false);
            yield return TestUtils.WaitForUi(
                () => toggle.resolvedStyle.display == DisplayStyle.None,
                "Losing admin status must hide the network GUI option without reopening settings."
            );
        }

        [Test]
        public void NetworkGuiToggle_NewSettingsRetainChoiceUntilApplicationStartup()
        {
            var settings = new GameSettingsVM();
            settings.SetAdmin(true);
            settings.DevNetworkGuiEnabled = true;

            Assert.IsTrue(
                new GameSettingsVM().DevNetworkGuiEnabled,
                "Replacing match settings must preserve the current app run's network GUI choice."
            );

            typeof(DevNetworkGUI)
                .GetMethod("ResetVisibility", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

            Assert.IsFalse(
                new GameSettingsVM().DevNetworkGuiEnabled,
                "Runtime initialization must turn the network GUI off even when domain reload is disabled."
            );
        }

        [Test]
        public void NetworkGuiToggle_NonAdminCannotEnableGui()
        {
            var settings = new GameSettingsVM();

            settings.DevNetworkGuiEnabled = true;

            Assert.IsFalse(DevNetworkGUI.IsVisible, "A nonadmin settings write must not enable the network GUI.");
        }

        [UnityTest]
        public IEnumerator SettingsModal_BindsSliderAndBlocksBackground()
        {
            CreateUi();
            Click(m_root, "open-settings-btn");
            yield return TestUtils.WaitForUi(
                () => !m_root.Q("GameScreen").enabledInHierarchy,
                "Opening settings must disable background controls."
            );

            var slider = m_root.Q<SliderInt>("sound-volume-slider");
            var dragArea = slider.Q("unity-drag-container");
            var percentage = m_root.Q<Label>("sound-volume-label");
            Assert.Greater(
                dragArea.worldBound.height,
                0,
                "The slider must reserve space for its pointer interaction area."
            );
            Assert.LessOrEqual(
                percentage.worldBound.yMax,
                slider.worldBound.yMin,
                "The volume percentage must appear above the slider without covering its track."
            );
            var picked = m_root.panel.Pick(dragArea.worldBound.center);
            Assert.IsTrue(
                picked == slider || slider.Contains(picked),
                "Pointer input at the slider track must reach the slider rather than the percentage label."
            );

            m_root.Q<SliderInt>("sound-volume-slider").value = 25;
            yield return TestUtils.WaitForUi(
                () => m_settings.Volume == 25 && m_root.Q<Label>("sound-volume-label").text == "25%",
                "The slider must update the saved volume and bound percentage label."
            );

            Click(m_root, "close-game-settings-btn");
            yield return TestUtils.WaitForUi(
                () => m_root.Q("GameScreen").enabledInHierarchy && IsHidden(),
                "Close must hide settings and restore background controls."
            );
        }

        [UnityTest]
        public IEnumerator UiReload_RestoresOpenSettingsAndEscapeDismisses()
        {
            CreateUi();
            Click(m_root, "open-settings-btn");
            m_root.RemoveFromHierarchy();
            m_root = CloneUi();
            m_window.rootVisualElement.Add(m_root);
            m_controller.Bind(m_root);
            yield return TestUtils.WaitForUi(
                () => !IsHidden(),
                "The replacement UI must restore the open settings panel."
            );

            var overlay = m_root.Q("game-settings-overlay");
            using (
                var escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape })
            )
            {
                escape.target = overlay;
                overlay.SendEvent(escape);
            }

            yield return TestUtils.WaitForUi(IsHidden, "Escape must dismiss settings after a UI reload.");
        }

        [UnityTest]
        public IEnumerator OnlyBackdropClicks_DismissSettings()
        {
            CreateUi();
            Click(m_root, "open-settings-btn");
            yield return TestUtils.WaitForUi(() => !IsHidden(), "Settings must open before clicking the panel.");

            SendClick(m_root.Q("sound-volume-label"));
            Assert.AreEqual(
                DisplayStyle.Flex,
                m_root.Q("game-settings-overlay").resolvedStyle.display,
                "Clicking inside the panel must leave settings open."
            );

            SendClick(m_root.Q("modal-backdrop"));
            yield return TestUtils.WaitForUi(IsHidden, "Clicking the backdrop must dismiss settings.");
        }

        private void CreateUi()
        {
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_settings = new GameSettingsVM();
            m_controller = new GameSettingsUiController(m_settings);
            m_root = CloneUi();
            m_root.style.flexGrow = 1;
            m_window.Show();
            m_window.rootVisualElement.Add(m_root);
            m_controller.Bind(m_root);
        }

        private static VisualElement CloneUi()
        {
            var root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();
            TestUtils.ClearBindingsExcept(root, root.Q("game-settings-panel"));
            return root;
        }

        private bool IsHidden() => m_root.Q("game-settings-overlay").resolvedStyle.display == DisplayStyle.None;

        private static void SendClick(VisualElement target)
        {
            using var click = ClickEvent.GetPooled();
            click.target = target;
            target.SendEvent(click);
        }

        private static void Click(VisualElement root, string name) =>
            typeof(Clickable)
                .GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(root.Q<Button>(name).clickable, new object[] { null, 0 });
    }
}
