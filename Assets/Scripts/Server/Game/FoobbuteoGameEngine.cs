// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game
{
    [Serializable]
    internal partial class FoobbuteoGameEngine : IGameEngine
    {
        [SerializeReference]
        private ServerGameState m_gameState;
        internal int GameId => m_gameState.GameId;
        internal ServerGameState GameState => m_gameState;
        private readonly FoobbuteoMainServer m_foobbuteoServer;
        private readonly IServerGameRoomNB m_serverGameRoomNB;
        internal IServerGameRoomNB ServerGameRoomNB => m_serverGameRoomNB;
        private readonly IUserRegistry m_userRegistry;
        private readonly IGamePersistenceRepository m_persistenceRepository;
        private readonly SemaphoreSlim m_persistenceGate = new(1, 1);
        private readonly TimeSpan m_persistenceRetryDelay;

        private readonly float m_playerMoveTime;
        private readonly List<PlayerCommand> m_commands;
        private readonly TurnProcessor m_turnProcessor;
        private readonly PlayerActivityTracker m_activity;

        public event Action GameStarted;
        public event Action TurnStarted;
        public event Action<ProcessingStartedMessage> ProcessingStarted;
        public event Action<TurnResultReadyMessage> TurnResultReady;
        public event Action GameEnded;
        internal event Action<ServerUser> PlayerClaimReplaced;

        internal FoobbuteoGameEngine(
            FoobbuteoMainServer foobbuteoServer,
            IServerGameRoomNB gameRoomNB,
            ServerGameState gameState,
            TimeSpan? persistenceRetryDelay = null
        )
        {
            ImmLogger.AddType(typeof(FoobbuteoGameEngine), "GameEngine");
            // if (ColorUtility.TryParseHtmlString("#a3ecfa", out var color))
            // {
            //     ImmLogger.SetLoggerOptions<FoobbuteoGameEngine>(ImmLogger.Level, color);
            // }
            m_foobbuteoServer = foobbuteoServer;
            m_serverGameRoomNB = gameRoomNB;
            m_gameState = gameState;
            m_persistenceRetryDelay = persistenceRetryDelay ?? TimeSpan.FromSeconds(5);
            m_commands = new List<PlayerCommand>(gameState.PendingCommands);
            m_turnProcessor = new TurnProcessor(m_gameState, m_commands);
            m_playerMoveTime = ServerConfig.Config.PlayerMoveTime;
            this.LInfo($"Config: {{ PlayerMoveTime: m_playerMoveTime}}");
            FixBallPosition();
            m_userRegistry = ServiceLocator.Current.Get<IUserRegistry>();
            m_persistenceRepository = ServiceLocator.Current.Get<IGamePersistenceRepository>();
            m_activity = new PlayerActivityTracker(
                gameState,
                m_userRegistry,
                ServerConfig.Config.GetInactiveAfterTurns()
            );
            InitializeAfterLoad();
            m_serverGameRoomNB.InitServer(this);
            m_userRegistry.ConnectionChanged += OnUserConnectionChanged;
        }

        internal void InitializeAfterLoad()
        {
            InitializeTeam(m_gameState.HomeTeam);
            InitializeTeam(m_gameState.AwayTeam);
            RefreshConnections();

            void InitializeTeam(ServerTeamState team)
            {
                foreach (ServerPlayerState player in team.Players)
                {
                    if (player.IsUser)
                    {
                        player.RegisterActivity(m_gameState.CurrentTurn);
                        player.SetCaptain(m_gameState.MatchType != FoobbuteoGameType.MvM && player.UserId == team.Id);
                    }
                    else
                    {
                        player.SetCaptain(false);
                    }
                }
            }
        }

        private void FixBallPosition()
        {
            if (m_gameState.Status == FoobbuteoGameStatus.Ongoing)
            {
                this.LTrace(m_gameState.Status);
                var playerWithBall = m_gameState.GetPlayerWithBall();
                if (
                    playerWithBall != null
                    && (playerWithBall.Position - m_gameState.BallPosition).sqrMagnitude > 16f * 16f
                )
                {
                    this.LTrace(playerWithBall.Position);
                    m_gameState.MoveBall(playerWithBall.BallSlotPosition);
                }
            }
        }

        public void Dispose()
        {
            StopConnectionTracking();
            m_persistenceGate.Dispose();
            this.LTrace("--");
        }

#if UNITY_EDITOR
        public bool EditorTryGetPlayerPosition(TeamIndex teamIdx, byte playerIdx, out Vector2 position)
        {
            position = default;
            if (playerIdx is < 1 or > 11)
                return false;

            position = m_gameState.Team(teamIdx).GetPlayerAtIdx(playerIdx).Position;
            return true;
        }

        public Vector2 EditorGetBallPosition() => m_gameState.BallPosition;

        public string EditorQueuePlayerMove(TeamIndex teamIdx, byte playerIdx, Vector2 target) =>
            m_turnProcessor.EditorQueuePlayerMove(teamIdx, playerIdx, target);

        public string EditorMoveBall(Vector2 target)
        {
            if (!target.IsOnPitch())
                return $"Ball position is outside the pitch: {target}";

            m_gameState.ClearPlayerWithBall();
            m_gameState.MoveBall(target);
            m_gameState.UpdatePositioningBounds();
            return null;
        }

        public void EditorRefreshPositioningBounds() => m_gameState.UpdatePositioningBounds();
#endif

        public async Task<FoobbuteoPlayerState> UserClaimPlayerAsync(ulong clientId, PlayerKey playerKey)
        {
            // Creating/joining a match and claiming a PvP player must not assign the same user concurrently.
            if (m_foobbuteoServer != null)
                await m_foobbuteoServer.WaitForParticipationChangeAsync();
            try
            {
                return await ClaimPlayerAndPersistAsync(clientId, playerKey);
            }
            finally
            {
                m_foobbuteoServer?.ReleaseParticipationChange();
            }
        }

        private async Task<FoobbuteoPlayerState> ClaimPlayerAndPersistAsync(ulong clientId, PlayerKey playerKey)
        {
            await m_persistenceGate.WaitAsync();
            try
            {
                if (!m_userRegistry.TryGetUser(clientId, out var user))
                    throw new AppException($"Client ^{clientId} not found");

                m_foobbuteoServer?.EnsureNoParticipation(user);
                if (!m_gameState.CanClaimPlayer(user, playerKey, out string reason))
                    throw new AppException(reason);

                ServerPlayerState previousPlayer = m_gameState.GetPlayer(playerKey);
                int previousUserId = previousPlayer.UserId;
                float actionPoints = previousPlayer.ActionPoints;
                var team = m_gameState.Team(playerKey.TeamIdx);
                CaptainAssignment? captain = m_activity.FindCaptainAfterClaim(team, previousPlayer, user);
                bool isCaptain = captain.HasValue ? captain.Value.UserId == user.UserId : user.UserId == team.Id;
                GameDetails_PlayerContract? claimedContract = await PersistOrThrow(
                    m_persistenceRepository.SavePlayerClaimAsync(GameId, playerKey, user, isCaptain)
                );
                var player = m_gameState.ClaimPlayer(user, playerKey);
                if (claimedContract.HasValue)
                {
                    GameDetails_PlayerContract contract = claimedContract.Value;
                    contract.ClientId = user.ClientId;
                    player.OnPlayerControlClaimed(contract);
                }
                player.RestoreActionPoints(actionPoints);
                player.RegisterActivity(m_gameState.CurrentTurn);
                player.SetConnection(user.ClientId);
                // Persisted commands belong to the pitch slot even if its player identity changes.
                for (int index = 0; index < m_commands.Count; index++)
                {
                    PlayerCommand command = m_commands[index];
                    if (command.TeamIdx != playerKey.TeamIdx || command.PlayerIdx != playerKey.PlayerIdx)
                        continue;
                    m_commands[index] = command.WithPlayerId(player.PlayerId);
                }
                if (
                    previousUserId > 0
                    && previousUserId != user.UserId
                    && m_userRegistry.TryGetUser(previousUserId, out var previousUser)
                    && previousUser.GameSlot.HasValue
                    && previousUser.GameSlot.Value == player.Slot
                )
                    PlayerClaimReplaced?.Invoke(previousUser);
                if (captain.HasValue)
                {
                    // The claim has already succeeded. Retry only the team update if it fails.
                    await PersistCaptainAsync(captain.Value);
                    ApplyCaptain(captain);
                }
                PublishPlayer(player);
                m_foobbuteoServer?.NotifyAboutUserChange(clientId, user);
                return player;
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        internal async Task JoinMvMGameAsync(ServerUser user, TeamKey teamKey, IGamesRepository repository)
        {
            await m_persistenceGate.WaitAsync();
            try
            {
                bool alreadyJoined =
                    m_gameState.Status == FoobbuteoGameStatus.Pending
                    && m_gameState.AwayTeam.Id == user.UserId
                    && m_gameState.AwayTeam.TeamKey == teamKey;
                if (!alreadyJoined && !m_gameState.CanJoinAsManager(user, teamKey, out string reason))
                    throw new AppException(reason);
                var joined = await repository.JoinMvMGameAsync(GameId, teamKey, user);
                var team = joined.Team;
                if (team.TeamIdx != TeamIndex.Away || team.Id != user.UserId || team.TeamKey != teamKey)
                    throw new RepositoryUnavailableException("The persisted away team does not match the request.");
                ServerTeamState.ValidateManagedRoster(GameId, team);
                m_gameState.SetManagedTeam(team);
                m_gameState.RescheduleStart(joined.StartDateTime);
                m_serverGameRoomNB.UpdateStartTime();
                m_serverGameRoomNB.UpdateTeamInfo(TeamIndex.Away);
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        public Task JoinMvMGameAsync(ulong senderClientId, TeamKey team) =>
            m_foobbuteoServer.JoinMvMGameAsync(senderClientId, GameId, team);

        public async Task<PlayerCommandResponse> AddPlayerMoveAsync(PlayerCommand cmd, ulong clientId)
        {
            await m_persistenceGate.WaitAsync();
            try
            {
                if (ValidateCommand(cmd) is string validationError)
                    return PlayerCommandResponse.Reject(cmd, validationError);

                //     if (m_gameState.TurnStage != TurnStageEnum.PlayerMove)
                //         return PlayerCommandResponse.Reject(
                //             cmd,
                //             $"Player can move only during 'Player Move' turn stage. Current stage: '{m_gameState.TurnStage}'"
                //         );

                //     if (!m_userRegistry.TryGetUser(clientId, out var user))
                //         return PlayerCommandResponse.Reject(cmd, $"User not found for client ^{clientId})");

                //     if (user.InGameRoomId != GameId)
                //         return Reject($"User is not in room ${GameId}");

                //     if (!user.GameSlot.HasValue)
                //         return Reject($"User is not control any player");

                //     if (user.GameSlot.Value.GameId != GameId)
                //         return Reject($"User control player in other game {user.GameSlot.Value.GameId}");

                //     if (user.GameSlot.Value.PlayerKey.TeamIdx != cmd.TeamIdx)
                //         return Reject($"Can not control player in opponent team ({cmd.TeamIdx}/{cmd.PlayerIdx})");

                //     var movePlayer = m_gameState.GetPlayer(cmd.TeamIdx, cmd.PlayerIdx);
                //     if (user.GameSlot.Value.PlayerKey.PlayerIdx != cmd.PlayerIdx)
                //     {
                //         if (movePlayer.IsUser)
                //         {
                //             return Reject($"Can not control players of other user {movePlayer.Slot}");
                //         }
                //         var userPlayer = m_gameState.GetPlayer(user.GameSlot.Value.PlayerKey);
                //         if (!userPlayer.IsCaptain)
                //         {
                //             return Reject($"Can not control player {movePlayer.Slot}. Only captain can control bots. ");
                //         }
                //     }
                var movePlayer = GetPlayer(cmd, clientId, out var reason);
                if (movePlayer == null)
                    return PlayerCommandResponse.Reject(cmd, reason);

                float previousActionPoints = movePlayer.ActionPoints;
                bool hadBall = movePlayer.HasBall;
                if (movePlayer.Server_RegisterMoveCmd(ref cmd) is string registerResult)
                    return PlayerCommandResponse.Reject(cmd, registerResult);

                try
                {
                    ServerUser actingUser = m_userRegistry.GetUser(clientId);
                    await m_persistenceRepository.SavePlayerCommandAsync(GameId, cmd, hadBall);
                    RegisterUserActivity(actingUser.UserId);
                }
                catch (RepositoryUnavailableException ex)
                {
                    movePlayer.RestoreActionPoints(previousActionPoints);
                    this.LException(ex);
                    return PlayerCommandResponse.Reject(cmd, "Games database is unavailable.");
                }

                m_commands.Add(cmd);
                return PlayerCommandResponse.Accept(cmd, movePlayer.ActionPoints);
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        private ServerPlayerState GetPlayer(IPlayerCommand cmd, ulong clientId, out string reason)
        {
            ServerUser user = null;
            reason = null;
            if (!m_userRegistry.TryGetUser(clientId, out user))
            {
                reason = Reason($"User not found for client ^{clientId})");
                return null;
            }

            var movePlayer = m_gameState.GetPlayerForCommand(cmd, user, out reason);
            if (movePlayer == null)
            {
                reason = Reason(reason);
                return null;
            }
            if (cmd.PlayerId != movePlayer.PlayerId)
            {
                reason = "The player identity has changed. Select the player again.";
                return null;
            }
            return movePlayer;

            string Reason(string msg) => $"[Client ^{clientId}, User @{user?.UserId}] {msg}";
        }

        public async Task ChangePlayerModeAsync(PlayerModeCommand cmd, ulong clientId)
        {
            await m_persistenceGate.WaitAsync();
            try
            {
                if (ValidateCommand(cmd) is string validationError)
                    throw new AppException(validationError);

                // if (m_gameState.Status != FoobbuteoGameStatus.Ongoing)
                //     throw new AppException("Client ^{clientId} not found");
                var player = GetPlayer(cmd, clientId, out var reason) ?? throw new AppException(reason);
                ServerUser actingUser = m_userRegistry.GetUser(clientId);
                await PersistOrThrow(m_persistenceRepository.SavePlayerModeAsync(GameId, cmd));
                player.ChangePlayerMode(cmd.Mode);
                RegisterUserActivity(actingUser.UserId);
                if (player.UserId != actingUser.UserId)
                    PublishPlayer(player);
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        private string ValidateCommand(PlayerCommand cmd)
        {
            if (ValidateCommand((IPlayerCommand)cmd) is string reason)
                return reason;

            if (
                float.IsNaN(cmd.TargetPosition.x)
                || float.IsInfinity(cmd.TargetPosition.x)
                || float.IsNaN(cmd.TargetPosition.y)
                || float.IsInfinity(cmd.TargetPosition.y)
            )
                return $"Invalid target position: {cmd.TargetPosition}";

            return cmd.Action switch
            {
                PlayerActionEnum.Move
                or PlayerActionEnum.LowPass
                or PlayerActionEnum.HighPass
                or PlayerActionEnum.Shot => null,
                _ => $"Invalid player action: {cmd.Action}",
            };
        }

        private string ValidateCommand(PlayerModeCommand cmd)
        {
            if (ValidateCommand((IPlayerCommand)cmd) is string reason)
                return reason;

            return cmd.Mode switch
            {
                PlayerModeEnum.GkSave
                or PlayerModeEnum.GkOutOfArea
                or PlayerModeEnum.GkFieldPlayer
                or PlayerModeEnum.Confrontation
                or PlayerModeEnum.Interception => null,
                _ => $"Invalid player mode: {cmd.Mode}",
            };
        }

        private string ValidateCommand(IPlayerCommand cmd)
        {
            if (m_gameState.TurnStage != TurnStageEnum.PlayerMove)
                return $"Player commands are not accepted during {m_gameState.TurnStage}.";

            if (cmd.TeamIdx != TeamIndex.Home && cmd.TeamIdx != TeamIndex.Away)
                return $"Invalid team index: {cmd.TeamIdx}";

            if (cmd.PlayerIdx < 1 || cmd.PlayerIdx > 11)
                return $"Invalid player index: {cmd.PlayerIdx}";

            if (cmd.Turn != m_gameState.CurrentTurn)
                return $"Command turn {cmd.Turn} does not match current turn {m_gameState.CurrentTurn}";

            if (cmd.PlayerId < 1)
                return $"Invalid player id: {cmd.PlayerId}";

            return null;
        }

        public void InitGame()
        {
            switch (m_gameState.Status)
            {
                case FoobbuteoGameStatus.Ongoing:
                case FoobbuteoGameStatus.Pending:
                    m_serverGameRoomNB.StartGameCoroutine(GameLoop());
                    break;
            }
        }

        private IEnumerator GameLoop()
        {
            if (m_gameState.Status == FoobbuteoGameStatus.Pending)
            {
                while (!TryStartGame(DateTimeOffset.UtcNow))
                    yield return null;
                GameStarted?.Invoke();
            }

            this.LInfo($"{InfoTag} ++ Started ++ InputTime: {m_playerMoveTime}");

            while (m_gameState.NextTurn())
            {
                // ++ NEXT TURN (Player move time)
                m_gameState.PlayerMoveStage(NetworkManager.Singleton.ServerTime.TimeAsFloat, m_playerMoveTime);
                RestorePendingCommandCosts();
                this.LDebug(() => $"{InfoTag} ++ MatchLoop Next Turn started ++");
                TurnStarted?.Invoke();
                this.LTrace(() => $"{InfoTag} Wait InputTime: {m_playerMoveTime}s");
                yield return new WaitForSeconds(m_playerMoveTime);

                // ++ ProcessingStarted
                this.LInfo($"{InfoTag} ProcessingStarted");
                m_gameState.ProcessingStarted(NetworkManager.Singleton.ServerTime.TimeAsFloat);
                ProcessingStarted?.Invoke(new ProcessingStartedMessage(m_gameState.CurrentTurn));

                Task<TurnResult> processingTask = ProcessAndPersistTurnAsync();
                while (!processingTask.IsCompleted)
                    yield return null;
                TurnResult result = processingTask.GetAwaiter().GetResult();
                var playbackDuration = result.PlaybackDuration + 2f;

                // ++ End processing (Display)
                TurnResultReady?.Invoke(
                    new TurnResultReadyMessage(
                        m_gameState.CurrentTurn,
                        NetworkManager.Singleton.ServerTime.TimeAsFloat,
                        playbackDuration,
                        result
                    )
                );

                this.LTrace(() =>
                    $"{InfoTag} TurnResultReady, wait playbackDuration: {playbackDuration:F0}s, ev:{result.Events.Length}"
                );
                yield return new WaitForSeconds(playbackDuration);
            }
            this.LInfo($"{InfoTag}-- End #{m_gameState.GameId} --");
            m_gameState.EndGame();
            GameEnded?.Invoke();
        }

        private void RestorePendingCommandCosts()
        {
            if (m_commands.Count == 0)
                return;

            for (int index = 0; index < m_commands.Count; index++)
            {
                PlayerCommand command = m_commands[index];
                ServerPlayerState player = m_gameState.GetPlayer(command.TeamIdx, command.PlayerIdx);
                string error = player == null ? "Player not found." : player.Server_RegisterMoveCmd(ref command);
                if (error != null)
                {
                    this.LError($"{InfoTag} Could not restore persisted command {command}: {error}");
                    m_commands.RemoveAt(index--);
                    continue;
                }
                m_commands[index] = command;
            }
        }

        private void RegisterUserActivity(int userId)
        {
            ServerPlayerState player = m_gameState.GetPlayerByUserId(userId);
            if (player == null)
                return;
            player.RegisterActivity(m_gameState.CurrentTurn);
            PublishPlayer(player);
        }

        private void ApplyCaptain(CaptainAssignment? captain)
        {
            if (!captain.HasValue)
                return;
            m_activity.ApplyCaptain(captain, PublishPlayer);
            m_serverGameRoomNB.UpdateTeamInfo(captain.Value.TeamIndex);
            m_foobbuteoServer?.NotifyAboutGameChange();
        }

        private void PublishPlayer(ServerPlayerState player) =>
            m_serverGameRoomNB.UpdatePlayerControlState(player.ToContract());

        private void OnUserConnectionChanged(int userId)
        {
            ServerPlayerState player = m_gameState.GetPlayerByUserId(userId);
            if (player == null)
                return;
            ulong? clientId = m_userRegistry.TryGetUser(userId, out var user) ? user.ClientId : null;
            if (player.SetConnection(clientId))
                PublishPlayer(player);
        }

        internal void StopConnectionTracking() => m_userRegistry.ConnectionChanged -= OnUserConnectionChanged;

        private void RefreshConnections()
        {
            RefreshTeam(m_gameState.HomeTeam);
            RefreshTeam(m_gameState.AwayTeam);

            void RefreshTeam(ServerTeamState team)
            {
                foreach (ServerPlayerState player in team.Players)
                {
                    ulong? clientId =
                        player.IsUser && m_userRegistry.TryGetUser(player.UserId, out var user) ? user.ClientId : null;
                    player.SetConnection(clientId);
                }
            }
        }

        internal async Task<TurnResult> ProcessAndPersistTurnAsync()
        {
            await m_persistenceGate.WaitAsync();
            try
            {
                RefreshConnections();
                var changedPlayers = new HashSet<ServerPlayerState>();
                m_activity.CloseInput(player => changedPlayers.Add(player));
                var captainChanges = new List<CaptainAssignment>(2);
                TryReplaceCaptain(m_gameState.HomeTeam);
                TryReplaceCaptain(m_gameState.AwayTeam);
                TurnResult result = m_turnProcessor.ProcessTurn();
                TurnPersistenceRequest request = TurnPersistenceRequest.Create(m_gameState, m_turnProcessor);
                await PersistTurnAndCaptainsAsync(request, captainChanges);
                foreach (ServerPlayerState player in changedPlayers)
                    PublishPlayer(player);
                foreach (CaptainAssignment captain in captainChanges)
                    m_serverGameRoomNB.UpdateTeamInfo(captain.TeamIndex);
                if (captainChanges.Count > 0)
                    m_foobbuteoServer?.NotifyAboutGameChange();
                m_commands.Clear();
                return result;

                void TryReplaceCaptain(ServerTeamState team)
                {
                    CaptainAssignment? captain = m_activity.FindCaptainReplacement(team);
                    if (!captain.HasValue)
                        return;
                    captainChanges.Add(captain.Value);
                    m_activity.ApplyCaptain(captain, player => changedPlayers.Add(player));
                }
            }
            finally
            {
                m_persistenceGate.Release();
            }
        }

        private async Task PersistTurnAndCaptainsAsync(
            TurnPersistenceRequest request,
            IReadOnlyList<CaptainAssignment> captainChanges
        )
        {
            await PersistWithRetryAsync(() => m_persistenceRepository.SaveTurnAsync(request), "completed turn");
            foreach (CaptainAssignment captain in captainChanges)
                await PersistCaptainAsync(captain);
        }

        private Task PersistCaptainAsync(CaptainAssignment captain) =>
            PersistWithRetryAsync(
                () =>
                    m_persistenceRepository.SaveTeamInfoAsync(
                        GameId,
                        captain.TeamIndex,
                        captain.UserId,
                        captain.PlayerId,
                        captain.Name
                    ),
                $"captain for {captain.TeamIndex}"
            );

        private async Task PersistWithRetryAsync(Func<Task> save, string operation)
        {
            while (true)
            {
                try
                {
                    await save();
                    return;
                }
                catch (RepositoryUnavailableException error)
                {
                    this.LError(
                        $"{InfoTag} Failed to persist {operation}. Retrying in {m_persistenceRetryDelay.TotalSeconds}s."
                    );
                    this.LException(error);
                    await Task.Delay(m_persistenceRetryDelay);
                }
            }
        }

        private static async Task PersistOrThrow(Task persistenceTask)
        {
            try
            {
                await persistenceTask;
            }
            catch (RepositoryUnavailableException)
            {
                throw new AppException("Games database is unavailable.");
            }
        }

        private async Task<T> PersistOrThrow<T>(Task<T> persistenceTask)
        {
            try
            {
                return await persistenceTask;
            }
            catch (RepositoryUnavailableException ex)
            {
                this.LExceptionTree(ex, "PersistOrThrow");
                throw new AppException("Games database is unavailable.");
            }
        }

        private string InfoTag => $"[G:{m_gameState.GameId} T:{m_gameState.CurrentTurn}]";

        public bool RemoveClientFromGame(ulong senderClientId)
        {
            if (m_foobbuteoServer.RemoveClientFromGame(senderClientId, m_serverGameRoomNB))
            {
                m_serverGameRoomNB.CloseGameRoom(senderClientId);
                return true;
            }
            return false;
        }

        public NetFoobbuteoTeam ToNetHomeTeam => m_gameState.HomeTeam.ToNetFoobbuteoTeam();
        public NetFoobbuteoTeam ToNetAwayTeam => m_gameState.AwayTeam.ToNetFoobbuteoTeam();

        public TurnStageNetState GetTurnStage()
        {
            return m_gameState.GetTurnStageNet();
        }

        public FixedGameInfoNetState ToFixedGameInfoNetState(long baseServerTime)
        {
            return m_gameState.ToFixedGameInfoNetState(baseServerTime);
        }

        public GameDetailsContract ToGameDetailsContract()
        {
            return m_gameState.ToGameDetailsContract();
        }

        public void CloseGame()
        {
            m_foobbuteoServer.DeleteGame(GameId);
        }
    }
}
