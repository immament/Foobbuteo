// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Modals
{
    internal sealed class ModalHostController : IDisposable
    {
        internal ModalHostState State { get; }
        internal ModalPanelController Active { get; private set; }
        private VisualElement m_root;
        private VisualElement m_content;
        private VisualElement m_layer;
        private VisualElement m_backdrop;
        private VisualElement m_context;
        private bool m_contentWasEnabled;
        private bool m_disposed;

        internal ModalHostController(ModalHostState state) => State = state;

        internal void Bind(VisualElement root)
        {
            Unbind();
            m_root = root;
            m_content = root.Q("screen-content");
            m_layer = root.Q("modal-layer");
            m_backdrop = root.Q("modal-backdrop");
            m_context = root.Q("contextual-popup-layer");
            m_contentWasEnabled = m_content.enabledSelf;
            m_content.SetBinding(
                nameof(VisualElement.enabledSelf),
                new DataBinding
                {
                    dataSource = State,
                    dataSourcePath = new PropertyPath(nameof(ModalHostState.BackgroundEnabled)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            m_layer.SetBinding(
                "style.display",
                new DataBinding
                {
                    dataSource = State,
                    dataSourcePath = new PropertyPath(nameof(ModalHostState.HasActivePanel)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            m_context?.SetBinding(
                "style.display",
                new DataBinding
                {
                    dataSource = State,
                    dataSourcePath = new PropertyPath(nameof(ModalHostState.BackgroundEnabled)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            m_backdrop.RegisterCallback<ClickEvent>(OnBackdrop);
            root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnEscape);
            root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            root.RegisterCallback<FocusInEvent>(OnFocus);
        }

        internal bool TryOpen(ModalPanelController panel, VisualElement opener)
        {
            if (m_disposed || (Active != null && !ReferenceEquals(Active, panel)))
                return false;
            if (ReferenceEquals(Active, panel))
                return true;
            Active = panel;
            State.SetActive(panel.State);
            panel.Opened(opener);
            return true;
        }

        internal bool TryCloseActive(ModalCloseReason reason)
        {
            if (Active == null || (reason != ModalCloseReason.Completed && !Active.CanDismiss()))
                return false;
            Close(reason);
            return true;
        }

        internal void ForceCloseActive(ModalCloseReason reason)
        {
            if (Active != null)
                Close(reason);
        }

        private void Close(ModalCloseReason reason)
        {
            var panel = Active;
            Active = null;
            State.SetActive(null);
            panel.Closed(reason);
        }

        internal void Remove(ModalPanelController panel)
        {
            if (ReferenceEquals(Active, panel))
                ForceCloseActive(ModalCloseReason.SessionEnded);
        }

        private void OnBackdrop(ClickEvent evt)
        {
            if (evt.target == m_backdrop)
            {
                TryCloseActive(ModalCloseReason.Backdrop);
                evt.StopPropagation();
            }
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (Active == null)
                return;
            if (evt.keyCode == KeyCode.Tab)
            {
                Active.MoveFocus(evt.shiftKey ? -1 : 1);
                evt.StopPropagation();
                m_root.focusController.IgnoreEvent(evt);
            }
        }

        private void OnEscape(KeyDownEvent evt)
        {
            if (Active == null || evt.keyCode != KeyCode.Escape)
                return;
            TryCloseActive(ModalCloseReason.Cancel);
            evt.StopPropagation();
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            if (Active == null)
                return;
            TryCloseActive(ModalCloseReason.Cancel);
            evt.StopPropagation();
        }

        private void OnFocus(FocusInEvent evt)
        {
            if (
                Active?.Root != null
                && evt.target is VisualElement element
                && !Active.Root.Contains(element)
                && element != Active.Root
            )
                Active.FocusInitial();
        }

        internal void Unbind()
        {
            if (m_root == null)
                return;
            m_backdrop.UnregisterCallback<ClickEvent>(OnBackdrop);
            m_root.UnregisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            m_root.UnregisterCallback<KeyDownEvent>(OnEscape);
            m_root.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            m_root.UnregisterCallback<FocusInEvent>(OnFocus);
            m_content.ClearBinding(nameof(VisualElement.enabledSelf));
            m_content.SetEnabled(m_contentWasEnabled);
            m_layer.ClearBinding("style.display");
            m_context?.ClearBinding("style.display");
            m_root = m_content = m_layer = m_backdrop = m_context = null;
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            ForceCloseActive(ModalCloseReason.SessionEnded);
            Unbind();
        }
    }
}
