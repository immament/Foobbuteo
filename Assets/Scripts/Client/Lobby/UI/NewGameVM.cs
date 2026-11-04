// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Imma.Foobbuteo.Client.Game;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    internal sealed class NewGameVM : INotifyBindablePropertyChanged, IDisposable
    {
        internal static readonly List<string> StartChoices = Enumerable
            .Range(
                NewGameOptionsRules.MinStartInMinutes,
                NewGameOptionsRules.MaxStartInMinutes - NewGameOptionsRules.MinStartInMinutes + 1
            )
            .Select(minutes => $"in {minutes} minutes")
            .ToList();
        internal static readonly List<string> TurnChoices = NewGameOptionsRules
            .TurnCounts.Select(count => count.ToString())
            .ToList();
        internal static readonly List<string> ColorChoices = Enum.GetNames(typeof(ShirtColorEnum)).ToList();
        internal static readonly List<string> PositionChoices = Imma.Foobbuteo
            .Client
            .Game
            .UI
            .Players
            .PlayerPositionLabels
            .Choices;

        private readonly ILobbyApiClient m_api;
        private readonly Func<bool> m_canCreate;
        private readonly Func<GameUser> m_getUser;
        private ManagedTeam[] m_teams = Array.Empty<ManagedTeam>();
        private string m_gameType = "PvP";
        private int m_selectedTeamIndex = -1;
        private string m_start;
        private string m_turns;
        private string m_home;
        private string m_away;
        private string m_position;
        private bool m_open;
        private bool m_pending;
        private bool m_disposed;
        private string m_error = "";

        public NewGameVM(ILobbyApiClient api, Func<bool> canCreate, Func<GameUser> getUser = null)
        {
            m_api = api;
            m_canCreate = canCreate;
            m_getUser = getUser ?? (() => null);
            RestoreDefaults();
            m_api.NewGameCompleted += OnCompleted;
        }

        [CreateProperty]
        public List<string> GameTypeChoices => m_teams.Length > 0 ? new() { "PvP", "MvM" } : new() { "PvP" };

        [CreateProperty]
        public string GameType
        {
            get => m_gameType;
            set
            {
                if (m_pending || m_gameType == value || !GameTypeChoices.Contains(value))
                    return;
                m_gameType = value;
                Notify(nameof(GameType));
                Notify(nameof(TeamDisplay));
                Notify(nameof(PositionDisplay));
                RefreshValidation();
            }
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
                RefreshValidation();
            }
        }

        [CreateProperty]
        public DisplayStyle TeamDisplay => GameType == "MvM" ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public DisplayStyle PositionDisplay => GameType == "PvP" ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public string StartTime
        {
            get => m_start;
            set => SetSelection(ref m_start, value, StartChoices);
        }

        [CreateProperty]
        public string Turns
        {
            get => m_turns;
            set => SetSelection(ref m_turns, value, TurnChoices);
        }

        [CreateProperty]
        public string HomeColor
        {
            get => m_home;
            set => SetSelection(ref m_home, value, ColorChoices);
        }

        [CreateProperty]
        public string AwayColor
        {
            get => m_away;
            set => SetSelection(ref m_away, value, ColorChoices);
        }

        [CreateProperty]
        public string Position
        {
            get => m_position;
            set => SetSelection(ref m_position, value, PositionChoices);
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> Display => m_open ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool BackgroundEnabled => !m_open;

        [CreateProperty]
        public bool InputsEnabled => !m_pending;

        [CreateProperty]
        public bool CanSubmit => !m_pending && m_canCreate();

        [CreateProperty]
        public string CreateText => m_pending ? "CREATING…." : "CREATE GAME";

        [CreateProperty]
        public string Error => m_error;

        [CreateProperty]
        public Color HomeTint => ShirtColors.GetColor(Enum.Parse<ShirtColorEnum>(m_home));

        [CreateProperty]
        public Color AwayTint => ShirtColors.GetColor(Enum.Parse<ShirtColorEnum>(m_away));

        internal bool IsOpen => m_open;
        internal bool IsPending => m_pending;
        internal event Action<bool> RequestCompleted;
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public bool Open()
        {
            if (m_disposed || m_pending || m_open || !m_canCreate())
                return false;
            RestoreDefaults();
            m_error = "";
            m_open = true;
            RefreshTeams();
            NotifyState();
            return true;
        }

        public void Reset()
        {
            m_open = false;
            m_pending = false;
            m_error = "";
            RestoreDefaults();
            NotifyState();
        }

        public void RefreshAvailability()
        {
            RefreshTeams();
            Notify(nameof(CanSubmit));
        }

        private void RefreshTeams()
        {
            TeamKey selected =
                m_selectedTeamIndex >= 0 && m_selectedTeamIndex < m_teams.Length
                    ? m_teams[m_selectedTeamIndex].Key
                    : default;
            m_teams = m_getUser()?.Teams.Where(team => team.Key.IsSupported).ToArray() ?? Array.Empty<ManagedTeam>();
            m_selectedTeamIndex = Array.FindIndex(m_teams, team => team.Key == selected);
            if (m_selectedTeamIndex < 0 && m_teams.Length > 0)
                m_selectedTeamIndex = 0;
            if (m_teams.Length == 0)
                m_gameType = "PvP";
            Notify(nameof(GameTypeChoices));
            Notify(nameof(GameType));
            Notify(nameof(TeamChoices));
            Notify(nameof(SelectedTeamIndex));
            Notify(nameof(TeamDisplay));
            Notify(nameof(PositionDisplay));
        }

        private void RefreshValidation()
        {
            NewGameOptionsRules.TryValidate(GetOptions(), out var error);
            m_error = error ?? "";
            Notify(nameof(Error));
        }

        public void Submit()
        {
            if (m_disposed || !m_open || !CanSubmit)
                return;
            var options = GetOptions();
            if (!NewGameOptionsRules.TryValidate(options, out string error))
            {
                m_error = error;
                Notify(nameof(Error));
                return;
            }
            m_pending = true;
            m_error = "";
            NotifyState();
            try
            {
                m_api.NewGame(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnCompleted(new NewGameResult(false, error: "The request could not be sent. Please try again."));
            }
        }

        public bool Cancel()
        {
            if (m_disposed || m_pending)
                return false;
            Reset();
            return true;
        }

        private NewGameOptions GetOptions() =>
            new(
                StartChoices.IndexOf(m_start) + NewGameOptionsRules.MinStartInMinutes,
                int.Parse(m_turns),
                Enum.Parse<ShirtColorEnum>(m_home),
                Enum.Parse<ShirtColorEnum>(m_away),
                PositionChoices.IndexOf(m_position) + NewGameOptionsRules.MinPlayerPosition,
                GameType == "MvM" ? FoobbuteoGameType.MvM : FoobbuteoGameType.PvP,
                GameType == "MvM" && m_selectedTeamIndex >= 0 && m_selectedTeamIndex < m_teams.Length
                    ? m_teams[m_selectedTeamIndex].Key
                    : default
            );

        private void SetSelection(
            ref string field,
            string value,
            List<string> choices,
            [CallerMemberName] string propertyName = ""
        )
        {
            if (m_disposed || m_pending || field == value || !choices.Contains(value))
                return;
            field = value;
            NewGameOptionsRules.TryValidate(GetOptions(), out string error);
            m_error = error ?? "";
            Notify(propertyName);
            Notify(nameof(Error));
            if (propertyName == nameof(HomeColor))
                Notify(nameof(HomeTint));
            if (propertyName == nameof(AwayColor))
                Notify(nameof(AwayTint));
        }

        private void RestoreDefaults()
        {
            var defaults = NewGameOptions.Default;
            m_start = StartChoices[defaults.StartInMinutes - NewGameOptionsRules.MinStartInMinutes];
            m_turns = defaults.NumberOfTurns.ToString();
            m_home = defaults.HomeTeamColor.ToString();
            m_away = defaults.AwayTeamColor.ToString();
            m_position = PositionChoices[defaults.PlayerPosition - NewGameOptionsRules.MinPlayerPosition];
            m_gameType = "PvP";
            Notify(nameof(GameType));
            Notify(nameof(TeamDisplay));
            Notify(nameof(PositionDisplay));
            Notify(nameof(StartTime));
            Notify(nameof(Turns));
            Notify(nameof(HomeColor));
            Notify(nameof(AwayColor));
            Notify(nameof(Position));
            Notify(nameof(HomeTint));
            Notify(nameof(AwayTint));
        }

        private void OnCompleted(NewGameResult result)
        {
            if (!m_pending || m_disposed)
                return;
            m_pending = false;
            if (result.Success)
                m_open = false;
            m_error = result.Success ? "" : result.Error;
            NotifyState();
            RequestCompleted?.Invoke(result.Success);
        }

        private void NotifyState()
        {
            Notify(nameof(Display));
            Notify(nameof(BackgroundEnabled));
            Notify(nameof(InputsEnabled));
            Notify(nameof(CanSubmit));
            Notify(nameof(CreateText));
            Notify(nameof(Error));
        }

        private void Notify(string propertyName) =>
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            m_api.NewGameCompleted -= OnCompleted;
        }
    }
}
