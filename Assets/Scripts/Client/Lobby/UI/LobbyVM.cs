// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Imma.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    [Serializable]
    public class LobbyVM : INotifyBindablePropertyChanged
    {
        public LobbyVM()
        {
            Games = new();
            // SelectedGame = null;
        }

        // Use in editor Reset
        public LobbyVM(List<GameListItemVM> games)
        {
            // Debug.Log("++LobbyVM(MatchInfoContract[]) ctor");
            Games = games;
            SelectedGame = null;
        }

        [SerializeReference]
        private GameUser m_user;

        internal GameUser User
        {
            get => m_user;
            set
            {
                this.LDebug(() => $"IsAdmin: {value?.IsAdmin} / prev: {IsAdmin}");
                m_user = value;
                PlayerDesc =
                    m_user != null
                        ? $"{m_user.Name} (@{m_user.UserId}/{m_user.PlayerId}) {m_user.GameSlot.NullableValue?.GameId}"
                        : "-";
                Notify(nameof(IsAdmin));
                Notify(nameof(CanCreateNewGame));
            }
        }

        [CreateProperty]
        internal bool IsAdmin => m_user?.IsAdmin ?? false;

        private string m_playerDesc;

        [CreateProperty]
        public string PlayerDesc
        {
            get => m_playerDesc;
            set
            {
                this.LDebug(() => $"PlayerDesc: {value} / prev: {m_playerDesc}");
                if (m_playerDesc == value)
                    return;
                m_playerDesc = value;
                Notify();
            }
        }

        [SerializeField]
        private List<GameListItemVM> m_games;

        [CreateProperty]
        public List<GameListItemVM> Games
        {
            get => m_games;
            set
            {
                m_games = value;

                if (m_selectedGame != null)
                {
                    SelectedGame = m_games.Find(g => g.GameId == m_selectedGame.GameId);
                }

                Notify();
                Notify(nameof(CanCreateNewGame));
            }
        }

        [SerializeReference]
        private GameListItemVM m_selectedGame;
        private readonly GameListItemVM m_emptyGameDetails = new();

        [CreateProperty]
        public GameListItemVM SelectedGameDetails => m_selectedGame ?? m_emptyGameDetails;

        [CreateProperty]
        public GameListItemVM SelectedGame
        {
            get => m_selectedGame;
            set
            {
                // Debug.Log($"set SelectedGame={value}");
                if (m_selectedGame == value)
                    return;
                m_selectedGame = value;
                // Debug.Log($"set SelectedGame (2)={value}");
                Notify();
                Notify(nameof(IsSelectedGame));
                Notify(nameof(SelectedGameDetails));
            }
        }

        [CreateProperty]
        public bool IsSelectedGame
        {
            get
            {
                // Debug.Log($"IsSelectedGame: {SelectedGame?.MatchId}, {SelectedGame != null}");
                return SelectedGame != null;
            }
        }

        [CreateProperty]
        public bool CanCreateNewGame => m_isClientConnected && m_user != null && !m_user.GameSlot.HasValue;

        // && (IsAdmin || !m_games.Any(game => game.Status != FoobbuteoGameStatus.Ended));

        private bool m_isClientConnected;
        private bool m_isClientConnecting;

        [CreateProperty]
        public bool IsClientConnected => m_isClientConnected;

        [CreateProperty]
        public bool IsLoginVisible => m_sessionView == ClientSessionView.Login;

        private ClientSessionView m_sessionView = ClientSessionView.Loading;

        [CreateProperty]
        public bool IsSessionVisible => m_sessionView == ClientSessionView.Session;

        [CreateProperty]
        public bool IsLoadingVisible => m_sessionView == ClientSessionView.Loading;

        [CreateProperty]
        public bool IsProblemVisible => m_sessionView == ClientSessionView.Problem;

        [CreateProperty]
        public string ConnectionProblem { get; private set; } = "";

        [CreateProperty]
        public bool CanLogout { get; private set; }

        [CreateProperty]
        public bool IsProfileMenuVisible { get; private set; }

        internal void UpdateSessionState(ClientSessionView view, string message, bool canLogout)
        {
            m_sessionView = view;
            ConnectionProblem = message;
            CanLogout = canLogout;
            Notify(nameof(IsLoginVisible));
            Notify(nameof(IsSessionVisible));
            Notify(nameof(IsLoadingVisible));
            Notify(nameof(IsProblemVisible));
            Notify(nameof(ConnectionProblem));
            Notify(nameof(CanLogout));
        }

        internal void ToggleProfileMenu()
        {
            IsProfileMenuVisible = CanLogout && !IsProfileMenuVisible;
            Notify(nameof(IsProfileMenuVisible));
        }

        internal void UpdateConnectionState(bool isConnected, bool isConnecting)
        {
            if (m_isClientConnected == isConnected && m_isClientConnecting == isConnecting)
                return;

            m_isClientConnected = isConnected;
            m_isClientConnecting = isConnecting;
            Notify(nameof(IsClientConnected));
            Notify(nameof(CanCreateNewGame));
        }

        // ++ Implement INotifyBindablePropertyChanged +++

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify([CallerMemberName] string propertyName = "")
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }
    }
}
