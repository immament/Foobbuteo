// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Runtime.CompilerServices;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.VM
{
    [Serializable]
    internal class PlayerVM : INotifyBindablePropertyChanged
    {
        private PlayerVM() { }

        public PlayerVM(byte playerIdx, TeamVM team, ClientPlayerState player)
        {
            m_team = team;
            m_shirt = team.Shirt;
            ShirtTextColor = team.ShirtTextColor;

            m_playerState = player;
            m_playerIdx = playerIdx;
            m_playerId = player.PlayerId;
            m_name = player.Name;
            m_hasBall = player.HasBall;
            m_isUser = player.IsUser;
            m_selectedPlayerMode = player.PlayerMode;
        }

        public void Update(ClientPlayerState player)
        {
            m_playerState = player;
            m_playerId = player.PlayerId;
            Name = player.Name;
            IsUser = player.IsUser;
            if (m_selectedPlayerMode != player.PlayerMode)
            {
                m_selectedPlayerMode = player.PlayerMode;
                Notify(nameof(SelectedPlayerMode));
            }
        }

#if UNITY_EDITOR
        internal static PlayerVM EditorPlayer()
        {
            ImmLogger.LTrace<PlayerVM>("++ EditorPlayer ++");
            return new PlayerVM()
            {
                m_playerId = -1000,
                m_playerIdx = 2,
                m_name = "Player Name",
                m_shirt = ClientImageCatalog.LoadForEditor()?.GetShirt(ShirtColorEnum.Blue),
                m_shirtTextColor = Color.white,
            };
        }
#endif

        private ClientPlayerState m_playerState;

        [SerializeField]
        private bool m_isUser = false;

        [CreateProperty]
        public bool IsUser
        {
            get => m_isUser;
            private set
            {
                if (m_isUser == value)
                    return;
                m_isUser = value;
                Notify();
                // Notify(nameof(CanClaimPlayer));
            }
        }

        [SerializeField]
        private string m_name;

        [CreateProperty]
        public string Name
        {
            get => m_name;
            private set
            {
                if (m_name == value)
                    return;
                m_name = value;
                Notify();
            }
        }

        public PlayerKey PlayerKey => m_playerState.Slot.PlayerKey;

        [SerializeField]
        private byte m_playerIdx;

        // public byte PlayerIdx => m_playerIdx;

        [CreateProperty]
        public byte ShirtNumber
        {
            get => m_playerIdx;
            private set
            {
                if (m_playerIdx == value)
                    return;
                m_playerIdx = value;
                Notify();
                Notify(nameof(IsGk));
            }
        }

        private const int GK_PLAYER_IDX = 1;

        [CreateProperty]
        public bool IsGk => m_playerIdx == GK_PLAYER_IDX;

        [SerializeField]
        private bool m_hasBall;

        [CreateProperty]
        public bool HasBall
        {
            get => m_hasBall;
            internal set
            {
                if (m_hasBall == value)
                    return;
                m_hasBall = value;
                Notify();
            }
        }

        [SerializeField]
        private Sprite m_shirt;

        [CreateProperty]
        public Sprite Shirt
        {
            get => m_shirt;
            private set
            {
                if (m_shirt == value)
                    return;
                m_shirt = value;
                Notify();
            }
        }

        [SerializeField]
        private Color m_shirtTextColor = Color.white;

        [CreateProperty]
        public Color ShirtTextColor
        {
            get => m_shirtTextColor;
            private set
            {
                if (m_shirtTextColor == value)
                    return;
                m_shirtTextColor = value;
                Notify();
            }
        }

        [CreateProperty]
        public bool ShowGkFieldPlayerActions => !IsGk && !HasBall;

        [CreateProperty]
        public bool ShowGkActions => IsGk && !HasBall;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify([CallerMemberName] string propertyName = "")
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }

        [SerializeField]
        private PlayerActionEnum m_selectedPlayerAction = PlayerActionEnum.Move;

        [CreateProperty]
        public PlayerActionEnum SelectedPlayerAction
        {
            get => m_selectedPlayerAction;
            set
            {
                this.LDebug(() => $"Select Action {m_selectedPlayerAction} => {value}");

                if (m_selectedPlayerAction == value)
                    return;
                m_selectedPlayerAction = value;
                Notify();
                PlayerActionChanged?.Invoke(value);
            }
        }

        public event Action<PlayerActionEnum> PlayerActionChanged;

        [SerializeField]
        private PlayerModeEnum m_selectedPlayerMode = PlayerModeEnum.Confrontation;

        [CreateProperty]
        public PlayerModeEnum SelectedPlayerMode
        {
            get => m_selectedPlayerMode;
            set
            {
                this.LDebug(() => $"Select Mode {m_selectedPlayerAction} => {value}");
                if (m_selectedPlayerMode == value)
                    return;
                m_selectedPlayerMode = value;
                Notify();
                PlayerModeChanged?.Invoke(value);
            }
        }

        public event Action<PlayerModeEnum> PlayerModeChanged;

        // -- VM --------------

        // private int _teamIdx;
        private int m_playerId;

        [CreateProperty]
        public int PlayerId => m_playerId;

        private readonly TeamVM m_team;
        public TeamVM Team => m_team;

        // private GameDetails_PlayerContract m_playerContract;
    }
}
