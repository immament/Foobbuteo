// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Client.UI.Modals;
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
        private readonly ModalHostController m_host;
        private readonly ModalPanelController m_modal;
        private readonly bool m_ownsHost;
        private Button m_close;

        internal PlayerListVM VM { get; } = new();

        public PlayerListUiController(
            Func<TeamIndex, FoobbuteoTeamState<ClientPlayerState>> getTeam,
            Func<TeamIndex, string> getTeamName,
            ClientImageCatalog images,
            ModalHostController host = null
        )
        {
            m_ownsHost = host == null;
            m_host = host ?? new ModalHostController(new ModalHostState());
            m_modal = new ModalPanelController(m_host, "team-player-list-overlay", () => true, _ => VM.Close());
            m_getTeam = getTeam;
            m_getTeamName = getTeamName;
            m_flags = new CountryFlagCache(images);
        }

        public void Bind(VisualElement root)
        {
            Unbind();
            m_root = root;
            if (m_ownsHost)
                m_host.Bind(root);
            m_overlay = root.Q("team-player-list-panel");
            m_overlay.dataSource = VM;
            m_overlay.dataSourcePath = new PropertyPath();
            m_close = m_overlay.Q<Button>("team-player-list-close");
            m_close.clicked += Close;
            AddTrigger("TeamAColor", TeamIndex.Home);
            AddTrigger("pitch-home-name", TeamIndex.Home);
            AddTrigger("TeamBColor", TeamIndex.Away);
            AddTrigger("pitch-away-name", TeamIndex.Away);
            m_table.Bind(m_overlay, VM.Table);
            Refresh();
            m_modal.Bind(root.Q("team-player-list-overlay"), m_close);
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
            if (!m_modal.TryOpen(opener))
                return;
            VM.Open(m_triggers[opener]);
            Refresh();
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

        private void Close() => m_modal.TryClose(ModalCloseReason.CloseButton);

        internal void Unbind()
        {
            m_modal.Unbind();
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
            m_overlay.dataSource = null;
            m_overlay = m_root = null;
            if (m_ownsHost)
                m_host.Unbind();
            m_close = null;
        }

        public void Dispose()
        {
            Unbind();
            m_modal.Dispose();
            if (m_ownsHost)
                m_host.Dispose();
            VM.Close();
        }
    }
}
