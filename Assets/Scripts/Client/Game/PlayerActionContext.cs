// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game
{
    internal class PlayerActionContext : IDisposable
    {
        internal PlayerActionContext(PlayerMB playerMB, Transform actionZone, Transform highPassZone)
        {
            m_playerMB = playerMB;
            m_actionZone = actionZone;
            m_highPassZone = highPassZone;

            m_playerMB.PlayerVM.PlayerActionChanged += OnPlayerActionChanged;
            m_playerMB.PlayerVM.PlayerModeChanged += OnPlayerModeChanged;
        }

        private void OnPlayerModeChanged(PlayerModeEnum mode)
        {
            try
            {
                PlayerModeCommand cmd = new PlayerModeCommand(
                    GameManager.Instance.TurnManager.CurrentTurn,
                    m_playerMB.TeamIdx,
                    m_playerMB.PlayerIdx,
                    m_playerMB.PlayerId,
                    mode
                );
                GameManager.Instance.CommandManager.ChangeMode(cmd);
            }
            catch (AppException e)
            {
                this.LWarn(e.Message);
                AppCtx.Client.ShowToast(e.Message);
                return;
            }

            m_playerMB.PlayerState.ChangePlayerMode(mode);
        }

        private readonly PlayerMB m_playerMB;
        private ClientPlayerState PlayerState => m_playerMB.PlayerState;

        private Vector2? m_targetPosition;
        private PlayerActionEnum m_targetActionType;

        [SerializeField]
        private Transform m_actionZone;

        [SerializeField]
        private Transform m_highPassZone;

        internal void SetActive(bool selected)
        {
            if (selected)
            {
                ShowZone();
            }
            else
            {
                HideZones();
            }
        }

        internal void ClearTarget()
        {
            m_targetPosition = null;
        }

        private void OnPlayerActionChanged(PlayerActionEnum action)
        {
            if (m_playerMB.IsSelected)
            {
                ShowZone();
            }
            else
            {
                HideZones();
            }
        }

        internal void ShowZone()
        {
            FixState();
            CurrentState.ShowAction(this);
        }

        // Set action to move if player has not ball
        private void FixState()
        {
            if (m_playerMB.PlayerVM.SelectedPlayerAction != PlayerActionEnum.Move && !m_playerMB.PlayerVM.HasBall)
            {
                m_playerMB.PlayerVM.SelectedPlayerAction = PlayerActionEnum.Move;
            }
        }

        private IActionState CurrentState
        {
            get
            {
                if (!m_playerMB.CanMove)
                {
                    return EmptyActionState.Instance;
                }
                return m_actionStates[m_playerMB.PlayerVM.SelectedPlayerAction];
            }
        }

        private void ShowZone(Transform zone, float logicDistance)
        {
            float diameter = CoordinateConverter.ToWorld(logicDistance) * 2f;
            zone.localScale = new Vector3(diameter, diameter, 1f);
            if (m_playerMB.IsSelected)
            {
                zone.gameObject.SetActive(true);
            }
        }

        internal void SetTargetLocalPosition(Vector2 localPosition, PlayerActionEnum actionType)
        {
            this.LDebug(localPosition);
            m_targetPosition = localPosition;
            m_targetActionType = actionType;
            ShowTargetMarker();
        }

        private void ShowTargetMarker()
        {
            if (m_playerMB.IsSelected)
            {
                GameManager.Instance.TargetMarker.ShowTargetMarker(m_targetPosition, m_targetActionType);
            }
        }

        public void Dispose()
        {
            if (m_playerMB.PlayerVM != null)
            {
                m_playerMB.PlayerVM.PlayerActionChanged -= OnPlayerActionChanged;
            }
        }

        internal void ExecuteAction(Vector2 logicTargetPos)
        {
            PlayerCommand? command = CurrentState.ExecuteAction(this, logicTargetPos);
            if (command.HasValue)
                GameManager.Instance.CommandManager.Submit(command.Value);
        }

        private void ShowActionZone(float logicDistance)
        {
            ShowZone(m_actionZone, logicDistance);
        }

        private void ShowHighPassZone(float logicDistance)
        {
            ShowZone(m_highPassZone, logicDistance);
        }

        // private void HideHighPassZone()
        // {
        //     m_highPassZone.gameObject.SetActive(false);
        // }

        internal void HideZones()
        {
            m_actionZone.gameObject.SetActive(false);
            m_highPassZone.gameObject.SetActive(false);
            HomeTeamGoalZoneSetActive(false);
            AwayTeamGoalZoneSetActive(false);
        }

        internal void HomeTeamGoalZoneSetActive(bool value)
        {
            GameManager.Instance.HomeTeamGoalZone.gameObject.SetActive(value);
        }

        internal void AwayTeamGoalZoneSetActive(bool value)
        {
            GameManager.Instance.AwayTeamGoalZone.gameObject.SetActive(value);
        }

        private static readonly Dictionary<PlayerActionEnum, IActionState> m_actionStates;

        static PlayerActionContext()
        {
            m_actionStates = new()
            {
                { PlayerActionEnum.Move, new MoveActionState() },
                { PlayerActionEnum.LowPass, new LowPassActionState() },
                { PlayerActionEnum.HighPass, new HighPassActionState() },
                { PlayerActionEnum.Shot, new ShotActionState() },
            };
        }

        internal interface IActionState
        {
            public void ShowAction(PlayerActionContext parent);
            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos);
        }

        class EmptyActionState : IActionState
        {
            internal static readonly EmptyActionState Instance = new EmptyActionState();

            public void ShowAction(PlayerActionContext parent)
            {
                parent.HideZones();
                // parent.ShowActionZone(parent.PlayerState.AvailableMoveDistance);
                // parent.ShowActionZone(parent.PlayerState.MaxLowPassDistance);
                // parent.ShowActionZone(parent.PlayerState.MaxHighPassDistance);
                // parent.ShowHighPassZone(parent.PlayerState.MinHighPassDistance);
            }

            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos) => null;
        }

        class MoveActionState : IActionState
        {
            public void ShowAction(PlayerActionContext parent)
            {
                parent.HideZones();
                parent.ShowActionZone(parent.PlayerState.AvailableMoveDistance);
                parent.ShowTargetMarker();
            }

            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos)
            {
                PlayerMB mb = parent.m_playerMB;
                return new PlayerCommand(
                    GameManager.Instance.TurnManager.CurrentTurn,
                    mb.TeamIdx,
                    mb.PlayerIdx,
                    mb.PlayerId,
                    PlayerActionEnum.Move,
                    logicTargetPos
                );
            }
        }

        class LowPassActionState : IActionState
        {
            public void ShowAction(PlayerActionContext parent)
            {
                parent.HideZones();
                parent.ShowActionZone(parent.PlayerState.AvailableLowPassDistance);
                parent.ShowTargetMarker();
            }

            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos)
            {
                this.LDebug(logicTargetPos);

                PlayerMB mb = parent.m_playerMB;

                return new PlayerCommand(
                    GameManager.Instance.TurnManager.CurrentTurn,
                    mb.TeamIdx,
                    mb.PlayerIdx,
                    mb.PlayerId,
                    PlayerActionEnum.LowPass,
                    logicTargetPos
                );
            }
        }

        class HighPassActionState : IActionState
        {
            public void ShowAction(PlayerActionContext parent)
            {
                parent.HideZones();
                parent.ShowActionZone(parent.PlayerState.AvailableHighPassDistance);
                parent.ShowHighPassZone(parent.PlayerState.MinHighPassDistance);
                parent.ShowTargetMarker();
            }

            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos)
            {
                PlayerMB mb = parent.m_playerMB;
                return new PlayerCommand(
                    GameManager.Instance.TurnManager.CurrentTurn,
                    mb.TeamIdx,
                    mb.PlayerIdx,
                    mb.PlayerId,
                    PlayerActionEnum.HighPass,
                    logicTargetPos
                );
            }
        }

        class ShotActionState : IActionState
        {
            public void ShowAction(PlayerActionContext parent)
            {
                parent.HideZones();
                if (parent.PlayerState.InHomeTeam)
                    parent.AwayTeamGoalZoneSetActive(true);
                else
                    parent.HomeTeamGoalZoneSetActive(true);
                parent.ShowTargetMarker();
            }

            public PlayerCommand? ExecuteAction(PlayerActionContext parent, Vector2 logicTargetPos)
            {
                PlayerMB mb = parent.m_playerMB;
                return new PlayerCommand(
                    GameManager.Instance.TurnManager.CurrentTurn,
                    mb.TeamIdx,
                    mb.PlayerIdx,
                    mb.PlayerId,
                    PlayerActionEnum.Shot,
                    logicTargetPos
                );
            }
        }
    }
}
