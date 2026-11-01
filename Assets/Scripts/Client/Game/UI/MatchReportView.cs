// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public sealed class MatchReportView : IDisposable
    {
        private ScrollView m_scroll;
        private bool m_scrollPending;
        private IVisualElementScheduledItem m_scrollTask;

        public void Bind(ScrollView scroll, MatchReportSession session)
        {
            Dispose();
            m_scroll = scroll;
            m_scroll.Clear();
            m_scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_scroll.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            foreach (var entry in session.Entries)
                Append(entry);
        }

        public void Append(MatchReportEntry entry)
        {
            if (m_scroll == null)
                return;
            var row = new VisualElement();
            row.AddToClassList("match-report-row");
            var turn = new Label($"{entry.Turn}'") { enableRichText = false };
            turn.AddToClassList("match-report-turn");
            row.Add(turn);
            if (!entry.Cue.Player.IsEmpty)
            {
                var marker = new Image { sprite = entry.TeamMarker };
                marker.AddToClassList("match-report-marker");
                row.Add(marker);
                // var number = new Label($"{entry.Cue.Player.PlayerIdx}.") { enableRichText = false };
                // number.AddToClassList("match-report-number");
                // row.Add(number);
            }
            var text = new Label(entry.Text) { enableRichText = false };
            text.AddToClassList("match-report-text");
            row.Add(text);
            row.RegisterCallback<GeometryChangedEvent>(OnRowGeometryChanged);
            m_scroll.Add(row);
            m_scrollPending = true;
            ScheduleScroll();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.newRect.size != evt.oldRect.size)
                m_scrollPending = true;
            if (m_scrollPending)
                ScheduleScroll();
        }

        private void OnRowGeometryChanged(GeometryChangedEvent evt)
        {
            var row = (VisualElement)evt.target;
            row.UnregisterCallback<GeometryChangedEvent>(OnRowGeometryChanged);
            m_scrollPending = true;
            ScheduleScroll();
        }

        private void ScheduleScroll()
        {
            m_scrollTask?.Pause();
            m_scrollTask = m_scroll.schedule.Execute(() =>
            {
                if (m_scroll?.panel == null || m_scroll.contentContainer.childCount == 0)
                    return;
                m_scroll.verticalScroller.value = Math.Max(0f, m_scroll.verticalScroller.highValue);
                m_scrollPending = false;
            });
        }

        public void Dispose()
        {
            m_scrollTask?.Pause();
            m_scrollTask = null;
            if (m_scroll != null)
            {
                foreach (var row in m_scroll.Children())
                    row.UnregisterCallback<GeometryChangedEvent>(OnRowGeometryChanged);
                m_scroll.contentContainer.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                m_scroll.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
            m_scroll = null;
            m_scrollPending = false;
        }
    }
}
