// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Common.Utils;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    internal interface IToServerRpc_GameRoomNB
    {
        void ToServer_MvMLineupRpc(bool save, int[] expected, int[] desired, RpcParams rpcParams = default);
        void ToServer_UserJoinedRoomRpc(RpcParams rpcParams = default);
        void ToServer_RequestLeaveGameRpc(RpcParams rpcParams = default);
        void ToServer_MoveCmdRpc(PlayerCommand cmd, RpcParams rpcParams = default);
        void ToServer_ModeCmdRpc(PlayerModeCommand cmd, RpcParams rpcParams = default);
        void ToServer_ClaimPlayerRpc(PlayerKey playerKey, RpcParams rpcParams = default);
        void ToServer_JoinMvMGameRpc(TeamKey team, RpcParams rpcParams = default);
    }

    // SERVER
    public partial class GameRoomNB : IToServerRpc_GameRoomNB, IServerGameRoomNB
    {
        [SerializeReference]
        private IGameEngine m_serverGameEngine;
        private NetworkObject m_netObject;

        private void StartServerGame()
        {
            this.LogInfo(this.InfoTag());
            m_netObject = GetComponent<NetworkObject>();
            m_serverGameEngine.InitGame();
        }

        private void NetworkShow(ulong clientId)
        {
            if (!m_netObject.IsNetworkVisibleTo(clientId))
                m_netObject.NetworkShow(clientId);
        }

        private void NetworkHide(ulong clientId)
        {
            if (m_netObject.IsNetworkVisibleTo(clientId))
                m_netObject.NetworkHide(clientId);
        }

        #region ++ To Server RPC +++++

        private bool AllowIncomingRpc(ulong clientId)
        {
            if (ServerRpcRateLimiter.TryConsume(clientId, out bool shouldLogWarning))
                return true;

            if (shouldLogWarning)
                this.LWarn($"{this.InfoTag()} RPC rate limit exceeded by client ^{clientId}");
            return false;
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_UserJoinedRoomRpc(RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            this.LogInfo(this.InfoTag());
            ToClient.ToClient_GameInfoRpc(
                m_serverGameEngine.ToGameDetailsContract(),
                RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)
            );
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_RequestLeaveGameRpc(RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            this.LogInfo($"{this.InfoTag()} clientId: ^{rpcParams.Receive.SenderClientId}");
            // ServerForClient.RequestRemoveClientFromGameRoom(rpcParams.Receive.SenderClientId);
            try
            {
                m_serverGameEngine.RemoveClientFromGame(rpcParams.Receive.SenderClientId);
            }
            catch (AppException exception)
            {
                ToClient.ToClient_AlertRpc(
                    exception.Message,
                    RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)
                );
            }
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_MoveCmdRpc(PlayerCommand cmd, RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            _ = HandleMoveCommandAsync(cmd, rpcParams.Receive.SenderClientId);
        }

        private async Task HandleMoveCommandAsync(PlayerCommand cmd, ulong clientId)
        {
            this.LogInfo(cmd);
            this.ToClient.ToClient_PlayerCmdResponseRpc(
                await m_serverGameEngine.AddPlayerMoveAsync(cmd, clientId),
                this.RpcTarget.Single(clientId, RpcTargetUse.Temp)
            );
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_ModeCmdRpc(PlayerModeCommand cmd, RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            _ = HandleModeCommandAsync(cmd, rpcParams.Receive.SenderClientId);
        }

        private async Task HandleModeCommandAsync(PlayerModeCommand cmd, ulong clientId)
        {
            this.LogInfo($"{this.InfoTag()}, {cmd}");
            try
            {
                await m_serverGameEngine.ChangePlayerModeAsync(cmd, clientId);
            }
            catch (AppException e)
            {
                this.LWarn($"{this.InfoTag()}: {e.Message}");
                this.LException(e);
                ToClient.ToClient_AlertRpc(e.Message, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_ClaimPlayerRpc(PlayerKey playerKey, RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
                return;

            _ = HandleClaimPlayerAsync(playerKey, rpcParams.Receive.SenderClientId);
        }

        private async Task HandleClaimPlayerAsync(PlayerKey playerKey, ulong clientId)
        {
            this.LogInfo($"{this.InfoTag()}, Client: ^{clientId} claim: {playerKey}");
            try
            {
                var player = await m_serverGameEngine.UserClaimPlayerAsync(clientId, playerKey);
                if (player != null)
                {
                    this.LDebug(() => $"{this.InfoTag()}, {player.Slot}");
                    this.ToClient.ToClients_PlayerControlClaimedRpc(player.ToContract());
                }
            }
            catch (AppException e)
            {
                this.LExceptionTree(e, this.InfoTag());
                ToClient.ToClient_AlertRpc(e.Message, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_JoinMvMGameRpc(TeamKey team, RpcParams rpcParams)
        {
            if (!AllowIncomingRpc(rpcParams.Receive.SenderClientId))
            {
                ToClient.ToClient_MvMJoinResultRpc(
                    new MvMJoinResult(false, "Please try again shortly."),
                    RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)
                );
                return;
            }
            _ = HandleMvMJoinAsync(team, rpcParams.Receive.SenderClientId);
        }

        private async Task HandleMvMJoinAsync(TeamKey team, ulong clientId)
        {
            MvMJoinResult result;
            try
            {
                await m_serverGameEngine.JoinMvMGameAsync(clientId, team);
                if (!IsSpawned)
                    return;
                ToClient.ToClients_MvMRosterReadyRpc(
                    m_serverGameEngine.ToNetAwayTeam,
                    m_serverGameEngine.ToGameDetailsContract()
                );
                result = new MvMJoinResult(true);
            }
            catch (AppException exception)
            {
                result = new MvMJoinResult(false, exception.Message);
            }
            catch (Exception exception)
            {
                this.LExceptionTree(exception, $"Join MvM game failed {team}");
                result = new MvMJoinResult(false, "The team could not be joined. Please try again.");
            }
            if (IsSpawned)
                ToClient.ToClient_MvMJoinResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        #endregion -- To Server RPC ------

        // [Serializable]
        // public class ServerGameRoomNBCtrl : IServerForServerGameRoomNB, IServerForClientGameRoomNB
        // {
        // public ServerGameRoomNBCtrl(GameRoomNB gameRoomNB)
        // {
        //     m_nb = gameRoomNB;
        // }

        // private readonly GameRoomNB m_nb;

        #region ++ IServerForServerGameRoomNB ++
        // FoobbuteoMainServer server, FoobbuteoGameState game
        // Initialized from matchEngine
        void IServerGameRoomNB.InitServer(IGameEngine gameEngine)
        {
            this.LogInfo(this.InfoTag());
            m_serverGameEngine = gameEngine;

            this.m_nv_fixedGameInfo = new(m_serverGameEngine.ToFixedGameInfoNetState(BaseServerTime));
            this.m_nv_turnStage = new(m_serverGameEngine.GetTurnStage());
            this.m_nv_HomeTeam = new(m_serverGameEngine.ToNetHomeTeam);
            this.m_nv_AwayTeam = new(m_serverGameEngine.ToNetAwayTeam);

            m_serverGameEngine.TurnStarted += Server_OnTurnStarted;
            m_serverGameEngine.TurnResultReady += Server_OnTurnResultReady;
            m_serverGameEngine.ProcessingStarted += Server_OnTurnProcessingStarted;
            m_serverGameEngine.GameEnded += Server_OnGameEnded;
        }

        void IServerGameRoomNB.AddClientToGameRoom(ulong clientId)
        {
            if (IsHostClient(clientId))
            {
                this.LDebug(this.InfoTag());
                // TODO: On Host Only - move in better place!! (Client?)
                ServiceLocator.Current.Get<NetworkObjectRegistry>().SetActive<IClientGameRoomNB>(this);
                AppCtx.Client.OpenGameScene(GameId);
            }
            else
            {
                this.LDebug(() => $"Client: ^{clientId}");
                NetworkShow(clientId);
            }
        }

        void IServerGameRoomNB.UpdateStartTime() =>
            m_nv_fixedGameInfo.Value = m_serverGameEngine.ToFixedGameInfoNetState(BaseServerTime);

        void IServerGameRoomNB.UpdateTeamInfo(TeamIndex teamIdx)
        {
            if (teamIdx == TeamIndex.Home)
                m_nv_HomeTeam.Value = m_serverGameEngine.ToNetHomeTeam;
            else
                m_nv_AwayTeam.Value = m_serverGameEngine.ToNetAwayTeam;
        }

        void IServerGameRoomNB.UpdatePlayerControlState(GameDetails_PlayerContract player)
        {
            if (!IsSpawned)
                return;
            ToClient.ToClients_PlayerControlStateRpc(player);
        }

        void IServerGameRoomNB.CloseGameRoom(ulong clientId)
        {
            if (IsHostClient(clientId))
            {
                this.LDebug(() => $"{this.InfoTag()}, Client ^{clientId} leave room ${GameId}! {{Host}}");
                // TODO: On Host Only - move in better place!! (Client?)
                if (ServiceLocator.Current.Get<NetworkObjectRegistry>().Unregister<IClientGameRoomNB>(this))
                    AppCtx.Client.OpenLobbyScene();
            }
            else
            {
                NetworkHide(clientId);
                this.LDebug(() => $"Client ^{clientId} leave room ${GameId}. {{NetworkHide}}");
            }
        }

        void IServerGameRoomNB.Close()
        {
            m_netObject.Despawn();
        }

        Coroutine IServerGameRoomNB.StartGameCoroutine(IEnumerator routine)
        {
            return this.StartCoroutine(routine);
        }

        #endregion -- IServerForServerGameRoomNB --


        private void ServerDispose()
        {
            this.LTrace(this.InfoTag());
            if (m_serverGameEngine != null)
            {
                m_serverGameEngine.TurnStarted -= Server_OnTurnStarted;
                m_serverGameEngine.TurnResultReady -= Server_OnTurnResultReady;
                m_serverGameEngine.ProcessingStarted -= Server_OnTurnProcessingStarted;
                m_serverGameEngine.GameEnded -= Server_OnGameEnded;
            }
        }

        private long BaseServerTime =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)NetworkManager.Singleton.ServerTime.Time;

        private bool IsHostClient(ulong clientId) => (NetworkManager.ServerClientId == clientId);

#if UNITY_EDITOR
        public bool EditorTryGetPlayerPosition(TeamIndex teamIdx, byte playerIdx, out Vector2 position)
        {
            position = default;
            return m_serverGameEngine != null
                && m_serverGameEngine.EditorTryGetPlayerPosition(teamIdx, playerIdx, out position);
        }

        public bool EditorTryGetBallPosition(out Vector2 position)
        {
            position = default;
            if (m_serverGameEngine == null)
                return false;

            position = m_serverGameEngine.EditorGetBallPosition();
            return true;
        }

        public string EditorQueuePlayerMove(TeamIndex teamIdx, byte playerIdx, Vector2 target) =>
            m_serverGameEngine == null
                ? "Server game is not initialized."
                : m_serverGameEngine.EditorQueuePlayerMove(teamIdx, playerIdx, target);

        public string EditorMoveBall(Vector2 target) =>
            m_serverGameEngine == null ? "Server game is not initialized." : m_serverGameEngine.EditorMoveBall(target);

        public void EditorRefreshPositioningBounds()
        {
            m_serverGameEngine?.EditorRefreshPositioningBounds();
        }
#endif

        #region ++ game engine events ++

        private void Server_OnTurnStarted()
        {
            this.LogTrace(this.InfoTag());
            this.m_nv_turnStage.Value = m_serverGameEngine.GetTurnStage();
        }

        private void Server_OnTurnProcessingStarted(ProcessingStartedMessage message)
        {
            this.LogTrace(() => $"{this.InfoTag()}, TurnNumber: {message.TurnNumber}");
            this.m_nv_turnStage.Value = m_serverGameEngine.GetTurnStage();
            // ProcessingStarted?.Invoke(message);
            // TODO:
        }

        private void Server_OnTurnResultReady(TurnResultReadyMessage message)
        {
            this.LogTrace(() => $"{this.InfoTag()}, TurnNumber: {message.TurnNumber}");
            this.m_nv_turnStage.Value = m_serverGameEngine.GetTurnStage();
            this.ToClient.ToClients_TurnResultRpc(message);
        }

        private void Server_OnGameEnded()
        {
            this.LogTrace(() => $"{this.InfoTag()}");
            this.m_nv_turnStage.Value = m_serverGameEngine.GetTurnStage();
        }

        #endregion -- game engine events --
    }
}
