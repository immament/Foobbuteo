// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game
{
    internal interface IToClientRpc_GameRoomNB
    {
        void ToClient_MvMLineupRpc(bool save, MvMLineupResult result, RpcParams rpcParams);
        void ToClients_LineupChangedRpc(GameDetailsContract details);
        void ToClient_AlertRpc(string message, RpcParams rpcParams = default);
        void ToClient_GameInfoRpc(GameDetailsContract game, RpcParams rpcParams);
        void ToClients_PlayerControlClaimedRpc(GameDetails_PlayerContract gameDetails_PlayerContract);
        void ToClients_PlayerControlStateRpc(GameDetails_PlayerContract player);
        void ToClients_TurnResultRpc(TurnResultReadyMessage msg, RpcParams rpcParams = default);
        void ToClient_PlayerCmdResponseRpc(PlayerCommandResponse playerCmdResponse, RpcParams rpcParams);
        void ToClient_MvMJoinResultRpc(MvMJoinResult result, RpcParams rpcParams);
        void ToClients_MvMRosterReadyRpc(NetFoobbuteoTeam awayTeam, GameDetailsContract details);
    }

    // CLIENT
    public partial class GameRoomNB : IClientGameRoomNB, IToClientRpc_GameRoomNB
    {
        private bool m_clientInitialized;
        private bool m_clientDetailsReady;
        private bool CanSendGameAction =>
            m_clientDetailsReady && IsSpawned && NetworkManager != null && NetworkManager.IsConnectedClient;

        private void InitClientVars()
        {
            this.LTrace(() => $"++ {this.InfoTag()}");
            this.m_nv_fixedGameInfo ??= new();
            this.m_nv_turnStage ??= new();
            this.m_nv_HomeTeam ??= new();
            this.m_nv_AwayTeam ??= new();
        }

        private void ClientDispose()
        {
            m_clientDetailsReady = false;
            m_clientInitialized = false;
            this.m_nv_fixedGameInfo.OnValueChanged -= Client_OnFixedGameInfoChanged;
            this.m_nv_turnStage.OnValueChanged -= Client_OnTurnStageChanged;
            this.m_nv_HomeTeam.OnValueChanged -= Client_OnHomeTeamChanged;
            this.m_nv_AwayTeam.OnValueChanged -= Client_OnAwayTeamChanged;
        }

        #region ++ To Client RPC +++++ IToClient_GameRoomNB ++

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClient_GameInfoRpc(GameDetailsContract game, RpcParams rpcParams)
        {
            m_clientDetailsReady = true;
            this.LogInfo(
                $"[Client ^{NetworkManager.LocalClientId}: Received game info #{game.GameId} {this.InfoTag()}"
            );
            GameDetailsReady?.Invoke(game);
        }

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClient_AlertRpc(string message, RpcParams _)
        {
            AppCtx.Client.ShowToast(message);
        }

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClient_PlayerCmdResponseRpc(
            PlayerCommandResponse playerCmdResponse,
            RpcParams rpcParams
        )
        {
            // ClientForServer.PlayerCmdResponse(playerCmdResponse);
            CommandResponseReceived?.Invoke(playerCmdResponse);
        }

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClients_TurnResultRpc(TurnResultReadyMessage msg, RpcParams _)
        {
            this.LogDebug(() => $"[Client ^{NetworkManager.LocalClientId}: Received: {msg}");
            // ClientForServer.InvokeTurnResultReady(msg);
            TurnResultReady?.Invoke(msg);
            // this.Turn
        }

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClients_PlayerControlClaimedRpc(GameDetails_PlayerContract player)
        {
            this.LogDebug("++");
            // ClientForServer.InvokePlayerControlClaimed(gameDetails_PlayerContract);
            PlayerControlClaimed?.Invoke(player);
        }

        #endregion -- ToClients RPC ------

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClients_PlayerControlStateRpc(GameDetails_PlayerContract player)
        {
            PlayerControlStateChanged?.Invoke(player);
        }

        #region  ++ IClientGameRoomNB ++

        public event Action<GameDetailsContract> GameDetailsReady;
        public event Action<TurnStateChangedMessage> TurnStarted;
        public event Action<TurnStateChangedMessage> ProcessingStarted;
        public event Action<TurnResultReadyMessage> TurnResultReady;
        public event Action<PlayerCommandResponse> CommandResponseReceived;
        public event Action<GameDetails_PlayerContract> PlayerControlClaimed;
        public event Action<GameDetails_PlayerContract> PlayerControlStateChanged;
        public event Action<TeamIndex, NetFoobbuteoTeam> TeamInfoChanged;

        public event Action<TurnStateChangedMessage> GameEnded;
        public event Action<MvMJoinResult> MvMJoinCompleted;

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClient_MvMJoinResultRpc(MvMJoinResult result, RpcParams rpcParams) =>
            MvMJoinCompleted?.Invoke(result);

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClients_MvMRosterReadyRpc(NetFoobbuteoTeam awayTeam, GameDetailsContract details)
        {
            TeamInfoChanged?.Invoke(TeamIndex.Away, awayTeam);
            GameDetailsReady?.Invoke(details);
        }

        void IClientGameRoomNB.JoinMvMGame(TeamKey team)
        {
            if (CanSendGameAction)
                ToServer.ToServer_JoinMvMGameRpc(team);
        }

        void IClientGameRoomNB.InitClient()
        {
            if (m_clientInitialized)
                return;
            m_clientInitialized = true;
            this.LTrace(() => $"{this.InfoTag()}");
            this.m_nv_fixedGameInfo.OnValueChanged += Client_OnFixedGameInfoChanged;
            this.m_nv_turnStage.OnValueChanged += Client_OnTurnStageChanged;
            this.m_nv_HomeTeam.OnValueChanged += Client_OnHomeTeamChanged;
            this.m_nv_AwayTeam.OnValueChanged += Client_OnAwayTeamChanged;
        }

        void IClientGameRoomNB.UserJoinedRoom()
        {
            this.LTrace(() => $"{this.InfoTag()}");
            this.ToServer.ToServer_UserJoinedRoomRpc();
        }

        void IClientGameRoomNB.RequestLeaveGame()
        {
            if (!CanSendGameAction)
                return;
            this.LTrace(() => $"{this.InfoTag()}");
            this.ToServer.ToServer_RequestLeaveGameRpc();
        }

        BaseGameInfo IClientGameRoomNB.GetBaseGameInfo()
        {
            return new BaseGameInfo(
                this.m_nv_fixedGameInfo.Value,
                this.m_nv_turnStage.Value,
                this.m_nv_HomeTeam.Value,
                this.m_nv_AwayTeam.Value
            );
        }

        void IClientGameRoomNB.SendMoveCmd(PlayerCommand command)
        {
            if (!CanSendGameAction)
                return;
            this.ToServer.ToServer_MoveCmdRpc(command);
        }

        void IClientGameRoomNB.SendModeCmd(PlayerModeCommand command)
        {
            if (!CanSendGameAction)
                return;
            this.ToServer.ToServer_ModeCmdRpc(command);
        }

        void IClientGameRoomNB.RequestPlayerControl(PlayerKey playerKey)
        {
            if (!CanSendGameAction)
                return;
            this.ToServer.ToServer_ClaimPlayerRpc(playerKey);
        }

        #endregion -- IClientGameRoomNB --


        private void Client_OnHomeTeamChanged(NetFoobbuteoTeam _, NetFoobbuteoTeam newValue) =>
            TeamInfoChanged?.Invoke(TeamIndex.Home, newValue);

        private void Client_OnAwayTeamChanged(NetFoobbuteoTeam _, NetFoobbuteoTeam newValue) =>
            TeamInfoChanged?.Invoke(TeamIndex.Away, newValue);

        // NetworkVariable changed
        private void Client_OnTurnStageChanged(TurnStageNetState preValue, TurnStageNetState newValue)
        {
            if (preValue.GameStatus != newValue.GameStatus)
            {
                this.LogDebug(() =>
                    $"{this.InfoTag()} GAME STATUS: {preValue.GameStatus}=>{newValue.GameStatus}, Stage: {preValue.Stage}=>{newValue.Stage}, Turn: {preValue.CurrentTurn}=>{newValue.CurrentTurn}, Times: [ StartTime: {preValue.StageStartTime:F1}=>{newValue.StageStartTime:F1}, EndTime: {newValue.StageEndTime:F1}]"
                );
                switch (newValue.GameStatus)
                {
                    case FoobbuteoGameStatus.Ended:
                        GameEnded?.Invoke(
                            new TurnStateChangedMessage(
                                newValue.GameStatus,
                                newValue.Stage,
                                newValue.CurrentTurn,
                                newValue.StageEndTime
                            )
                        );
                        return;
                }
            }

            if (preValue.Stage != newValue.Stage)
            {
                this.LogDebug(() =>
                    $"{this.InfoTag()} Game status: {preValue.GameStatus}=>{newValue.GameStatus}, STAGE: {preValue.Stage}=>{newValue.Stage}, Turn: {preValue.CurrentTurn}=>{newValue.CurrentTurn}, Times: [ StartTime: {preValue.StageStartTime:F1}=>{newValue.StageStartTime:F1}, EndTime: {newValue.StageEndTime:F1}]"
                );
                switch (newValue.Stage)
                {
                    case TurnStageEnum.PlayerMove:
                        TurnStarted?.Invoke(
                            new TurnStateChangedMessage(
                                newValue.GameStatus,
                                newValue.Stage,
                                newValue.CurrentTurn,
                                newValue.StageEndTime
                            )
                        );
                        break;
                    case TurnStageEnum.Processing:
                        ProcessingStarted?.Invoke(
                            new TurnStateChangedMessage(
                                newValue.GameStatus,
                                newValue.Stage,
                                newValue.CurrentTurn,
                                newValue.StageEndTime
                            )
                        );
                        break;
                }
            }
        }
    }
}
