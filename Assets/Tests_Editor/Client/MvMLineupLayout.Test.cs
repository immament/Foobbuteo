// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Linq;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.UI;
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
    public class MvMLineupLayoutTests
    {
        [Test]
        public void PresentationDefaults_UseOtherPlayersAndAveragesOnlyForSmallScreens()
        {
            var vm = new MvMLineupVM(42, ClientImageCatalog.LoadForEditor());
            vm.Open("Home", true);
            Assert.IsFalse(vm.StartingExpanded, "Small-screen opening must leave space for the reserve list.");
            Assert.IsTrue(vm.ReservesExpanded, "Other players must initially be expanded on a small screen.");
            Assert.AreSame(
                PlayerTableViewDefinition.AverageOnly,
                vm.Starting.View,
                "Starting XI must initially omit skill columns on small screens."
            );
            Assert.AreSame(
                PlayerTableViewDefinition.AverageOnly,
                vm.Reserves.View,
                "Other players must initially omit skill columns on small screens."
            );
            vm.StartingExpanded = true;
            vm.ReservesExpanded = false;
            vm.Reserves.SetView(PlayerTableViewDefinition.AllSkills);
            vm.Open("Home", false);
            Assert.IsTrue(vm.StartingExpanded, "An already-open session must retain the expanded starting list.");
            Assert.IsFalse(vm.ReservesExpanded, "An already-open session must retain the collapsed reserve list.");
            Assert.AreSame(
                PlayerTableViewDefinition.AllSkills,
                vm.Reserves.View,
                "Refreshing an open session must preserve the selected table view."
            );
            vm.Close();
            vm.Open("Home", false);
            Assert.IsTrue(
                vm.StartingExpanded && vm.ReservesExpanded,
                "A fresh desktop opening must expand both player sections."
            );
            Assert.AreSame(
                PlayerTableViewDefinition.AllSkills,
                vm.Starting.View,
                "A fresh desktop opening must restore the full starting table."
            );
        }

        [UnityTest]
        public IEnumerator Layout_RecordedViewportSizesKeepPitchFooterAndCompleteRowsVisible()
        {
            using var ui = new LayoutFixture();
            ui.Resize(new Vector2(393, 759));
            yield return TestUtils.WaitForUi(
                () => ui.Layout.IsSmall && ui.Root.layout.width == 393,
                "The initial portrait root must have valid geometry before opening."
            );
            ui.VM.Open("A team with a long name", true);
            foreach (
                Vector2 size in new[]
                {
                    new Vector2(393, 759),
                    new Vector2(393, 839),
                    new Vector2(393, 873),
                    new Vector2(873, 393),
                    new Vector2(839, 393),
                    new Vector2(839, 343),
                    new Vector2(839, 279),
                    new Vector2(1120, 522),
                    new Vector2(1290, 763),
                }
            )
            {
                ui.Resize(size);
                yield return TestUtils.WaitForUi(
                    () => ui.IsSettled(size),
                    () => "The lineup must settle after resizing to " + size + ": " + ui.Describe()
                );
                var panel = ui.Root.Q("mvm-lineup-panel");
                var footer = ui.Root.Q(className: "lineup-editor__footer");
                var pitch = ui.Root.Q("mvm-lineup-pitch");
                Assert.LessOrEqual(
                    panel.worldBound.xMax,
                    ui.Overlay.worldBound.xMax + 1,
                    "The panel must fit horizontally at " + size
                );
                Assert.LessOrEqual(
                    footer.worldBound.yMax,
                    ui.Overlay.worldBound.yMax + 1,
                    "Apply and Cancel must fit vertically at " + size
                );
                Assert.Greater(pitch.worldBound.width, 0, "The pitch must remain visible at " + size);
                Assert.AreEqual(
                    DisplayStyle.None,
                    ui.Root.Q("mvm-lineup-starting").resolvedStyle.display,
                    "Rotation must retain the collapsed starting list at " + size
                );
                var reserveTable = ui.Root.Q("mvm-lineup-reserves");
                var content = reserveTable.Q("player-table-content");
                float headerAndRowHeight = content[0].layout.height + content[1].layout.height;
                Assert.GreaterOrEqual(
                    reserveTable.Q<ScrollView>().contentViewport.layout.height,
                    headerAndRowHeight,
                    "At least one full player row plus its header must fit at " + size
                );
                Assert.AreSame(
                    PlayerTableViewDefinition.AverageOnly,
                    ui.VM.Reserves.View,
                    "Resizing must retain the manager's table view at " + size
                );
                if (ui.Layout.IsSmall)
                    Assert.That(
                        panel.worldBound.size,
                        Is.EqualTo(ui.Overlay.worldBound.size),
                        "A small-screen dialog must use the full available root at " + size
                    );
            }
        }

        [UnityTest]
        public IEnumerator Sections_TwoWayTogglesAndUiRebindPreserveDraftAndView()
        {
            using var ui = new LayoutFixture();
            ui.Resize(new Vector2(393, 759));
            yield return TestUtils.WaitForUi(
                () => ui.Layout.IsSmall,
                "Portrait geometry must be available before opening."
            );
            ui.VM.Open("Home", true);
            yield return TestUtils.WaitForUi(
                () => ui.IsSettled(new Vector2(393, 759)),
                "The initial reserve table must become visible."
            );
            var draft = ui.VM.Draft;
            ui.Root.Q<Toggle>("mvm-lineup-starting-toggle").value = true;
            yield return TestUtils.WaitForUi(
                () =>
                    ui.VM.StartingExpanded
                    && ui.Root.Q("mvm-lineup-starting").resolvedStyle.display == DisplayStyle.Flex,
                "The starting toggle must update the view model and show its table."
            );
            ui.Root.Q<Toggle>("mvm-lineup-reserves-toggle").value = false;
            yield return TestUtils.WaitForUi(
                () =>
                    !ui.VM.ReservesExpanded
                    && ui.Root.Q("mvm-lineup-reserves").resolvedStyle.display == DisplayStyle.None,
                "The reserve toggle must independently collapse its table."
            );
            ui.VM.Starting.SetView(PlayerTableViewDefinition.AllSkills);
            ui.RebindLayout();
            Assert.AreSame(draft, ui.VM.Draft, "Rebinding layout must retain the manager's local draft.");
            Assert.IsTrue(ui.VM.StartingExpanded, "Rebinding must retain the expanded starting list.");
            Assert.IsFalse(ui.VM.ReservesExpanded, "Rebinding must retain the collapsed reserve list.");
            Assert.AreSame(
                PlayerTableViewDefinition.AllSkills,
                ui.VM.Starting.View,
                "Rebinding must retain the manually selected table view."
            );
            Assert.IsTrue(ui.VM.Swap(101, 112), "The expanded starting list must still support exchanges.");
            ui.VM.StartingExpanded = false;
            yield return TestUtils.WaitForUi(
                () =>
                    ui.Root.Q("mvm-lineup-starting").resolvedStyle.display == DisplayStyle.None
                    && ui.Overlay.ClassListContains("lineup-editor--lists-collapsed"),
                "Both sections may collapse while retaining their toggle headers."
            );
            Assert.Greater(
                ui.Root.Q<Toggle>("mvm-lineup-reserves-toggle").worldBound.height,
                0,
                "A collapsed reserve section must retain an accessible expansion control."
            );
            ui.VM.ReservesExpanded = true;
            yield return TestUtils.WaitForUi(
                () => ui.Root.Q("mvm-lineup-reserves").resolvedStyle.display == DisplayStyle.Flex,
                "Other players must reopen without losing the draft."
            );
            Assert.AreSame(draft, ui.VM.Draft, "Collapse and expansion must not replace the draft.");
        }

        [UnityTest]
        public IEnumerator Layout_HighResolutionCanvasKeepsLogicalUiAndScreenCoverage()
        {
            using var ui = new LayoutFixture();
            var logicalSize = new Vector2(393, 759);
            ui.Resize(logicalSize);
            yield return TestUtils.WaitForUi(() => ui.Layout.IsSmall, "Logical portrait geometry must settle.");
            ui.VM.Open("Home", true);
            yield return TestUtils.WaitForUi(() => ui.IsSettled(logicalSize), "The dialog must open before scaling.");
            const float renderScale = 2.75f;
            Vector2 renderSize = logicalSize * renderScale;
            var renderRect = new Rect(Vector2.zero, renderSize);
            Assert.IsTrue(
                UiViewportMetrics.TryCreate(
                    renderRect,
                    Vector2.zero,
                    renderSize,
                    renderSize,
                    renderRect,
                    logicalSize,
                    true,
                    true,
                    out var metrics
                ),
                "The high-resolution canvas must provide a valid logical mapping."
            );
            ui.Layout.ApplyViewport(metrics);
            yield return TestUtils.WaitForUi(
                () => Mathf.Abs(ui.Overlay.worldBound.width - renderSize.x) < 1,
                "The logical dialog must cover the high-resolution canvas after local scaling."
            );
            Assert.That(
                ui.Overlay.layout.width,
                Is.EqualTo(logicalSize.x).Within(0.01f),
                "The layout must retain CSS-sized coordinates at higher rendering resolutions."
            );
            Assert.That(
                ui.Overlay.worldBound.height,
                Is.EqualTo(renderSize.y).Within(0.1f),
                "Local scaling must cover the complete physical canvas height."
            );
            Assert.IsTrue(ui.Layout.IsSmall, "Render density must not turn a phone into a desktop layout.");

            var source = new VisualElement();
            var destination = new VisualElement { userData = 112 };
            foreach (var element in new[] { source, destination })
            {
                element.style.position = Position.Absolute;
                element.style.top = 20;
                element.style.width = 40;
                element.style.height = 40;
                element.AddToClassList("lineup-editor__target");
                ui.Overlay.Add(element);
            }
            source.style.left = 20;
            destination.style.left = 100;
            int? droppedPlayer = null;
            using var dragSession = new LineupDragSession(ui.Overlay);
            source.AddManipulator(
                new LineupDragManipulator(
                    dragSession,
                    101,
                    () => new Label("Player"),
                    () => true,
                    (player, target) => droppedPlayer = target
                )
            );
            yield return TestUtils.WaitForUi(
                () => destination.worldBound.width > 0,
                "Scaled pointer targets must have settled bounds before dragging."
            );
            using (
                var down = PointerDownEvent.GetPooled(
                    new Event
                    {
                        type = EventType.MouseDown,
                        button = 0,
                        mousePosition = source.worldBound.center,
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
                        mousePosition = source.worldBound.center + new Vector2(4 * renderScale, 0),
                    }
                )
            )
                source.SendEvent(move);
            Assert.IsNull(
                ui.Overlay.Q(className: "lineup-editor__ghost"),
                "Movement below eight logical units must not start a drag at higher render density."
            );
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
                source.SendEvent(up);
            Assert.AreEqual(112, droppedPlayer, "Scaled hit testing must deliver the correct drag destination.");
        }

        private sealed class LayoutFixture : IDisposable
        {
            private readonly EditorWindow m_window;
            private readonly PlayerTableView m_starting = new();
            private readonly PlayerTableView m_reserves = new();
            internal VisualElement Root { get; }
            internal VisualElement Overlay { get; }
            internal MvMLineupVM VM { get; } = new(42, ClientImageCatalog.LoadForEditor());
            internal MvMLineupLayout Layout { get; private set; }

            internal LayoutFixture()
            {
                m_window = ScriptableObject.CreateInstance<EditorWindow>();
                m_window.position = new Rect(30, 30, 1450, 1000);
                m_window.Show();
                Root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/Players/MvMLineup.tmpl.uxml")
                    .CloneTree();
                Root.style.position = Position.Absolute;
                Root.style.fontSize = 14;
                Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/Common.style.uss"));
                m_window.rootVisualElement.Add(Root);
                Overlay = Root.Q("mvm-lineup-surface");
                Overlay.dataSource = VM;
                VM.SetTeam(TeamIndex.Home);
                VM.Load(
                    new MvMLineupResult
                    {
                        Success = true,
                        Squad = Enumerable
                            .Range(1, 30)
                            .Select(index => new GameDetails_PlayerContract
                            {
                                PlayerId = 100 + index,
                                Name = "A player with a long name " + index,
                                CountryId = 0,
                                MoveSkill = 10,
                                ConfrontationSkill = index == 1 ? 0 : 5,
                                LowPassSkill = 3,
                                LongPassSkill = 3,
                                GkPassSkill = 0,
                                CrossesSkill = 3,
                                ShootingSkill = 3,
                                LongShotsSkill = 3,
                                GkSavesSkill = 0,
                                OutOfAreaSkill = 0,
                            })
                            .ToArray(),
                        Lineup = Enumerable.Range(101, 11).ToArray(),
                    }
                );
                m_starting.Bind(
                    Root.Q("mvm-lineup-starting"),
                    VM.Starting,
                    viewControls: Root.Q("mvm-lineup-views"),
                    selectView: VM.SetView
                );
                m_reserves.Bind(Root.Q("mvm-lineup-reserves"), VM.Reserves, Array.Empty<PlayerTableViewDefinition>());
                RebindLayout();
            }

            internal void Resize(Vector2 size)
            {
                Root.style.width = size.x;
                Root.style.height = size.y;
            }

            internal bool IsSettled(Vector2 size) =>
                Mathf.Abs(Root.layout.width - size.x) < 0.1f
                && Mathf.Abs(Overlay.layout.height - size.y) < 0.1f
                && Root.Q<Toggle>("mvm-lineup-starting-toggle").value == VM.StartingExpanded
                && Root.Q<Toggle>("mvm-lineup-reserves-toggle").value == VM.ReservesExpanded
                && Root.Q("mvm-lineup-starting").resolvedStyle.display == VM.StartingDisplay
                && Root.Q("mvm-lineup-reserves").resolvedStyle.display == VM.ReservesDisplay
                && Root.Q("mvm-lineup-pitch").layout.width > 0
                && Root.Q("mvm-lineup-reserves").Q<ScrollView>().contentViewport.layout.height > 0;

            internal string Describe() =>
                "root="
                + Root.layout
                + ", overlay="
                + Overlay.layout
                + ", panel="
                + Root.Q("mvm-lineup-panel").layout
                + ", body="
                + Root.Q(className: "lineup-editor__body").layout
                + ", pitchFrame="
                + Root.Q("mvm-lineup-pitch-frame").layout
                + ", pitch="
                + Root.Q("mvm-lineup-pitch").layout
                + ", table="
                + Root.Q("mvm-lineup-reserves").layout
                + ", viewport="
                + Root.Q("mvm-lineup-reserves").Q<ScrollView>().contentViewport.layout;

            internal void RebindLayout()
            {
                Layout?.Dispose();
                Layout = new MvMLineupLayout(Root, Overlay, VM, MeasureTestViewport);
            }

            private static UiViewportMetrics? MeasureTestViewport(VisualElement root)
            {
                // Virtual viewport sizes must not depend on the monitor or Editor window's native bounds.
                Vector2 size = root.contentRect.size;
                return UiViewportMetrics.TryCreate(
                    new Rect(Vector2.zero, size),
                    Vector2.zero,
                    size,
                    size,
                    new Rect(Vector2.zero, size),
                    size,
                    false,
                    false,
                    out var metrics
                )
                    ? metrics
                    : null;
            }

            public void Dispose()
            {
                Layout.Dispose();
                m_starting.Dispose();
                m_reserves.Dispose();
                m_window.Close();
                UnityEngine.Object.DestroyImmediate(m_window);
            }
        }
    }
}
