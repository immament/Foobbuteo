// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerListUiController : IDisposable
    {
        private readonly Func<TeamIndex, FoobbuteoTeamState<ClientPlayerState>> m_getTeam;
        private readonly Func<TeamIndex, string> m_getTeamName;
        private readonly CountryFlagCache m_flags;
        private readonly PlayerTableView m_table = new();
        private readonly Dictionary<VisualElement, TeamIndex> m_triggers = new();
        private VisualElement m_root;
        private VisualElement m_overlay;
        private VisualElement m_background;
        private Button m_close;
        private string m_openerName;
        private IVisualElementScheduledItem m_focus;

        internal PlayerListVM VM { get; } = new();

        public PlayerListUiController(
            Func<TeamIndex, FoobbuteoTeamState<ClientPlayerState>> getTeam,
            Func<TeamIndex, string> getTeamName,
            ClientImageCatalog images
        )
        {
            m_getTeam = getTeam;
            m_getTeamName = getTeamName;
            m_flags = new CountryFlagCache(images);
        }

        public void Bind(VisualElement root)
        {
            Unbind();
            m_root = root;
            m_overlay = root.Q("team-player-list-overlay");
            m_overlay.dataSource = VM;
            m_overlay.dataSourcePath = new PropertyPath();
            m_background = root.Q("game-room-content");
            m_background.SetBinding(
                nameof(VisualElement.enabledSelf),
                new DataBinding
                {
                    dataSource = VM,
                    dataSourcePath = new PropertyPath(nameof(PlayerListVM.BackgroundEnabled)),
                    bindingMode = BindingMode.ToTarget,
                    updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                }
            );
            m_close = m_overlay.Q<Button>("team-player-list-close");
            m_close.clicked += Close;
            m_overlay.RegisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_overlay.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            AddTrigger("TeamAColor", TeamIndex.Home);
            AddTrigger("MobileTeamAColor", TeamIndex.Home);
            AddTrigger("TeamBColor", TeamIndex.Away);
            AddTrigger("MobileTeamBColor", TeamIndex.Away);
            m_table.Bind(m_overlay, VM.Table);
            Refresh();
            if (VM.Team.HasValue)
                FocusWhenReady(m_close);
        }

        private void AddTrigger(string name, TeamIndex team)
        {
            var trigger = m_root.Q(name);
            trigger.focusable = true;
            trigger.tabIndex = 0;
            trigger.tooltip = "View players";
            trigger.RegisterCallback<ClickEvent>(OnTeamClick);
            trigger.RegisterCallback<KeyDownEvent>(OnTeamKey);
            m_triggers.Add(trigger, team);
        }

        private void OnTeamClick(ClickEvent evt) => Open((VisualElement)evt.currentTarget);

        private void OnTeamKey(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space)
                return;
            Open((VisualElement)evt.currentTarget);
            evt.StopPropagation();
        }

        private void Open(VisualElement opener)
        {
            m_openerName = opener.name;
            VM.Open(m_triggers[opener]);
            Refresh();
            FocusWhenReady(m_close);
        }

        public void Refresh()
        {
            if (!VM.Team.HasValue)
                return;
            var team = m_getTeam(VM.Team.Value);
            VM.SetTitle(m_getTeamName(VM.Team.Value) + " — Players");
            VM.Table.SetRows(
                team?.Players?.Where(player => player != null)
                    .Select(player => new PlayerListRowVM(player, player.Slot.PlayerIdx, m_flags.Get(player.CountryId)))
                    ?? Enumerable.Empty<PlayerListRowVM>()
            );
        }

        private void Close()
        {
            VM.Close();
            var opener = m_root.Q(m_openerName);
            if (
                opener != null
                && opener.resolvedStyle.display != DisplayStyle.None
                && opener.worldBound.width > 0
                && opener.worldBound.height > 0
            )
                FocusWhenReady(opener);
            else
                FocusWhenReady(
                    m_triggers.Keys.FirstOrDefault(element =>
                        element.worldBound.width > 0 && element.worldBound.height > 0
                    )
                );
        }

        private void FocusWhenReady(VisualElement target)
        {
            m_focus?.Pause();
            if (target == null)
                return;
            m_focus = m_root
                .schedule.Execute(() =>
                {
                    if (m_overlay.resolvedStyle.display != VM.Display || !target.enabledInHierarchy)
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
            if (evt.keyCode != KeyCode.Escape)
                return;
            Close();
            evt.StopPropagation();
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
            m_table.Dispose();
            foreach (var trigger in m_triggers.Keys)
            {
                trigger.UnregisterCallback<ClickEvent>(OnTeamClick);
                trigger.UnregisterCallback<KeyDownEvent>(OnTeamKey);
            }
            m_triggers.Clear();
            if (m_overlay == null)
                return;
            m_close.clicked -= Close;
            m_overlay.UnregisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            m_background.ClearBinding(nameof(VisualElement.enabledSelf));
            m_background.SetEnabled(true);
            m_overlay.dataSource = null;
            m_overlay = m_background = m_root = null;
            m_close = null;
        }

        public void Dispose()
        {
            Unbind();
            VM.Close();
        }
    }
}
