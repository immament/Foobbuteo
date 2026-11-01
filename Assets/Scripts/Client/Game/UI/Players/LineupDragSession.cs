// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class LineupDragSession : IDisposable
    {
        internal const double TouchHoldSeconds = 0.35;
        private const float DragThresholdUnits = 8;
        private const int GestureUpdateMilliseconds = 16;
        private const float TouchPreviewOffsetUnits = 72;
        private const float MousePreviewOffsetUnits = 24;
        private readonly VisualElement m_overlay;
        private readonly Func<double> m_time;
        private readonly List<(
            ScrollView Scroll,
            float Deceleration,
            ScrollView.TouchScrollBehavior Behavior
        )> m_scrolls = new();
        private readonly IVisualElementScheduledItem m_update;
        private LineupDragManipulator m_source;
        private VisualElement m_ghost;
        private VisualElement m_hover;
        private Vector2 m_start;
        private Vector2 m_position;
        private double m_holdUntil;
        private int m_pointer = -1;
        private int m_suppressedClickPointer = -1;
        private bool m_touch;
        private bool m_disposed;

        internal LineupDragSession(VisualElement overlay, Func<double> time = null)
        {
            m_overlay = overlay;
            m_time = time ?? (() => Time.realtimeSinceStartupAsDouble);
            overlay.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCancelEvent>(OnCancel, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCaptureEvent>(OnCapture, TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            overlay.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            overlay.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            overlay.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            m_update = overlay.schedule.Execute(UpdateGesture).Every(GestureUpdateMilliseconds);
            m_update.Pause();
        }

        internal void TryBegin(LineupDragManipulator source, PointerDownEvent evt)
        {
            if (m_disposed || evt.button != 0 || m_source != null || !source.CanDrag)
                return;
            bool touch = evt.pointerType == PointerType.touch;
            if (touch && (!evt.isPrimary || m_overlay.panel.GetCapturingElement(evt.pointerId) != null))
                return;
            m_source = source;
            m_pointer = evt.pointerId;
            m_touch = touch;
            m_position = evt.position;
            m_start = m_overlay.WorldToLocal(m_position);
            m_holdUntil = m_time() + TouchHoldSeconds;
            m_update.Resume();
            if (!touch)
            {
                m_overlay.CapturePointer(m_pointer);
                evt.StopPropagation();
            }
        }

        private void OnDown(PointerDownEvent evt)
        {
            m_suppressedClickPointer = -1;
            if (m_ghost != null)
                evt.StopImmediatePropagation();
            else if (m_source != null && evt.pointerId != m_pointer)
                Cancel();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (m_source == null)
                return;
            bool dragging = m_ghost != null;
            if (evt.pointerId == m_pointer)
            {
                m_position = evt.position;
                if (!m_source.CanDrag)
                    Cancel();
                else if (m_ghost == null)
                {
                    float distance = Vector2.Distance(m_start, m_overlay.WorldToLocal(m_position));
                    if (m_touch && m_time() >= m_holdUntil)
                        Activate();
                    else if (m_touch && distance >= DragThresholdUnits)
                        Cancel();
                    else if (!m_touch && distance >= DragThresholdUnits)
                        Activate();
                    else
                        UpdateGesture();
                }
                if (m_ghost != null)
                    UpdatePreview();
            }
            if (dragging || m_ghost != null)
                evt.StopImmediatePropagation();
        }

        internal void UpdateGesture()
        {
            if (m_source == null)
                return;
            if (!m_source.CanDrag)
            {
                Cancel();
                return;
            }
            if (m_touch && m_ghost == null && m_time() >= m_holdUntil)
                Activate();
        }

        private void Activate()
        {
            // Capture above the ScrollViews so their capture callbacks cannot attach scroll handlers to the drag owner.
            m_overlay.CapturePointer(m_pointer);
            m_overlay
                .Query<ScrollView>()
                .ForEach(scroll =>
                {
                    m_scrolls.Add((scroll, scroll.scrollDecelerationRate, scroll.touchScrollBehavior));
                    scroll.scrollDecelerationRate = 0;
                    scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
                });
            m_ghost = m_source.CreatePreview();
            m_ghost.pickingMode = PickingMode.Ignore;
            m_ghost.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
            m_ghost.AddToClassList("lineup-editor__ghost");
            m_ghost.RegisterCallback<GeometryChangedEvent>(OnPreviewGeometry);
            m_overlay.Add(m_ghost);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var offset = new Vector2(0, m_touch ? -TouchPreviewOffsetUnits : -MousePreviewOffsetUnits);
            var position = m_overlay.WorldToLocal(m_position) + offset;
            float halfWidth = m_ghost.layout.width / 2;
            float height = m_ghost.layout.height;
            if (!float.IsNaN(halfWidth) && !float.IsNaN(height))
            {
                position.x = Mathf.Clamp(
                    position.x,
                    halfWidth,
                    Mathf.Max(halfWidth, m_overlay.layout.width - halfWidth)
                );
                position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, m_overlay.layout.height - height));
            }
            m_ghost.style.translate = new StyleTranslate(position);
            m_hover?.RemoveFromClassList("lineup-editor__target--hover");
            m_hover = FindTarget(m_position);
            m_hover?.AddToClassList("lineup-editor__target--hover");
        }

        private void OnPreviewGeometry(GeometryChangedEvent evt) => UpdatePreview();

        private VisualElement FindTarget(Vector2 position)
        {
            var picked = m_overlay.panel?.Pick(position);
            while (picked != null && picked != m_overlay)
            {
                if (picked.ClassListContains("lineup-editor__target"))
                    return picked;
                picked = picked.parent;
            }
            return null;
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != m_pointer)
            {
                if (m_ghost != null)
                    evt.StopImmediatePropagation();
                return;
            }
            var source = m_source;
            bool dragging = m_ghost != null;
            int? destination = dragging ? FindTarget(evt.position)?.userData as int? : null;
            bool canDrop = source.CanDrag;
            Cancel();
            if (dragging)
            {
                m_suppressedClickPointer = evt.pointerId;
                evt.StopImmediatePropagation();
            }
            if (canDrop && destination.HasValue)
                source.Drop(destination.Value);
        }

        private void OnCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != m_pointer)
                return;
            bool dragging = m_ghost != null;
            Cancel();
            if (dragging)
                evt.StopImmediatePropagation();
        }

        private void OnCapture(PointerCaptureEvent evt)
        {
            if (evt.pointerId == m_pointer && evt.target != m_overlay)
                Cancel();
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == m_pointer && evt.target == m_overlay)
                Cancel();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (m_ghost != null)
                evt.StopImmediatePropagation();
            else
                Cancel();
        }

        private void OnClick(ClickEvent evt)
        {
            // Pointer capture can make the generated click target the modal backdrop after a drop.
            if (evt.pointerId == m_suppressedClickPointer)
            {
                m_suppressedClickPointer = -1;
                evt.StopImmediatePropagation();
            }
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target == m_overlay)
                Cancel();
        }

        internal void Cancel(LineupDragManipulator source)
        {
            if (m_source == source)
                Cancel();
        }

        internal void Cancel()
        {
            int pointer = m_pointer;
            m_pointer = -1;
            m_source = null;
            m_update.Pause();
            m_hover?.RemoveFromClassList("lineup-editor__target--hover");
            m_hover = null;
            if (m_ghost != null)
            {
                m_ghost.UnregisterCallback<GeometryChangedEvent>(OnPreviewGeometry);
                m_ghost.RemoveFromHierarchy();
            }
            m_ghost = null;
            foreach (var state in m_scrolls)
            {
                state.Scroll.scrollDecelerationRate = state.Deceleration;
                state.Scroll.touchScrollBehavior = state.Behavior;
            }
            m_scrolls.Clear();
            if (pointer >= 0 && m_overlay.HasPointerCapture(pointer))
                m_overlay.ReleasePointer(pointer);
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            Cancel();
            m_overlay.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<PointerCancelEvent>(OnCancel, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<PointerCaptureEvent>(OnCapture, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            m_overlay.UnregisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }
    }
}
