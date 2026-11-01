// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class GameSettingsUiController : IDisposable
    {
        private readonly GameSettingsVM m_vm;
        private VisualElement m_overlay;
        private SliderInt m_slider;
        private Button m_open;
        private Button m_close;
        private IVisualElementScheduledItem m_focus;

        internal GameSettingsUiController(GameSettingsVM vm) => m_vm = vm;

        public void Bind(VisualElement root)
        {
            Unbind();
            m_overlay = root.Q("game-settings-overlay");
            m_overlay.dataSource = m_vm;
            m_overlay.dataSourcePath = new PropertyPath();
            m_slider = root.Q<SliderInt>("sound-volume-slider");
            m_open = root.Q<Button>("open-settings-btn");
            m_close = root.Q<Button>("close-game-settings-btn");
            root.Q("MainLayout")
                .SetBinding(
                    nameof(VisualElement.enabledSelf),
                    new DataBinding
                    {
                        dataSource = m_vm,
                        dataSourcePath = new PropertyPath(nameof(GameSettingsVM.BackgroundEnabled)),
                        bindingMode = BindingMode.ToTarget,
                        updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                    }
                );
            m_open.clicked += Open;
            m_close.clicked += Close;
            m_overlay.RegisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_overlay.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            if (m_vm.Display == DisplayStyle.Flex)
                FocusWhenReady(m_slider);
        }

        private void Open()
        {
            m_vm.SetOpen(true);
            FocusWhenReady(m_slider);
        }

        private void Close()
        {
            m_vm.SetOpen(false);
            FocusWhenReady(m_open);
        }

        private void FocusWhenReady(VisualElement target)
        {
            m_focus?.Pause();
            m_focus = m_overlay
                .schedule.Execute(() =>
                {
                    if (m_overlay.resolvedStyle.display != m_vm.Display || !target.enabledInHierarchy)
                        return;
                    target.Focus();
                    m_focus.Pause();
                })
                .Every(1);
        }

        private void OnBackdrop(ClickEvent evt)
        {
            if (evt.target == m_overlay)
                Close();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target == m_overlay)
                Unbind();
        }

        private void Unbind()
        {
            m_focus?.Pause();
            m_focus = null;
            if (m_overlay == null)
                return;
            m_open.clicked -= Open;
            m_close.clicked -= Close;
            m_overlay.UnregisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            m_overlay = null;
            m_slider = null;
            m_open = m_close = null;
        }

        public void Dispose() => Unbind();
    }
}
