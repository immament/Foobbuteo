// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using Imma.Foobbuteo.Client.Game.UI;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class GameRoomPresentationTests
    {
        private const float k_pitchAspect = 1.6f;

        [TestCase(6, false)]
        [TestCase(5, true)]
        [TestCase(1, true)]
        [TestCase(0, true)]
        [TestCase(-1, false)]
        public void MoveCountdown_EmphasizesOnlyFinalFiveSeconds(int seconds, bool urgent)
        {
            var vm = ScriptableObject.CreateInstance<GameHudVM>();
            try
            {
                vm.UpdateTimer(GameHudVM.TimerType.TimeToMove, seconds);
                Assert.AreEqual(urgent, vm.IsTimerUrgent);
                Assert.AreEqual($"{Mathf.Max(0, seconds)}s remaining", vm.TimerText);
                vm.UpdateTimer(GameHudVM.TimerType.Processing);
                Assert.IsFalse(vm.IsTimerUrgent);
                vm.UpdateTimer(GameHudVM.TimerType.TimeToMove, 3);
                vm.TimerText = "Final Result 0 : 0";
                Assert.IsFalse(vm.IsTimerUrgent);
            }
            finally
            {
                Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void PendingStartAndResults_AreNotUrgent()
        {
            var vm = ScriptableObject.CreateInstance<GameHudVM>();
            try
            {
                foreach (
                    var type in new[]
                    {
                        GameHudVM.TimerType.SecondsLeft,
                        GameHudVM.TimerType.RunningTurn,
                        GameHudVM.TimerType.EndPlayerMove,
                        GameHudVM.TimerType.Starting,
                    }
                )
                {
                    vm.UpdateTimer(GameHudVM.TimerType.TimeToMove, 3);
                    vm.UpdateTimer(type, 2);
                    Assert.IsFalse(vm.IsTimerUrgent, type.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(vm);
            }
        }

        [UnityTest]
        public IEnumerator PitchLayout_RecoversFromUnmeasuredAndZeroWidthStartup()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var detachedRoot = new VisualElement();
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml"
                );
                tree.CloneTree(detachedRoot);
                var main = detachedRoot.Q("MainLayout");
                var leftSide = main.Q("LeftSide");
                var pitchArea = main.Q("PitchArea");
                var pitchContainer = main.Q("PitchContainer");
                ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect);
                Assert.IsFalse(float.IsNaN(leftSide.style.width.value.value));
                Assert.IsFalse(float.IsNaN(pitchContainer.style.width.value.value));
                Assert.IsFalse(float.IsNaN(pitchContainer.style.height.value.value));
                TestUtils.ClearBindingsExcept(detachedRoot);
                main.style.position = Position.Absolute;
                main.style.width = 1445;
                main.style.height = 561;
                ResponsiveLayout.ApplyLayout(main, 1445, 561, 1445f / 561f);
                main.AddToClassList("fixed-pitch-layout");
                leftSide.style.width = 0;
                window.Show();
                window.rootVisualElement.Add(detachedRoot);
                for (int frame = 0; frame < 6; frame++)
                    yield return null;
                Assert.AreEqual(0, pitchArea.contentRect.width);
                Assert.Greater(pitchArea.contentRect.height, 100);

                pitchArea.RegisterCallback<GeometryChangedEvent>(_ =>
                    ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect)
                );
                ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect);
                for (int frame = 0; frame < 12; frame++)
                    yield return null;
                Assert.Greater(leftSide.layout.width, 100);
                Assert.Greater(pitchContainer.layout.height, 100);
                Assert.AreEqual(pitchArea.contentRect.width, pitchContainer.layout.width, 1f);
                Assert.AreEqual(pitchArea.contentRect.height, pitchContainer.layout.height, 1f);
                Assert.AreEqual(pitchContainer.worldBound.xMax, main.Q("RightSide").worldBound.xMin, 1f);
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator PitchLayout_SizesLeftSideAndFillsRemainingWidthAcrossRotation()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.Show();
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml"
                );
                tree.CloneTree(window.rootVisualElement);
                var main = window.rootVisualElement.Q("MainLayout");
                TestUtils.ClearBindingsExcept(window.rootVisualElement);
                main.style.position = Position.Absolute;
                var pitchArea = main.Q("PitchArea");
                var pitchContainer = main.Q("PitchContainer");
                var leftSide = main.Q("LeftSide");
                var rightSide = main.Q("RightSide");
                var gameScreen = main.Q("GameScreen");
                pitchArea.RegisterCallback<GeometryChangedEvent>(_ =>
                    ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect)
                );

                foreach (
                    var size in new[]
                    {
                        new Vector2(1077, 721),
                        new Vector2(1670, 720),
                        new Vector2(2560, 720),
                        new Vector2(502, 725),
                        new Vector2(502, 1115),
                        new Vector2(900, 800),
                        new Vector2(844, 390),
                        new Vector2(1077, 721),
                    }
                )
                {
                    main.style.width = size.x;
                    main.style.height = size.y;
                    ResponsiveLayout.ApplyLayout(main, size.x, size.y, size.x / size.y);
                    ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect);
                    yield return TestUtils.WaitForUi(
                        () =>
                            Mathf.Abs(main.layout.width - size.x) < 0.1f
                            && Mathf.Abs(main.layout.height - size.y) < 0.1f
                            && Mathf.Abs(pitchContainer.layout.width - pitchArea.contentRect.width) < 1f
                            && (
                                main.ClassListContains("stacked-layout")
                                || Mathf.Abs(
                                    leftSide.layout.width
                                        - Mathf.Min(
                                            pitchArea.contentRect.height * k_pitchAspect,
                                            gameScreen.contentRect.width - (size.x <= 2000f ? 312f : 336f)
                                        )
                                ) < 1f
                            )
                            && (
                                !main.ClassListContains("portrait-screen")
                                || Mathf.Abs(pitchContainer.layout.height - pitchContainer.layout.width / k_pitchAspect)
                                    < 1f
                            ),
                        () =>
                            $"Pitch layout must settle at {size}: main={main.layout}, left={leftSide.layout}, area={pitchArea.contentRect}, pitch={pitchContainer.layout}"
                    );

                    bool stacked = main.ClassListContains("stacked-layout");
                    Assert.IsFalse(pitchContainer.ClassListContains("fixed-pitch-layout"));
                    Assert.AreEqual(!stacked, main.ClassListContains("fixed-pitch-layout"));
                    Assert.Greater(pitchContainer.layout.height, 100f, $"Pitch collapsed at {size}");
                    Assert.AreEqual(pitchArea.contentRect.width, pitchContainer.layout.width, 1f);
                    Assert.AreEqual(pitchArea.contentRect.height, pitchContainer.layout.height, 1f);
                    Assert.AreEqual(StyleKeyword.Null, pitchContainer.style.width.keyword);
                    Assert.AreEqual(StyleKeyword.Null, pitchContainer.style.height.keyword);
                    if (stacked)
                    {
                        Assert.AreEqual(StyleKeyword.Null, leftSide.style.width.keyword);
                        if (main.ClassListContains("portrait-screen"))
                        {
                            Assert.AreEqual(
                                pitchContainer.layout.width / k_pitchAspect,
                                pitchContainer.layout.height,
                                1f,
                                $"Portrait pitch container must fit the displayed pitch at {size}"
                            );
                            Assert.AreEqual(
                                pitchContainer.worldBound.yMax,
                                rightSide.worldBound.yMin,
                                1f,
                                $"Portrait sidebar must follow the pitch without empty space at {size}"
                            );
                        }
                    }
                    else
                    {
                        Assert.AreEqual(
                            pitchArea.contentRect.height * k_pitchAspect,
                            leftSide.style.width.value.value,
                            1f
                        );
                        Assert.AreEqual(
                            pitchContainer.worldBound.xMax,
                            rightSide.worldBound.xMin,
                            1f,
                            $"Gap between pitch and sidebar at {size}"
                        );
                        Assert.AreEqual(pitchContainer.layout.width, leftSide.layout.width, 1f);
                        Assert.AreEqual(gameScreen.worldBound.xMax, rightSide.worldBound.xMax, 1f);
                        Assert.AreEqual(
                            gameScreen.contentRect.width - leftSide.layout.width,
                            rightSide.layout.width,
                            1f
                        );
                        float sidebarBaseWidth = size.x <= 2000f ? 312f : 336f;
                        Assert.GreaterOrEqual(rightSide.layout.width, sidebarBaseWidth - 1f);
                        if (size.x >= 1670f)
                            Assert.Greater(rightSide.layout.width, sidebarBaseWidth + 100f);
                    }
                }
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator Layout_ReflowsWithoutClippingAndPreservesFeedAcrossRotation()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var view = new MatchReportView();
            try
            {
                window.Show();
                var root = window.rootVisualElement;
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml"
                );
                tree.CloneTree(root);
                var main = root.Q("MainLayout");

                // Exercise the complete control layout without depending on the saved preview VM.
                TestUtils.ClearBindingsExcept(root);
                main.Q<Label>("TeamAName").text = "Miguel Canelas";
                main.Q<Label>("TeamBName").text = "Aleksander Kołodziej-Hajdo";
                // Test exact panel sizes without depending on the desktop's window size limits.
                main.style.position = Position.Absolute;
                var matchReportScroll = main.Q<ScrollView>("MatchReportScroll");
                view.Bind(matchReportScroll, new MatchReportSession());
                foreach (
                    var size in new[]
                    {
                        new Vector2(1670, 720),
                        new Vector2(1077, 721),
                        new Vector2(502, 725),
                        new Vector2(844, 390),
                        new Vector2(1077, 721),
                    }
                )
                {
                    main.style.width = size.x;
                    main.style.height = size.y;
                    ResponsiveLayout.ApplyLayout(main, size.x, size.y, size.x / size.y);
                    ResponsiveLayout.ApplyPitchLayout(main, k_pitchAspect);
                    yield return TestUtils.WaitForUi(
                        () =>
                            Mathf.Abs(main.layout.width - size.x) < 0.1f
                            && Mathf.Abs(main.layout.height - size.y) < 0.1f,
                        "The game layout must settle after resizing to " + size
                    );

                    // bool stacked = main.ClassListContains("stacked-layout");

                    // try
                    {
                        Assert.Greater(
                            main.Q("PitchContainer").resolvedStyle.height,
                            100,
                            $"Pitch collapsed at {size}"
                        );
                        Assert.GreaterOrEqual(
                            matchReportScroll.contentViewport.resolvedStyle.height,
                            20,
                            $"MessagePanel Feed collapsed at {size}"
                        );

                        bool portrait = size.x < size.y;
                        Assert.AreEqual(
                            // portrait ? DisplayStyle.None : DisplayStyle.Flex,
                            DisplayStyle.Flex,
                            main.Q("ActionsScroll").resolvedStyle.display,
                            $"ActionsScroll display style, portrait: {portrait} "
                        );

                        string closeBtnName;
                        float closeBtnExpectedHeight,
                            closeBtnExpectedWidth;
                        if (size.x / size.y >= 2f)
                        {
                            closeBtnName = "pitch-close-button";
                            closeBtnExpectedHeight = 24f;
                            closeBtnExpectedWidth = 24f;
                        }
                        else
                        {
                            closeBtnName = "GameCloseButton";
                            closeBtnExpectedHeight = 40f;
                            closeBtnExpectedWidth = 40f;
                        }
                        var close = main.Q<Button>(closeBtnName);

                        Assert.GreaterOrEqual(
                            close.resolvedStyle.height,
                            closeBtnExpectedHeight,
                            $"Expected {closeBtnName} height. Tested safe size: {size}"
                        );
                        Assert.GreaterOrEqual(
                            close.resolvedStyle.width,
                            closeBtnExpectedWidth,
                            $"Expected Close btn width. Tested safe size: {size}"
                        );
                        if (!portrait)
                        {
                            Assert.GreaterOrEqual(
                                main.Q("MoveButton").resolvedStyle.height,
                                44,
                                $"Expected Move btn height. Tested safe size: {size}"
                            );
                            Assert.GreaterOrEqual(
                                main.Q("MoveButton").resolvedStyle.width,
                                44,
                                $"Expected Move btn width. Tested safe size: {size}"
                            );
                        }
                        Assert.GreaterOrEqual(main.Q<Label>("PhaseText").resolvedStyle.fontSize, 16);
                    }
                    // catch (System.Exception ex)
                    // {
                    //     Debug.LogException(ex);
                    //     // add to y window title high
                    //     window.position = new Rect(window.position.x, window.position.y, size.x, size.y + 25);
                    //     window.Show();
                    //     window.Repaint();
                    //     EditorWindowCapture.Capture(
                    //         window,
                    //         "Layout_ReflowsWithoutClippingAndPreservesFeedAcrossRotation"
                    //     );
                    //     throw;
                    // }

                    if (matchReportScroll.contentContainer.childCount == 0)
                    {
                        view.Append(new MatchReportEntry(1, default, "A short opening event.", null));
                        for (int frame = 0; frame < 6; frame++)
                            yield return null;
                        Assert.Less(
                            matchReportScroll.contentContainer.ElementAt(0).worldBound.y
                                - matchReportScroll.contentViewport.worldBound.y,
                            12,
                            "Short feed should start at top"
                        );
                        for (int index = 0; index < 30; index++)
                            view.Append(
                                new MatchReportEntry(
                                    index,
                                    default,
                                    "Aleksander Kołodziej-Hajdo makes a high pass to zone 5.",
                                    null
                                )
                            );
                    }

                    // for (int frame = 0; frame < 6; frame++)
                    //     yield return null;
                    yield return TestUtils.WaitForBindings(() =>
                        Mathf.Abs(
                            matchReportScroll.verticalScroller.value - matchReportScroll.verticalScroller.highValue
                        ) <= 1f
                    );
                    Assert.AreEqual(31, matchReportScroll.contentContainer.childCount);
                    Assert.LessOrEqual(
                        Mathf.Abs(
                            matchReportScroll.verticalScroller.value - matchReportScroll.verticalScroller.highValue
                        ),
                        1,
                        $"Latest event should remain visible. Tested safe size: {size}"
                    );
                }
            }
            finally
            {
                view.Dispose();
                window.Close();
                Object.DestroyImmediate(window);
            }
        }
    }
}
