// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Foobbuteo.Client.Game.UI.Players;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class LineupDragTests
    {
        private const int SourcePlayerId = 101;
        private const int DestinationPlayerId = 112;
        private const float InitialScrollOffset = 100;
        private EditorWindow m_window;
        private VisualElement m_overlay;
        private VisualElement m_source;
        private VisualElement m_destination;
        private ScrollView m_scroll;
        private ScrollView m_otherScroll;
        private LineupDragSession m_session;
        private double m_time;
        private int? m_dropped;
        private bool m_canDrag;
        private Vector2 m_start;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_time = 0;
            m_dropped = null;
            m_canDrag = true;
            m_window = ScriptableObject.CreateInstance<EditorWindow>();
            m_window.Show();
            m_window.position = new Rect(30, 30, 650, 450);
            m_overlay = new VisualElement();
            m_overlay.style.width = 650;
            m_overlay.style.height = 450;
            m_window.rootVisualElement.Add(m_overlay);
            m_scroll = CreateScroll(0);
            m_otherScroll = CreateScroll(300);
            m_source = new VisualElement { userData = SourcePlayerId };
            m_source.style.position = Position.Absolute;
            m_source.style.left = 150;
            m_source.style.top = 150;
            m_source.style.width = 200;
            m_source.style.height = 80;
            m_source.AddToClassList("lineup-editor__target");
            m_scroll.Add(m_source);
            m_destination = new VisualElement { userData = DestinationPlayerId };
            m_destination.style.position = Position.Absolute;
            m_destination.style.left = 560;
            m_destination.style.top = 100;
            m_destination.style.width = 60;
            m_destination.style.height = 60;
            m_destination.AddToClassList("lineup-editor__target");
            m_overlay.Add(m_destination);
            m_session = new LineupDragSession(m_overlay, () => m_time);
            m_source.AddManipulator(
                new LineupDragManipulator(
                    m_session,
                    SourcePlayerId,
                    () => new Label("Player"),
                    () => m_canDrag,
                    (source, destination) => m_dropped = destination
                )
            );
            yield return TestUtils.WaitForUi(
                () => m_scroll.contentViewport.worldBound.width > 0 && m_destination.worldBound.width > 0,
                "Both scroll viewports and drop targets must have usable geometry."
            );
            m_scroll.scrollOffset = new Vector2(InitialScrollOffset, InitialScrollOffset);
            m_otherScroll.scrollOffset = new Vector2(InitialScrollOffset, InitialScrollOffset);
            m_start = m_source.worldBound.min + new Vector2(30, 30);
        }

        private ScrollView CreateScroll(float left)
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.position = Position.Absolute;
            scroll.style.left = left;
            scroll.style.width = 250;
            scroll.style.height = 350;
            scroll.contentContainer.style.width = 800;
            scroll.contentContainer.style.height = 800;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            m_overlay.Add(scroll);
            return scroll;
        }

        [TearDown]
        public void TearDown()
        {
            m_session?.Dispose();
            m_window.Close();
            UnityEngine.Object.DestroyImmediate(m_window);
        }

        [TestCase(60, 0)]
        [TestCase(0, 60)]
        [TestCase(60, 60)]
        public void SwipeBeforeHold_ScrollsWithoutStartingDrag(float x, float y)
        {
            Down();
            Move(m_start - new Vector2(x, y));
            m_time = LineupDragSession.TouchHoldSeconds + 1;
            m_session.UpdateGesture();

            Assert.IsNull(Ghost, "Moving before the hold must permanently choose scrolling for this gesture.");
            Assert.Greater(
                Vector2.Distance(new Vector2(InitialScrollOffset, InitialScrollOffset), m_scroll.scrollOffset),
                0,
                "The original swipe must reach the ScrollView and move its content."
            );
            Up(m_start - new Vector2(x, y));
            Assert.IsNull(m_dropped, "A scrolling gesture must never perform a player swap.");
        }

        [Test]
        public void HoldWithSmallJitter_ActivatesOnlyAfterDeadlineAndDrops()
        {
            Down();
            Move(m_start + Vector2.one);
            m_time = LineupDragSession.TouchHoldSeconds - 0.001;
            m_session.UpdateGesture();
            Assert.IsNull(Ghost, "A touch must remain pending before the hold deadline.");

            m_time = LineupDragSession.TouchHoldSeconds;
            m_session.UpdateGesture();
            Assert.IsNotNull(Ghost, "A stationary hold with small jitter must display the drag preview.");
            Assert.AreEqual(
                PickingMode.Ignore,
                Ghost.pickingMode,
                "The preview must allow picking targets underneath."
            );
            Assert.AreSame(
                m_overlay,
                m_overlay.panel.GetCapturingElement(PointerId.touchPointerIdBase),
                "The overlay must own active dragging above both ScrollViews."
            );
            Move(m_destination.worldBound.center);
            Assert.AreSame(
                m_destination,
                m_overlay.panel.Pick(m_destination.worldBound.center),
                "The destination must remain pickable underneath the active preview."
            );
            Up(m_destination.worldBound.center);

            Assert.AreEqual(
                DestinationPlayerId,
                m_dropped,
                "An activated touch drag must drop on the intended player."
            );
            Assert.IsNull(Ghost, "Dropping must remove the preview.");
        }

        [TestCase(60, 0)]
        [TestCase(0, 60)]
        [TestCase(60, 60)]
        public void ActiveDrag_BlocksBothScrollAxesAndWheelThenRestoresScrolling(float x, float y)
        {
            float deceleration = m_otherScroll.scrollDecelerationRate;
            Hold();
            Vector2 original = m_scroll.scrollOffset;
            Vector2 otherOriginal = m_otherScroll.scrollOffset;

            Move(m_start - new Vector2(x, y));
            using (
                var wheel = WheelEvent.GetPooled(
                    new Event
                    {
                        type = EventType.ScrollWheel,
                        delta = new Vector2(4, 4),
                        mousePosition = m_otherScroll.worldBound.center,
                    }
                )
            )
                m_otherScroll.SendEvent(wheel);
            Assert.AreEqual(
                original,
                m_scroll.scrollOffset,
                "Dragging must leave both source scroll offsets unchanged."
            );
            Assert.AreEqual(otherOriginal, m_otherScroll.scrollOffset, "The other list must also remain locked.");
            Up(Vector2.zero);

            Assert.AreEqual(
                deceleration,
                m_otherScroll.scrollDecelerationRate,
                "Ending a drag must restore inertia settings."
            );
            Assert.AreEqual(
                ScrollView.TouchScrollBehavior.Elastic,
                m_otherScroll.touchScrollBehavior,
                "Ending a drag must restore the original elastic scroll behavior."
            );
            Down();
            Move(m_start - new Vector2(x, y));
            Assert.Greater(
                Vector2.Distance(original, m_scroll.scrollOffset),
                0,
                "The next swipe must scroll normally after a drag has ended."
            );
            Up(m_start - new Vector2(x, y));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EarlyReleaseOrPointerCancel_DoesNotActivateLater(bool cancel)
        {
            Down();
            if (cancel)
                CancelPointer();
            else
                Up(m_start);
            m_time = LineupDragSession.TouchHoldSeconds + 1;
            m_session.UpdateGesture();
            Assert.IsNull(Ghost, "A released or canceled touch must not activate after its hold deadline.");
            Assert.IsNull(m_dropped, "A pending gesture must not perform a drop.");
        }

        [TestCase("cancel")]
        [TestCase("detach")]
        [TestCase("capture")]
        [TestCase("disabled")]
        [TestCase("dispose")]
        public void InterruptedDrag_RemovesPreviewAndRestoresScroll(string interruption)
        {
            float deceleration = m_scroll.scrollDecelerationRate;
            Hold();
            switch (interruption)
            {
                case "cancel":
                    CancelPointer();
                    break;
                case "detach":
                    m_source.RemoveFromHierarchy();
                    break;
                case "capture":
                    m_destination.CapturePointer(PointerId.touchPointerIdBase);
                    // Flush the pending capture change through normal event dispatch.
                    Move(m_start);
                    break;
                case "disabled":
                    m_canDrag = false;
                    m_session.UpdateGesture();
                    break;
                case "dispose":
                    m_session.Dispose();
                    break;
            }
            Assert.IsNull(Ghost, "Interrupting an active drag must remove its preview: " + interruption);
            Assert.AreEqual(
                deceleration,
                m_scroll.scrollDecelerationRate,
                "Interrupting a drag must restore scroll settings: " + interruption
            );
            Assert.IsNull(m_dropped, "Interrupted drags must not swap players: " + interruption);
        }

        [Test]
        public void DropClick_DoesNotReachModalBackdropButNextTapDoes()
        {
            int clicks = 0;
            m_overlay.RegisterCallback<ClickEvent>(evt => clicks++);
            Hold();
            using (var up = PointerUpEvent.GetPooled(Touch(m_start, TouchPhase.Ended), EventModifiers.None))
            {
                m_overlay.SendEvent(up);
                using var click = ClickEvent.GetPooled(up);
                m_overlay.SendEvent(click);
            }
            Assert.AreEqual(0, clicks, "The click generated by a completed drag must not reach the modal backdrop.");

            Down();
            using (var up = PointerUpEvent.GetPooled(Touch(m_start, TouchPhase.Ended), EventModifiers.None))
            {
                m_source.SendEvent(up);
                using var click = ClickEvent.GetPooled(up);
                m_overlay.SendEvent(click);
            }
            Assert.AreEqual(1, clicks, "The next ordinary tap must not remain suppressed after a drag.");
        }

        [Test]
        public void DisposedSession_CannotStartAnotherDrag()
        {
            m_session.Dispose();
            Down();
            m_time = LineupDragSession.TouchHoldSeconds;
            m_session.UpdateGesture();
            Assert.IsNull(Ghost, "Old player elements must not restart dragging after the controller unbinds.");
            Up(m_start);
        }

        [Test]
        public void MovementAfterHoldDeadline_ActivatesBeforeNextScheduledUpdate()
        {
            Down();
            m_time = LineupDragSession.TouchHoldSeconds;
            Move(m_destination.worldBound.center);
            Assert.IsNotNull(
                Ghost,
                "Movement after the hold deadline must activate dragging even between timer ticks."
            );
            Up(m_destination.worldBound.center);
            Assert.AreEqual(
                DestinationPlayerId,
                m_dropped,
                "The first movement after a completed hold must allow a drop."
            );
        }

        [Test]
        public void SecondaryTouchDuringHold_CancelsPendingDrag()
        {
            Down();
            Down(1);
            m_time = LineupDragSession.TouchHoldSeconds;
            m_session.UpdateGesture();
            Assert.IsNull(Ghost, "A second finger during the hold must cancel the pending drag.");
            Up(m_start, 1);
            Up(m_start);
        }

        [Test]
        public void SecondaryTouch_DoesNotMoveOrEndActiveDrag()
        {
            Hold();
            Down(1);
            Move(m_destination.worldBound.center, 1);
            Up(m_destination.worldBound.center, 1);
            Assert.IsNotNull(Ghost, "A second finger must not end the active player's drag.");
            Assert.IsNull(m_dropped, "A second finger must not drop the active player.");
            Move(m_destination.worldBound.center);
            Up(m_destination.worldBound.center);
            Assert.AreEqual(DestinationPlayerId, m_dropped, "The original finger must retain drag ownership.");
        }

        private VisualElement Ghost => m_overlay.Q(className: "lineup-editor__ghost");

        private void Hold()
        {
            Down();
            m_time += LineupDragSession.TouchHoldSeconds;
            m_session.UpdateGesture();
            Assert.IsNotNull(Ghost, "Holding the source player must activate the fixture's drag.");
        }

        private void Down(int finger = 0)
        {
            using var evt = PointerDownEvent.GetPooled(Touch(m_start, TouchPhase.Began, finger), EventModifiers.None);
            m_source.SendEvent(evt);
        }

        private void Move(Vector2 position, int finger = 0)
        {
            using var evt = PointerMoveEvent.GetPooled(Touch(position, TouchPhase.Moved, finger), EventModifiers.None);
            (m_overlay.panel.GetCapturingElement(evt.pointerId) ?? m_source).SendEvent(evt);
        }

        private void Up(Vector2 position, int finger = 0)
        {
            using var evt = PointerUpEvent.GetPooled(Touch(position, TouchPhase.Ended, finger), EventModifiers.None);
            (m_overlay.panel.GetCapturingElement(evt.pointerId) ?? m_source).SendEvent(evt);
        }

        private void CancelPointer()
        {
            using var evt = PointerCancelEvent.GetPooled(Touch(m_start, TouchPhase.Canceled), EventModifiers.None);
            m_source.SendEvent(evt);
        }

        private static Touch Touch(Vector2 position, TouchPhase phase, int finger = 0) =>
            new()
            {
                fingerId = finger,
                position = position,
                phase = phase,
            };
    }
}
