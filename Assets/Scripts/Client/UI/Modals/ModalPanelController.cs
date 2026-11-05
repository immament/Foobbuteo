// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Modals
{
    internal sealed class ModalPanelController : IDisposable
    {
        private readonly ModalHostController m_host;
        private readonly Action<ModalCloseReason> m_onClosed;
        internal Func<bool> CanDismiss { get; }
        internal ModalPanelState State { get; }
        internal VisualElement Root { get; private set; }
        private VisualElement m_initial;
        private VisualElement m_opener;
        private IVisualElementScheduledItem m_focus;
        private bool m_disposed;
        private bool m_restorePending;

        internal ModalPanelController(
            ModalHostController host,
            string panelName,
            Func<bool> canDismiss,
            Action<ModalCloseReason> onClosed
        )
        {
            m_host = host;
            CanDismiss = canDismiss;
            m_onClosed = onClosed;
            State = new ModalPanelState(host.State);
        }

        internal bool TryOpen(VisualElement opener = null) => !m_disposed && m_host.TryOpen(this, opener);

        internal bool TryClose(ModalCloseReason reason) =>
            ReferenceEquals(m_host.Active, this) && m_host.TryCloseActive(reason);

        internal void Bind(VisualElement instanceRoot, VisualElement initialFocus)
        {
            Unbind();
            Root = instanceRoot;
            Root.focusable = true;
            Root.tabIndex = -1;
            m_initial = initialFocus;
            Root.SetBinding(
                "style.display",
                new DataBinding
                {
                    dataSource = State,
                    dataSourcePath = new PropertyPath(nameof(ModalPanelState.IsVisible)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            Root.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            if (State.IsVisible)
                ScheduleFocus();
        }

        internal void Opened(VisualElement opener)
        {
            m_restorePending = false;
            m_opener = opener;
            ScheduleFocus();
        }

        internal void Closed(ModalCloseReason reason)
        {
            m_focus?.Pause();
            m_onClosed?.Invoke(reason);
            if (reason == ModalCloseReason.SessionEnded || Root?.panel == null)
                return;
            m_restorePending = true;
            m_focus = Root.schedule.Execute(RestoreFocus).StartingIn(1);
        }

        private void RestoreFocus()
        {
            if (!m_restorePending || Root?.panel == null || m_host.State.HasActivePanel)
                return;
            var target = m_opener;
            if (target?.panel == null && !string.IsNullOrEmpty(target?.name))
                target = Root.panel.visualTree.Q(target.name);
            if (!CanFocus(target))
                target = Root
                    .panel.visualTree.Query<VisualElement>()
                    .ToList()
                    .Find(element => !Root.Contains(element) && CanFocus(element));
            if (!CanFocus(target))
                return;
            m_restorePending = false;
            target.Focus();
        }

        private void ScheduleFocus()
        {
            m_focus?.Pause();
            if (Root != null)
                m_focus = Root.schedule.Execute(FocusInitial).StartingIn(1);
        }

        internal void RequestInitialFocus() => ScheduleFocus();

        internal void FocusInitial()
        {
            if (!State.IsVisible || Root?.panel == null)
                return;
            var target = CanFocus(m_initial) ? m_initial : Focusable().FirstOrDefault() ?? Root;
            if (
                target != null
                && (
                    Root.focusController.focusedElement is not VisualElement focused
                    || focused == Root
                    || !Root.Contains(focused)
                )
            )
                target.Focus();
        }

        internal void MoveFocus(int direction)
        {
            var elements = Focusable();
            if (elements.Count == 0)
                return;
            int current = elements.IndexOf(Root.focusController.focusedElement as VisualElement);
            int next =
                current < 0
                    ? (direction > 0 ? 0 : elements.Count - 1)
                    : (current + direction + elements.Count) % elements.Count;
            elements[next].Focus();
        }

        private List<VisualElement> Focusable() => Root?.Query<VisualElement>().ToList().FindAll(CanFocus) ?? new();

        private static bool CanFocus(VisualElement element) =>
            element?.panel != null
            && element.focusable
            && element.tabIndex >= 0
            && element.enabledInHierarchy
            && element.resolvedStyle.visibility == Visibility.Visible
            && element.worldBound.width > 0
            && element.worldBound.height > 0;

        private void OnGeometry(GeometryChangedEvent evt)
        {
            if (State.IsVisible)
                FocusInitial();
            else
                RestoreFocus();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target == Root)
                Unbind();
        }

        internal void Unbind()
        {
            m_focus?.Pause();
            m_focus = null;
            m_restorePending = false;
            if (Root != null)
            {
                Root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
                Root.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                Root.ClearBinding("style.display");
            }
            Root = m_initial = null;
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            m_host.Remove(this);
            Unbind();
        }
    }
}
