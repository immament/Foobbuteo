// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Imma.Foobbuteo.Client.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class MvMLineupLayout : IDisposable
    {
        private const float PitchAspectRatio = 1.1f;
        private readonly VisualElement m_root;
        private readonly VisualElement m_overlay;
        private readonly VisualElement m_pitchFrame;
        private readonly VisualElement m_pitch;
        private readonly MvMLineupVM m_vm;
        private readonly Func<VisualElement, UiViewportMetrics?> m_readViewport;
        private bool m_vertical;

        internal bool IsSmall { get; private set; }

        internal MvMLineupLayout(
            VisualElement root,
            VisualElement overlay,
            MvMLineupVM vm,
            Func<VisualElement, UiViewportMetrics?> readViewport = null
        )
        {
            m_root = root;
            m_overlay = overlay;
            m_vm = vm;
            m_readViewport = readViewport ?? ReadViewport;
            m_pitchFrame = overlay.Q("mvm-lineup-pitch-frame");
            m_pitch = overlay.Q("mvm-lineup-pitch");
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometry);
            m_pitchFrame.RegisterCallback<GeometryChangedEvent>(OnPitchGeometry);
            vm.propertyChanged += OnPresentationChanged;
            Refresh();
        }

        internal void Refresh()
        {
            var metrics = m_readViewport(m_root);
            if (metrics.HasValue)
                ApplyViewport(metrics.Value);
        }

        private static UiViewportMetrics? ReadViewport(VisualElement root) =>
            UiViewportMetrics.TryRead(root, out var metrics) ? metrics : null;

        internal void ApplyViewport(UiViewportMetrics metrics)
        {
            var layout = metrics.Layout;
            IsSmall = layout.IsSmall;
            m_vertical = layout.IsNarrow || layout.IsPortrait;
            m_overlay.EnableInClassList("lineup-editor--small", IsSmall);
            m_overlay.EnableInClassList("lineup-editor--compact", m_vertical);
            m_overlay.EnableInClassList("lineup-editor--short", layout.IsShort);
            m_overlay.style.left = 0;
            m_overlay.style.top = 0;
            m_overlay.style.width = layout.LogicalSize.x;
            m_overlay.style.height = layout.LogicalSize.y;
            m_overlay.style.scale = new Scale(
                new Vector3(metrics.PanelUnitsPerLogicalUnit.x, metrics.PanelUnitsPerLogicalUnit.y, 1f)
            );
            UpdateSectionLayout();
        }

        private void OnRootGeometry(GeometryChangedEvent evt) => Refresh();

        private void OnPitchGeometry(GeometryChangedEvent evt) => FitPitch();

        private void OnPresentationChanged(object sender, BindablePropertyChangedEventArgs evt)
        {
            if (
                evt.propertyName == nameof(MvMLineupVM.StartingExpanded)
                || evt.propertyName == nameof(MvMLineupVM.ReservesExpanded)
            )
                UpdateSectionLayout();
        }

        private void UpdateSectionLayout()
        {
            m_overlay.EnableInClassList(
                "lineup-editor--both-lists-open",
                !m_vertical && m_vm.StartingExpanded && m_vm.ReservesExpanded
            );
            m_overlay.EnableInClassList(
                "lineup-editor--lists-collapsed",
                !m_vm.StartingExpanded && !m_vm.ReservesExpanded
            );
        }

        private void FitPitch()
        {
            Vector2 available = m_pitchFrame.contentRect.size;
            if (!UiScreenLayout.IsValidSize(available))
                return;
            float width = Mathf.Min(available.x, available.y * PitchAspectRatio);
            m_pitch.style.width = width;
            m_pitch.style.height = width / PitchAspectRatio;
        }

        public void Dispose()
        {
            m_root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometry);
            m_pitchFrame.UnregisterCallback<GeometryChangedEvent>(OnPitchGeometry);
            m_vm.propertyChanged -= OnPresentationChanged;
        }
    }
}
