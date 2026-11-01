// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    internal sealed class MvMJoinUiController : IDisposable
    {
        private readonly MvMJoinVM m_vm;
        private Button m_join;

        internal MvMJoinUiController(IClientGameRoomNB api, ClientGameState game, Func<GameUser> getUser) =>
            m_vm = new MvMJoinVM(api, game, getUser);

        internal void Bind(VisualElement root)
        {
            Unbind();
            var panel = root?.Q("mvm-join-panel");
            if (panel == null)
                return;
            panel.dataSource = m_vm;
            var team = panel.Q<DropdownField>("mvm-join-team");
            team.formatSelectedValueCallback = m_vm.FormatTeamChoice;
            team.formatListItemCallback = m_vm.FormatTeamChoice;
            m_join = panel.Q<Button>("mvm-join-button");
            m_join.clicked += m_vm.Submit;
            Refresh();
        }

        internal void Refresh() => m_vm.Refresh();

        internal void Unbind()
        {
            if (m_join != null)
                m_join.clicked -= m_vm.Submit;
            m_join = null;
        }

        public void Dispose()
        {
            Unbind();
            m_vm.Dispose();
        }
    }
}
