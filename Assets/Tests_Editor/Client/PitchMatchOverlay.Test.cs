// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2026 Dariusz Juńczyk

using System.Collections;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class PitchMatchOverlayTests
    {
        private EditorWindow m_window;
        private GameHudVM m_vm;
        private VisualElement m_root;

        [TearDown]
        public void TearDown()
        {
            if (m_window != null)
            {
                m_window.Close();
                Object.DestroyImmediate(m_window);
            }
            if (m_vm != null)
                Object.DestroyImmediate(m_vm);
        }

        [UnityTest]
        public IEnumerator CompactScoreboard_BindsTeamColorsNamesAndLiveScores()
        {
            CreateUi();
            var home = m_root.Q("pitch-home-team");
            var away = m_root.Q("pitch-away-team");
            yield return TestUtils.WaitForUi(
                () => m_root.Q<Label>("pitch-home-name").text == "Home club",
                "The pitch scoreboard must display the configured home name."
            );
            Assert.AreEqual(
                "Away club",
                m_root.Q<Label>("pitch-away-name").text,
                "The pitch scoreboard must display the configured away name."
            );
            Assert.AreEqual(
                new Color(0.92f, 0.94f, 0.97f),
                home.resolvedStyle.backgroundColor,
                "The white team's scoreboard must use its shared shirt palette color."
            );
            Assert.AreEqual(
                Color.black,
                m_root.Q("pitch-home-name").resolvedStyle.color,
                "The white team's name must inherit its configured dark text."
            );
            Assert.AreEqual(
                new Color(0.9f, 0.08f, 0.1f),
                away.resolvedStyle.backgroundColor,
                "The red team's scoreboard must use its shared shirt palette color."
            );
            Assert.AreEqual(
                Color.white,
                m_root.Q("pitch-away-score").resolvedStyle.color,
                "The red team's score must inherit its configured light text."
            );

            m_vm.ScoreA = 12;
            m_vm.ScoreB = 3;
            m_vm.TeamA = new TeamVM(TeamIndex.Home, 3, "Replacement club", null, null, ShirtColorEnum.Red);
            yield return TestUtils.WaitForUi(
                () =>
                    m_root.Q<Label>("pitch-home-score").text == "12"
                    && m_root.Q<Label>("pitch-away-score").text == "3"
                    && m_root.Q<Label>("pitch-home-name").text == "Replacement club"
                    && home.resolvedStyle.backgroundColor == new Color(0.9f, 0.08f, 0.1f),
                "Scores and replacement team configuration must update without rebuilding the overlay."
            );
        }

        [UnityTest]
        public IEnumerator CompactOverlay_FitsShortPitchWithoutTakingPitchSpaceAndHidesOnNormalLayout()
        {
            CreateUi();
            var main = m_root.Q("MainLayout");
            var pitch = m_root.Q("PitchContainer");
            var overlay = m_root.Q("pitch-match-overlay");
            m_vm.TeamA = new TeamVM(
                TeamIndex.Home,
                1,
                "A very long home football club name",
                null,
                null,
                ShirtColorEnum.White
            );
            m_vm.TeamB = new TeamVM(
                TeamIndex.Away,
                2,
                "A very long away football club name",
                null,
                null,
                ShirtColorEnum.Red
            );
            foreach (var name in new[] { "mobile-home-edit-lineup", "mobile-away-edit-lineup" })
                m_root.Q(name).style.display = DisplayStyle.Flex;
            foreach (float width in new[] { 839f, 640f })
            {
                main.style.width = width;
                main.style.height = 360;
                main.AddToClassList("compact-header");
                yield return TestUtils.WaitForUi(
                    () => Mathf.Abs(main.layout.width - width) < 0.1f && overlay.worldBound.width > 0,
                    "The compact overlay must settle at width " + width
                );
                Assert.AreEqual(
                    DisplayStyle.None,
                    m_root.Q("MainHeader").resolvedStyle.display,
                    "The regular header must be hidden while the pitch overlay is active."
                );
                Assert.AreEqual(
                    pitch.worldBound.yMin,
                    overlay.worldBound.yMin,
                    "The absolute overlay must start at the pitch top without reserving a header row."
                );
                foreach (string name in new[] { "pitch-scoreboard", "pitch-header-controls" })
                {
                    var control = m_root.Q(name);
                    Assert.GreaterOrEqual(
                        control.worldBound.xMin,
                        pitch.worldBound.xMin,
                        "Compact controls must stay inside the left pitch edge: " + name
                    );
                    Assert.LessOrEqual(
                        control.worldBound.xMax,
                        pitch.worldBound.xMax,
                        "Compact controls must stay inside the right pitch edge: " + name
                    );
                }
                Assert.LessOrEqual(
                    m_root.Q("pitch-scoreboard").worldBound.xMax,
                    m_root.Q("pitch-header-controls").worldBound.xMin,
                    "Long team names and tactic buttons must not overlap Help and Close."
                );
                var picked = m_root.panel.Pick(new Vector2(pitch.worldBound.center.x, pitch.worldBound.yMax - 5));
                Assert.IsFalse(
                    picked == overlay || overlay.Contains(picked),
                    "The empty pitch below the controls must remain available for gameplay input."
                );
            }
            main.RemoveFromClassList("compact-header");
            yield return TestUtils.WaitForUi(
                () =>
                    overlay.resolvedStyle.display == DisplayStyle.None
                    && m_root.Q("MainHeader").resolvedStyle.display == DisplayStyle.Flex,
                "Returning to the normal layout must restore the existing header and hide the pitch overlay."
            );
        }

        private void CreateUi()
        {
            m_vm = ScriptableObject.CreateInstance<GameHudVM>();
            m_vm.TeamA = new TeamVM(TeamIndex.Home, 1, "Home club", null, null, ShirtColorEnum.White);
            m_vm.TeamB = new TeamVM(TeamIndex.Away, 2, "Away club", null, null, ShirtColorEnum.Red);
            m_root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();
            var overlay = m_root.Q("pitch-match-overlay");
            Assert.IsNotNull(overlay, "Compact match controls must be provided as a pitch overlay.");
            TestUtils.ClearBindingsExcept(m_root, overlay);
            overlay.dataSource = m_vm;
            m_root.Q("MainLayout").AddToClassList("compact-header");
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.position = new Rect(50, 50, 1000, 750);
            m_window.rootVisualElement.Add(m_root);
            m_window.Show();
        }
    }
}
