// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.VM
{
    internal sealed class MvMJoinVM : INotifyBindablePropertyChanged, IDisposable
    {
        private readonly IClientGameRoomNB m_api;
        private readonly ClientGameState m_game;
        private readonly Func<GameUser> m_getUser;
        private ManagedTeam[] m_teams = Array.Empty<ManagedTeam>();
        private int m_selectedTeamIndex;
        private bool m_pending;
        private bool m_disposed;

        internal MvMJoinVM(IClientGameRoomNB api, ClientGameState game, Func<GameUser> getUser)
        {
            m_api = api;
            m_game = game;
            m_getUser = getUser;
            m_api.MvMJoinCompleted += OnCompleted;
        }

        [CreateProperty]
        public List<string> TeamChoices => m_teams.Select(team => team.Key.ToString()).ToList();

        internal string FormatTeamChoice(string value)
        {
            int index = Array.FindIndex(m_teams, team => team.Key.ToString() == value);
            return index >= 0 ? m_teams[index].DisplayName : "";
        }

        [CreateProperty]
        public int SelectedTeamIndex
        {
            get => m_selectedTeamIndex;
            set
            {
                if (m_pending || value < 0 || value >= m_teams.Length || value == m_selectedTeamIndex)
                    return;
                m_selectedTeamIndex = value;
                Notify(nameof(SelectedTeamIndex));
                Notify(nameof(CanJoin));
            }
        }

        [CreateProperty]
        public DisplayStyle Display =>
            m_game.HomeTeam != null
            && m_game.AwayTeam != null
            && m_game.IsWaitingForOpponent
            && m_getUser() != null
            && !m_getUser().GameSlot.HasValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        [CreateProperty]
        public bool InputsEnabled => !m_pending;

        [CreateProperty]
        public bool CanJoin =>
            !m_pending
            && m_selectedTeamIndex >= 0
            && m_selectedTeamIndex < m_teams.Length
            && m_game.CanJoinAsManager(m_getUser(), m_teams[m_selectedTeamIndex].Key, out _);

        [CreateProperty]
        public string JoinText => m_pending ? "Joining…" : "Join as away manager";

        [CreateProperty]
        public string Error { get; private set; } = "";

        internal void Refresh()
        {
            if (m_disposed)
                return;
            TeamKey selected =
                m_selectedTeamIndex >= 0 && m_selectedTeamIndex < m_teams.Length
                    ? m_teams[m_selectedTeamIndex].Key
                    : default;
            m_teams =
                m_getUser()?.Teams.Where(team => team.Key.IsSupported && team.Key != m_game.HomeTeam?.TeamKey).ToArray()
                ?? Array.Empty<ManagedTeam>();
            m_selectedTeamIndex = Array.FindIndex(m_teams, team => team.Key == selected);
            if (m_selectedTeamIndex < 0 && m_teams.Length > 0)
                m_selectedTeamIndex = 0;
            Notify(nameof(TeamChoices));
            Notify(nameof(SelectedTeamIndex));
            NotifyState();
        }

        internal void Submit()
        {
            if (m_disposed || !CanJoin)
                return;
            m_pending = true;
            Error = "";
            NotifyState();
            try
            {
                m_api.JoinMvMGame(m_teams[m_selectedTeamIndex].Key);
            }
            catch (Exception)
            {
                OnCompleted(new MvMJoinResult(false, "The request could not be sent. Please try again."));
            }
        }

        private void OnCompleted(MvMJoinResult result)
        {
            if (m_disposed || !m_pending)
                return;
            m_pending = false;
            Error = result.Success ? "" : result.Error;
            Refresh();
        }

        private void NotifyState()
        {
            Notify(nameof(Display));
            Notify(nameof(InputsEnabled));
            Notify(nameof(CanJoin));
            Notify(nameof(JoinText));
            Notify(nameof(Error));
        }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify(string property) =>
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));

        public void Dispose()
        {
            m_disposed = true;
            m_api.MvMJoinCompleted -= OnCompleted;
        }
    }
}
