// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    internal sealed class NewGameUiController : IDisposable
    {
        private readonly NewGameVM m_viewModel;
        private bool m_disposed;
        private VisualElement m_modal;
        private VisualElement m_background;
        private DropdownField m_start;
        private Button m_create;
        private Button m_cancel;
        private Button m_close;
        private Button m_openButton;
        private IVisualElementScheduledItem m_focusTask;

        public NewGameUiController(ILobbyApiClient api, Func<bool> canCreate, Func<GameUser> getUser = null)
        {
            m_viewModel = new NewGameVM(api, canCreate, getUser);
            m_viewModel.RequestCompleted += OnCompleted;
        }

        public void Bind(VisualElement root)
        {
            if (m_disposed)
                throw new ObjectDisposedException(nameof(NewGameUiController));
            Unbind();
            m_modal = root.Q("new-game-modal");
            m_modal.tabIndex = -1;
            m_background = root.Q("main-view__lobby");
            m_start = root.Q<DropdownField>("new-game-start");
            m_create = root.Q<Button>("new-game-create");
            m_cancel = root.Q<Button>("new-game-cancel");
            m_close = root.Q<Button>("new-game-close");
            m_openButton = root.Q<Button>("main-view__new-game-btn");
            m_start.choices = new(NewGameVM.StartChoices);
            root.Q<DropdownField>("new-game-turns").choices = new(NewGameVM.TurnChoices);
            root.Q<DropdownField>("new-game-home").choices = new(NewGameVM.ColorChoices);
            root.Q<DropdownField>("new-game-away").choices = new(NewGameVM.ColorChoices);
            root.Q<DropdownField>("new-game-position").choices = new(NewGameVM.PositionChoices);
            var team = root.Q<DropdownField>("new-game-team");
            team.formatSelectedValueCallback = m_viewModel.FormatTeamChoice;
            team.formatListItemCallback = m_viewModel.FormatTeamChoice;
            root.Q<Image>("new-game-home-swatch").image = Texture2D.whiteTexture;
            root.Q<Image>("new-game-away-swatch").image = Texture2D.whiteTexture;
            m_modal.dataSource = m_viewModel;
            m_modal.dataSourcePath = new PropertyPath();
            m_background.SetBinding(
                nameof(VisualElement.enabledSelf),
                new DataBinding
                {
                    dataSource = m_viewModel,
                    dataSourcePath = new PropertyPath(nameof(NewGameVM.BackgroundEnabled)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            m_create.clicked += Submit;
            m_cancel.clicked += Cancel;
            m_close.clicked += Cancel;
            m_modal.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_modal.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            if (m_viewModel.IsOpen)
                FocusWhenReady(m_viewModel.IsPending ? m_modal : m_start);
        }

        public void Open()
        {
            if (m_viewModel.Open())
                FocusWhenReady(m_start);
        }

        public void Reset() => m_viewModel.Reset();

        public void Refresh() => m_viewModel.RefreshAvailability();

        private void Submit()
        {
            m_viewModel.Submit();
            if (m_viewModel.IsPending)
                FocusWhenReady(m_modal);
        }

        private void OnCompleted(bool success) => FocusWhenReady(success ? m_openButton : m_create);

        private void Cancel()
        {
            if (m_viewModel.Cancel())
                FocusWhenReady(m_openButton);
        }

        private void FocusWhenReady(VisualElement target)
        {
            m_focusTask?.Pause();
            if (m_modal == null || target == null)
                return;
            m_focusTask = m_modal
                .schedule.Execute(() =>
                {
                    bool visible = m_modal.resolvedStyle.display != DisplayStyle.None;
                    if (
                        visible != m_viewModel.IsOpen
                        || m_background.enabledSelf != m_viewModel.BackgroundEnabled
                        || !target.enabledInHierarchy
                    )
                        return;
                    target.Focus();
                    m_focusTask.Pause();
                })
                .Every(1);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Cancel();
                evt.StopPropagation();
            }
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target == m_modal)
                Unbind();
        }

        public void Unbind()
        {
            m_focusTask?.Pause();
            m_focusTask = null;
            if (m_modal == null)
                return;
            m_create.clicked -= Submit;
            m_cancel.clicked -= Cancel;
            m_close.clicked -= Cancel;
            m_modal.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_modal.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            m_background = null;
            m_modal = null;
            m_start = null;
            m_create = m_cancel = m_close = m_openButton = null;
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            Unbind();
            m_viewModel.RequestCompleted -= OnCompleted;
            m_viewModel.Dispose();
        }
    }
}
