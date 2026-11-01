// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class MvMLineupVM : INotifyBindablePropertyChanged
    {
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
        internal MvMLineupDraft Draft { get; private set; }
        internal TeamIndex? Team { get; private set; }
        internal bool IsOpen { get; private set; }
        internal bool Busy { get; private set; }
        internal PlayerTableVM Starting { get; } = new();
        internal PlayerTableVM Reserves { get; } = new();
        private readonly CountryFlagCache m_flags;
        private readonly int m_gameId;
        private bool m_startingExpanded = true;
        private bool m_reservesExpanded = true;

        internal MvMLineupVM(int gameId, ClientImageCatalog images)
        {
            m_gameId = gameId;
            m_flags = new CountryFlagCache(images);
        }

        [CreateProperty]
        public string Title { get; private set; } = "Prepare lineup";

        [CreateProperty]
        public string Error { get; private set; } = "";

        [CreateProperty]
        public DisplayStyle ErrorDisplay => string.IsNullOrEmpty(Error) ? DisplayStyle.None : DisplayStyle.Flex;

        [CreateProperty]
        public bool StartingExpanded
        {
            get => m_startingExpanded;
            set
            {
                if (m_startingExpanded == value)
                    return;
                m_startingExpanded = value;
                NotifyPresentation();
            }
        }

        [CreateProperty]
        public bool ReservesExpanded
        {
            get => m_reservesExpanded;
            set
            {
                if (m_reservesExpanded == value)
                    return;
                m_reservesExpanded = value;
                NotifyPresentation();
            }
        }

        [CreateProperty]
        public DisplayStyle StartingDisplay => StartingExpanded ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public DisplayStyle ReservesDisplay => ReservesExpanded ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public float StartingFlexGrow => StartingExpanded ? 1f : 0f;

        [CreateProperty]
        public float ReservesFlexGrow => ReservesExpanded ? 1f : 0f;

        [CreateProperty]
        public DisplayStyle Display => IsOpen ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public DisplayStyle HomeDisplay => Team == TeamIndex.Home ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public DisplayStyle AwayDisplay => Team == TeamIndex.Away ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool BackgroundEnabled => !IsOpen;

        [CreateProperty]
        public bool InputsEnabled => !Busy;

        [CreateProperty]
        public bool CanApply => !Busy && Draft != null;

        internal void SetTeam(TeamIndex? team)
        {
            if (Team == team)
                return;
            Team = team;
            Draft = null;
            if (!team.HasValue)
                Close();
            Notify();
        }

        internal void Load(MvMLineupResult result)
        {
            Busy = false;
            Error = result.Error ?? "";
            if (result.Success)
                Draft = new MvMLineupDraft(result.Squad, result.Lineup);
            RefreshRows();
            Notify();
        }

        internal void Open(string teamName, bool? smallScreen = null)
        {
            if (!IsOpen && smallScreen.HasValue)
            {
                StartingExpanded = !smallScreen.Value;
                ReservesExpanded = true;
                var view = smallScreen.Value
                    ? PlayerTableViewDefinition.AverageOnly
                    : PlayerTableViewDefinition.AllSkills;
                SetView(view);
            }
            IsOpen = true;
            Title = teamName + " — Prepare lineup";
            Error = "";
            Notify();
        }

        internal void SetView(PlayerTableViewDefinition view)
        {
            Starting.SetView(view);
            Reserves.SetView(view);
        }

        internal void SetBusy()
        {
            Busy = true;
            Error = "";
            Notify();
        }

        internal void SaveCompleted(MvMLineupResult result)
        {
            Busy = false;
            Error = result.Error ?? "";
            if (result.Success)
                Close();
            else if (Draft != null && result.Lineup?.Length == MvMLineupRules.PlayersPerTeam)
                Draft.Rebase(result.Lineup);
            Notify();
        }

        internal bool Swap(int sourceId, int targetId)
        {
            if (Busy || !IsOpen || Draft == null || !Draft.Swap(sourceId, targetId))
                return false;
            RefreshRows();
            return true;
        }

        internal void Close()
        {
            IsOpen = false;
            Busy = false;
            Draft = null;
            Starting.SetRows(Array.Empty<PlayerListRowVM>());
            Reserves.SetRows(Array.Empty<PlayerListRowVM>());
            Notify();
        }

        private void RefreshRows()
        {
            if (Draft == null || !Team.HasValue)
                return;
            Starting.SetRows(Draft.Starting.Select((id, index) => Row(id, (byte)(index + 1))));
            Reserves.SetRows(Draft.Reserves.Select(id => Row(id, null)));
        }

        private PlayerListRowVM Row(int id, byte? slot)
        {
            var player = ClientPlayerState.FromContract(m_gameId, Draft.GetPlayer(id, Team.Value));
            return new PlayerListRowVM(player, slot, m_flags.Get(player.CountryId));
        }

        private void Notify()
        {
            foreach (
                string property in new[]
                {
                    nameof(Title),
                    nameof(Error),
                    nameof(ErrorDisplay),
                    nameof(Display),
                    nameof(HomeDisplay),
                    nameof(AwayDisplay),
                    nameof(BackgroundEnabled),
                    nameof(InputsEnabled),
                    nameof(CanApply),
                }
            )
                propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }

        private void NotifyPresentation()
        {
            foreach (
                string property in new[]
                {
                    nameof(StartingExpanded),
                    nameof(ReservesExpanded),
                    nameof(StartingDisplay),
                    nameof(ReservesDisplay),
                    nameof(StartingFlexGrow),
                    nameof(ReservesFlexGrow),
                }
            )
                propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
