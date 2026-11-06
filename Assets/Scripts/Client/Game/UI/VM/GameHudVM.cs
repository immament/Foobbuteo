// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Runtime.CompilerServices;
using Imma.Common;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.VM
{
    [CreateAssetMenu(fileName = "GameHudVMData", menuName = "Foobbuteo/GameHud View Model")]
    internal class GameHudVM : ScriptableObject, INotifyBindablePropertyChanged
    {
        internal static GameHudVM CreateRuntime(GameHudVM template)
        {
            var runtime = Instantiate(template);
            runtime.hideFlags = HideFlags.DontSave;
            runtime.ResetRuntimeState();
            return runtime;
        }

        internal void ResetRuntimeState()
        {
            m_user = null;
            m_gameState = null;
            m_selectedPlayer = null;
            m_teamA = null;
            m_teamB = null;
            m_scoreA = m_scoreB = 0;
            m_onePlayerHasBall = false;
            m_isSessionReady = false;
            m_timerValue = -1;
            m_timerType = default;
            m_turnText = "";
            m_timerText = "";
            IsTimerUrgent = false;
            foreach (
                string property in new[]
                {
                    nameof(SelectedPlayer),
                    nameof(HasSelectedPlayer),
                    nameof(TeamA),
                    nameof(TeamB),
                    nameof(ScoreA),
                    nameof(ScoreB),
                    nameof(ScoreText),
                    nameof(OnePlayerHasBall),
                    nameof(TurnText),
                    nameof(TimerText),
                    nameof(IsTimerUrgent),
                    nameof(CanMoveSelectedPlayer),
                    nameof(CanClaimPlayer),
                    nameof(ShowCurrentUserPlayerBtn),
                    nameof(CloseButtonDisplay),
                }
            )
                Notify(property);
        }

        [SerializeReference]
        private GameUser m_user;
        internal GameUser User
        {
            get => m_user;
            set
            {
                m_user = value;

                Notify(nameof(CanClaimPlayer));
                Notify(nameof(ShowCurrentUserPlayerBtn));
                RefreshRoomPermissions();

                if (SelectedPlayer != null)
                {
                    Notify(nameof(SelectedPlayer));
                    Notify(nameof(CanMoveSelectedPlayer));
                }
            }
        }
        private ClientGameState m_gameState;
        internal ClientGameState GameState
        {
            get => m_gameState;
            set
            {
                m_gameState = value;
                RefreshRoomPermissions();
            }
        }

        internal bool CanLeaveRoom =>
            GameState != null && GameRoomLeavePolicy.CanLeave(User, GameState.GameId, GameState.Status);

        [CreateProperty]
        public DisplayStyle CloseButtonDisplay =>
            User != null && GameState != null && !GameRoomLeavePolicy.CanLeave(User, GameState.GameId, GameState.Status)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

        internal void RefreshRoomPermissions() => Notify(nameof(CloseButtonDisplay));

        private bool m_isSessionReady = true;
        internal bool IsSessionReady
        {
            get => m_isSessionReady;
            set
            {
                if (m_isSessionReady == value)
                    return;
                m_isSessionReady = value;
                Notify(nameof(CanMoveSelectedPlayer));
                Notify(nameof(CanClaimPlayer));
            }
        }

        internal ClientPlayerState PlayerWithBall =>
            GameState?.HomeTeam?.GetPlayerWithBall() ?? GameState?.AwayTeam?.GetPlayerWithBall();

        [SerializeField]
        private bool m_onePlayerHasBall;

        [CreateProperty]
        public bool OnePlayerHasBall
        {
            get { return m_onePlayerHasBall; }
            internal set
            {
                if (m_onePlayerHasBall == value)
                    return;
                m_onePlayerHasBall = value;
                Notify();
            }
        }

        [CreateProperty]
        public bool CanMoveSelectedPlayer
        {
            get
            {
                if (m_isSessionReady && m_selectedPlayer != null && GameState != null)
                {
                    if (GameState.GetPlayerForCommand(m_selectedPlayer.PlayerKey, User, out var reason) == null)
                    {
                        // this.LogWarn(reason);
                        return false;
                    }
                    return true;
                }
                return false;
            }
        }

        [CreateProperty]
        public bool ShowCurrentUserPlayerBtn =>
            m_user != null
            && m_user.GameSlot.HasValue
            && m_user.GameSlot.Value.IsPlayer
            && m_user.GameSlot.Value.GameId == GameState?.GameId;

        [CreateProperty]
        public bool CanClaimPlayer =>
            m_isSessionReady
            && m_selectedPlayer != null
            && GameState?.CanClaimPlayer(User, m_selectedPlayer.PlayerKey, out _) == true;

        [SerializeReference]
        private PlayerVM m_selectedPlayer;

        [CreateProperty]
        public PlayerVM SelectedPlayer
        {
            get { return m_selectedPlayer; }
            private set
            {
                if (m_selectedPlayer == value)
                    return;
                bool hadSelectedPlayer = HasSelectedPlayer;
                m_selectedPlayer = value;
                Notify();
                if (hadSelectedPlayer != HasSelectedPlayer)
                    Notify(nameof(HasSelectedPlayer));

                Notify(nameof(CanClaimPlayer));

                Notify(nameof(CanMoveSelectedPlayer));
            }
        }

        [CreateProperty]
        public bool HasSelectedPlayer => m_selectedPlayer != null;

        [SerializeField]
        private string m_turnText = "Turn 0";

        [CreateProperty]
        public string TurnText
        {
            get => m_turnText;
            set
            {
                if (m_turnText == value)
                    return;
                m_turnText = value;
                Notify();
            }
        }

        private int m_timerValue = 0;
        private TimerType m_timerType = 0;
        internal bool IsTimerUrgent { get; private set; }

        internal enum TimerType
        {
            SecondsLeft,
            MinutesLeft,
            TimeToMove,
            EndPlayerMove,
            RunningTurn,
            Processing,
            Starting,
        }

        private const string END_PLAYER_MOVE_TIMER_TEXT = "End player move";
        private const string PROCESSING_TIMER_TEXT = "Processing turn...";

        internal bool UpdateTimer(TimerType type, int value = 0)
        {
            if (m_timerValue == value && m_timerType == type)
                return false;

            switch (type)
            {
                case TimerType.MinutesLeft:
                    TimerText = $"Starts in {value}m";
                    break;
                case TimerType.SecondsLeft:
                    TimerText = $"Starts in {value}s";
                    break;
                case TimerType.TimeToMove:
                    TimerText = $"{Math.Max(0, value)}s remaining";
                    break;
                case TimerType.EndPlayerMove:
                    if (m_timerType != type)
                        TimerText = END_PLAYER_MOVE_TIMER_TEXT;
                    break;
                case TimerType.Processing:
                    if (m_timerType != type)
                        TimerText = PROCESSING_TIMER_TEXT;
                    break;
                case TimerType.RunningTurn:
                    TimerText = $"Displaying results: {value}";
                    break;
                case TimerType.Starting:
                    TimerText = "Starting...";
                    break;
            }
            m_timerType = type;
            m_timerValue = value;
            IsTimerUrgent = type == TimerType.TimeToMove && value >= 0 && value <= 5;
            Notify(nameof(IsTimerUrgent));
            return true;
        }

        [SerializeField]
        private string m_timerText = "Timer 0";

        [CreateProperty]
        public string TimerText
        {
            get => m_timerText;
            internal set
            {
                if (m_timerText == value)
                    return;
                m_timerText = value;
                IsTimerUrgent = false;
                Notify(nameof(IsTimerUrgent));
                Notify();
            }
        }

        [SerializeReference]
        private TeamVM m_teamA;

        [CreateProperty]
        public TeamVM TeamA
        {
            get => m_teamA;
            set
            {
                if (m_teamA == value)
                    return;
                m_teamA = value;
                Notify();
            }
        }

        [SerializeReference]
        private TeamVM m_teamB;

        [CreateProperty]
        public TeamVM TeamB
        {
            get => m_teamB;
            set
            {
                if (m_teamB == value)
                    return;
                m_teamB = value;
                Notify();
            }
        }

        internal void UpdateTeamInfo(TeamIndex teamIdx, NetFoobbuteoTeam teamInfo)
        {
            TeamVM team = teamIdx == TeamIndex.Home ? TeamA : TeamB;
            team.UpdateInfo(teamInfo);
            Notify(teamIdx == TeamIndex.Home ? nameof(TeamA) : nameof(TeamB));
        }

        [SerializeField]
        private int m_scoreA = 0;

        [CreateProperty]
        public int ScoreA
        {
            get => m_scoreA;
            set
            {
                if (m_scoreA == value)
                    return;
                m_scoreA = value;
                Notify();
                Notify(nameof(ScoreText));
            }
        }

        [SerializeField]
        private int m_scoreB = 0;

        [CreateProperty]
        public int ScoreB
        {
            get => m_scoreB;
            set
            {
                if (m_scoreB == value)
                    return;
                m_scoreB = value;
                Notify();
                Notify(nameof(ScoreText));
            }
        }

        [CreateProperty]
        public string ScoreText => $"{ScoreA} : {ScoreB}";

        internal void SwitchPlayer(PlayerVM player)
        {
            SelectedPlayer = player;
        }

        internal void RefreshSelectedPlayer()
        {
            Notify(nameof(SelectedPlayer));
            Notify(nameof(CanClaimPlayer));
            Notify(nameof(CanMoveSelectedPlayer));
        }

        // ++ Implement INotifyBindablePropertyChanged +++

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify([CallerMemberName] string propertyName = "")
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }

#if UNITY_EDITOR
        // EDITOR RESET
        public void Reset()
        {
            m_selectedPlayer = PlayerVM.EditorPlayer();
            m_teamA = new TeamVM(TeamIndex.Home, 1001, "Team A Editor", null, null, ShirtColorEnum.Blue);
            m_teamB = new TeamVM(TeamIndex.Away, 1002, "Team B Editor", null, null, ShirtColorEnum.Red);
        }
#endif
    }
}
