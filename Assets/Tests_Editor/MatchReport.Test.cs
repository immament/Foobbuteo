// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Client.Game;
using Imma.Foobbuteo.Client.Game.UI;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using Tests.Editor.Utils;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    [Category("MatchReport")]
    public class MatchReportTests
    {
        [TestCase(MatchCommentaryKind.DribbleAgainstOpponents, "Alex is trying to dribble 3 opponents.")]
        [TestCase(MatchCommentaryKind.AdvanceUnopposed, "Alex advances with the ball free of opposition.")]
        [TestCase(MatchCommentaryKind.LowPassToZone, "Alex makes a low pass to zone 5.")]
        [TestCase(MatchCommentaryKind.HighPassToZone, "Alex makes a high pass to zone 5.")]
        [TestCase(MatchCommentaryKind.Shoot, "Alex shoots...")]
        [TestCase(MatchCommentaryKind.ScoreGoal, "...GOAL!!! Alex scores!")]
        [TestCase(
            MatchCommentaryKind.WinBallConfrontation,
            "Alex won the ball. 2 teammates helped him against 3 opponents. (Zone: 5)"
        )]
        [TestCase(MatchCommentaryKind.Offside, "The referee blows the whistle. Offside!")]
        [TestCase(MatchCommentaryKind.InterceptPass, "Alex intercepts the pass.")]
        [TestCase(MatchCommentaryKind.GetBall, "Alex gets the ball.")]
        public void EnglishTemplates_FormatNamedValues(MatchCommentaryKind kind, string expected)
        {
            var cue = new MatchCommentaryCue(
                kind,
                new PlayerKey(TeamIndex.Home, 10),
                0,
                zone: 5,
                teammates: 2,
                opponents: 3
            );
            Assert.AreEqual(expected, MatchReportText.Format(new GameTextFormatter(), cue, "Alex"));
        }

        [Test]
        public void Formatter_SupportsOtherNamespacesAndDoesNotReinterpretPlayerText()
        {
            var formatter = new GameTextFormatter(key =>
                key == "lobby.welcome" ? "Welcome {player}. {player}!" : throw new KeyNotFoundException(key)
            );
            Assert.AreEqual(
                "Welcome {zone}. {zone}!",
                formatter.Format("lobby.welcome", new Dictionary<string, object> { ["player"] = "{zone}" })
            );
            Assert.Throws<ArgumentException>(() => formatter.Format("lobby.welcome", null));
            Assert.AreEqual("matchReport.getBall", GameTextKeys.MatchReport.GetBall);
        }

        [Test]
        public void TurnResult_RoundTripsCuesAndAnimation()
        {
            var expected = new MatchCommentaryCue(
                MatchCommentaryKind.WinBallConfrontation,
                new PlayerKey(TeamIndex.Away, 10),
                0,
                true,
                5,
                2,
                3
            );
            var result = new TurnResult(
                TurnResultTypeEnum.None,
                1,
                2,
                new MatchEvent[] { new BallMoveEvent(MatchEventType.HighPass, 9, Vector2.zero, Vector2.one) },
                expected.Player,
                PlayerKey.None,
                1f,
                new[] { expected }
            );
            using var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteNetworkSerializable(result);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out TurnResult actual);
            Assert.AreEqual(expected, actual.Commentary.Single());
            Assert.AreEqual(MatchEventType.HighPass, actual.Events.Single().EventType);
            Assert.AreEqual(2, actual.AwayScore);
        }

        [UnityTest]
        public IEnumerator Playback_EmitsCuesAroundInstantEvents()
        {
            var go = new GameObject("Commentary test");
            try
            {
                var playback = go.AddComponent<PlaybackManager>();
                var cues = new[]
                {
                    new MatchCommentaryCue(MatchCommentaryKind.Shoot, PlayerKey.None, 0),
                    new MatchCommentaryCue(MatchCommentaryKind.ScoreGoal, PlayerKey.None, 0, true),
                };
                var result = new TurnResult(
                    TurnResultTypeEnum.Goal,
                    1,
                    0,
                    new MatchEvent[] { new InstantEvent() },
                    PlayerKey.None,
                    PlayerKey.None,
                    0,
                    cues
                );
                var observed = new List<MatchCommentaryKind>();
                yield return playback.Play(
                    new TurnResultReadyMessage(9, 0, 0, result),
                    (turn, index, cue) =>
                    {
                        Assert.AreEqual(9, turn);
                        observed.Add(cue.Kind);
                    }
                );
                CollectionAssert.AreEqual(new[] { MatchCommentaryKind.Shoot, MatchCommentaryKind.ScoreGoal }, observed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Session_DeduplicatesAndClearsOnLeavingRoom()
        {
            var session = new MatchReportSession();
            var entry = new MatchReportEntry(9, default, "text", null);
            Assert.IsTrue(session.Add(0, entry));
            Assert.IsFalse(session.Add(0, entry));
            Assert.IsTrue(session.Add(1, entry));
            session.Clear();
            Assert.IsEmpty(session.Entries);
            Assert.IsTrue(session.Add(0, entry));
        }

        // [Test]
        // public void View_ReloadAndLayoutSwitchPreserveEntriesAndDisableRichText()
        // {
        //     var session = new MatchReportSession();
        //     session.Add(0, new MatchReportEntry(9,
        //         new MatchCommentaryCue(MatchCommentaryKind.GetBall, new PlayerKey(TeamIndex.Home, 10), 0),
        //         "<b>Alex</b> gets the ball.", null));
        //     var root = new VisualElement();
        //     var side = new VisualElement { name = "SidePanel" };
        //     var below = new VisualElement { name = "MatchReportBelowPitch" };
        //     var report = new VisualElement { name = "MessagePanel" };
        //     var scroll = new ScrollView();
        //     root.Add(side);
        //     root.Add(below);
        //     side.Add(report);
        //     report.Add(scroll);
        //     using var view = new MatchReportView();
        //     view.Bind(scroll, session);
        //     ResponsiveLayout.PlaceMatchReport(root, true, 1000);
        //     Assert.AreSame(below, report.parent);
        //     Assert.AreEqual(250f, below.style.height.value.value);
        //     ResponsiveLayout.PlaceMatchReport(root, false, 500);
        //     Assert.AreSame(side, report.parent);
        //     Assert.AreEqual(1, scroll.contentContainer.childCount);
        //     var reloaded = new ScrollView();
        //     view.Bind(reloaded, session);
        //     Assert.AreEqual(1, reloaded.contentContainer.childCount);
        //     Assert.IsTrue(reloaded.Query<Label>().ToList().All(label => !label.enableRichText));
        //     Assert.AreEqual("9'", reloaded.Query<Label>().ToList()[0].text);
        // }

        private sealed class InstantEvent : MatchEvent
        {
            public InstantEvent()
                : base(MatchEventType.FixPosition, 9, 0) { }
        }

        [UnityTest]
        public IEnumerator View_AutoScrollsAfterWrappedRowsAreLaidOut()
        {
            var window = ScriptableObject.CreateInstance<ReportTestWindow>();
            using var view = new MatchReportView();
            try
            {
                window.position = new Rect(50, 50, 360, 180);
                window.ShowUtility();
                var root = window.rootVisualElement;
                root.name = "MainLayout";
                root.styleSheets.Add(
                    AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI Toolkit/Game/GameRoom.style.uss")
                );
                var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "MatchReportScroll" };
                root.Add(scroll);
                view.Bind(scroll, new MatchReportSession());
                for (int index = 0; index < 30; index++)
                    view.Append(
                        new MatchReportEntry(
                            index,
                            default,
                            "A long player name gets the ball, with enough text to wrap onto another line.",
                            null
                        )
                    );
                for (int frame = 0; frame < 10; frame++)
                    yield return null;
                Assert.Greater(scroll.verticalScroller.highValue, 0);
                Assert.AreEqual(scroll.verticalScroller.highValue, scroll.scrollOffset.y, 1f);
                scroll.verticalScroller.value = 0;
                view.Append(
                    new MatchReportEntry(31, default, "Another message added while reading older entries.", null)
                );
                yield return TestUtils.WaitForBindings(() =>
                    Mathf.Abs(scroll.verticalScroller.highValue - scroll.scrollOffset.y) <= 1f
                );
                Assert.AreEqual(scroll.verticalScroller.highValue, scroll.scrollOffset.y, 1f);
            }
            finally
            {
                view.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private sealed class ReportTestWindow : EditorWindow { }

        // [UnityTest]
        // public IEnumerator GameRoomLayout_ReportFitsBothLayouts()
        // {
        //     var window = ScriptableObject.CreateInstance<ReportTestWindow>();
        //     using var view = new MatchReportView();
        //     try
        //     {
        //         window.ShowUtility();
        //         var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
        //             "Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml"
        //         );
        //         tree.CloneTree(window.rootVisualElement);
        //         var main = window.rootVisualElement.Q("MainLayout");
        //         var scroll = main.Q<ScrollView>("MatchReportScroll");
        //         view.Bind(scroll, new MatchReportSession());
        //         for (int index = 0; index < 50; index++)
        //             view.Append(new MatchReportEntry(index, default, "Player makes a high pass to zone 5.", null));
        //         foreach (var size in new[] { new Vector2(1600, 900), new Vector2(800, 1000), new Vector2(1000, 500) })
        //         {
        //             window.position = new Rect(20, 20, size.x, size.y);
        //             bool stacked = size.x / size.y <= 1.3f;
        //             main.EnableInClassList("stacked-layout", stacked);
        //             main.EnableInClassList("compact-landscape", !stacked);
        //             main.EnableInClassList("compact-header", size.x / size.y >= 2f);
        //             ResponsiveLayout.PlaceMatchReport(main, stacked, size.y);
        //             for (int frame = 0; frame < 10; frame++)
        //                 yield return null;
        //             var report = main.Q("MessagePanel");
        //             Assert.Greater(scroll.contentViewport.resolvedStyle.height, 0, $"No report space at {size}");
        //             Assert.LessOrEqual(report.worldBound.yMax, main.worldBound.yMax + 1, $"Report overflow at {size}");
        //             Assert.AreEqual(50, scroll.contentContainer.childCount);
        //         }
        //     }
        //     finally
        //     {
        //         view.Dispose();
        //         window.Close();
        //         UnityEngine.Object.DestroyImmediate(window);
        //     }
        // }
    }
}
