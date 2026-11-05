// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using Imma.Common;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.Lobby;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Netcode;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;

namespace Imma.Foobbuteo.Client.Game
{
    [RequireComponent(typeof(TeamSpawner))]
    [RequireComponent(typeof(PlayerSpriteDatabase))]
    public class GameManager : BaseMonoBehaviour
    {
        private static GameManager m_instance;
        public static GameManager Instance
        {
            get
            {
                if (m_instance == null)
                    throw new AppException("GameManager Instance not set!!");
                return m_instance;
            }
        }

        private PlayerSpriteDatabase m_SpriteDB;

        private PlayerMB m_selectedPlayer;
        internal PlayerMB SelectedPlayer => m_selectedPlayer;

        // private PlayerMB m_currentUserPlayer;

        [SerializeField]
        private TargetMarkerMB m_targetMarker;
        public TargetMarkerMB TargetMarker => m_targetMarker;

        [SerializeField]
        private GoalZoneMB m_homeTeamGoalZone;
        public GoalZoneMB HomeTeamGoalZone => m_homeTeamGoalZone;

        [SerializeField]
        private GoalZoneMB m_awayTeamGoalZone;
        public GoalZoneMB AwayTeamGoalZone => m_awayTeamGoalZone;

        [SerializeField]
        private PlaybackManager m_playbackManager;

        [SerializeField]
        private MatchAudioPlayer m_audioPlayer;
        private MatchSoundService m_matchSounds;

        [SerializeField]
        private BallMB m_ball;
        public BallMB Ball => m_ball;

        [SerializeField]
        private GameHudPresenter m_hud;

        internal ClientTurnManager TurnManager { get; private set; }

        private ClientGameState m_gameState;
        private int GameId => m_gameState.GameId;

        internal ClientCmdManager CommandManager { get; private set; }
        internal ClientTeamsViewRegistry TeamsRegistry { get; private set; }
        private TeamSpawner m_teamSpawner;

        [SerializeField]
        private PitchViewport m_pitchViewport;
        private Camera m_pitchCamera;
        private float m_viewportPixelsPerUnit;
        private float m_viewportDensity;

        public IClientGameRoomNB ApiClient { get; private set; }
        private bool m_hasFreshDetails;
        public bool CanMove => m_hasFreshDetails && AppCtx.Client.IsClientConnected && m_gameState.CanMove;

        internal bool CanMovePlayer(PlayerMB player) =>
            m_hasFreshDetails
            && AppCtx.Client.IsClientConnected
            && m_gameState.GetPlayerForCommand(player.PlayerState.Slot.PlayerKey, AppCtx.Client.User, out _) != null;

        // private GameInfoContract m_gameInfo;
        // private GameDetailsContract m_gameDetails;

        private IDisposable m_gameRoomLifecycleToken;
        private bool m_isLeavingRoom;

        new void Awake()
        {
            this.LogInfo("++");
            base.Awake();

            if (m_instance != null && m_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            m_instance = this;
            InitializeOnce();
        }

        private void InitializeOnce()
        {
            this.LogTrace("++");

            m_hud.EnsureInitialized();
            m_matchSounds = new MatchSoundService(m_audioPlayer.Play, () => m_audioPlayer.Preferences.Volume);
            m_SpriteDB = GetComponent<PlayerSpriteDatabase>();
            var images = ClientAppCtrl.Instance.Images;
            m_SpriteDB.Init(images);
            m_hud.InitializeImages(images);
            m_teamSpawner = GetComponent<TeamSpawner>();

            var registry = ServiceLocator.Current.Get<NetworkObjectRegistry>();
            m_gameRoomLifecycleToken = registry.BindLifecycle<IClientGameRoomNB>(
                onSpawn: nb => InitFromNetwork(nb),
                onDespawn: OnDespawn
            );

            TeamsRegistry = new();
            AppCtx.Client.ClientConnectionStateChanged += OnConnectionChanged;
            m_pitchCamera = m_pitchViewport.PitchCamera;
            if (m_pitchViewport != null)
                m_pitchViewport.ViewportChanged += UpdateViewportScale;
            UpdateViewportScale();
        }

        private void RegisterEvents()
        {
            ApiClient.StartTimeChanged += OnStartTimeChanged;
            ApiClient.GameDetailsReady += OnGameDetailsReady;
            ApiClient.TurnStarted += OnTurnStarted;
            ApiClient.ProcessingStarted += OnProcessingStarted;
            ApiClient.TurnResultReady += OnTurnResultReady;
            ApiClient.CommandResponseReceived += OnCommandResponseReceived;
            ApiClient.PlayerControlClaimed += OnPlayerControlClaimed;
            ApiClient.PlayerControlStateChanged += OnPlayerControlClaimed;
            ApiClient.TeamInfoChanged += OnTeamInfoChanged;
            ApiClient.GameEnded += OnGameEnded;

            AppCtx.Client.UserInfoUpdated += UpdateUserInfo;
        }

        private void UnregisterEvents()
        {
            if (ApiClient != null)
            {
                ApiClient.StartTimeChanged -= OnStartTimeChanged;
                ApiClient.GameDetailsReady -= OnGameDetailsReady;
                ApiClient.TurnStarted -= OnTurnStarted;
                ApiClient.ProcessingStarted -= OnProcessingStarted;
                ApiClient.TurnResultReady -= OnTurnResultReady;
                ApiClient.CommandResponseReceived -= OnCommandResponseReceived;
                ApiClient.PlayerControlClaimed -= OnPlayerControlClaimed;
                ApiClient.PlayerControlStateChanged -= OnPlayerControlClaimed;
                ApiClient.TeamInfoChanged -= OnTeamInfoChanged;
                ApiClient.GameEnded -= OnGameEnded;

                if (AppCtx.Client != null)
                    AppCtx.Client.UserInfoUpdated -= UpdateUserInfo;
            }
        }

        private void OnDespawn()
        {
            m_isLeavingRoom =
                AppCtx.Client.IsClientConnected
                && NetworkManager.Singleton != null
                && !NetworkManager.Singleton.ShutdownInProgress;
            m_hasFreshDetails = false;
            m_hud.SetSessionReady(false);
            StopAllCoroutines();
            if (m_audioPlayer != null)
                m_audioPlayer.gameObject.SetActive(false);
            m_hud.StopUserLists();
            this.LDebug("onDeSpawn --");
            UnregisterEvents();
            ApiClient = null;
        }

        private void InitFromNetwork(IClientGameRoomNB clientGameRoomNB)
        {
            // Room replacement can register before this scene finishes unloading.
            if (m_isLeavingRoom)
                return;
            m_hasFreshDetails = false;
            m_hud.EnsureInitialized();
            m_matchSounds = new MatchSoundService(m_audioPlayer.Play, () => m_audioPlayer.Preferences.Volume);
            if (m_audioPlayer != null)
                m_audioPlayer.gameObject.SetActive(true);
            this.LDebug("onSpawn ++");

            if (clientGameRoomNB.IsHost)
            {
                clientGameRoomNB.InitClient();
            }

            ApiClient = clientGameRoomNB;
            RegisterEvents();

            var baseGameInfo = ApiClient.GetBaseGameInfo();

            m_gameState = ClientGameState.FromContract(baseGameInfo);
            AppCtx.Client.User.EnterRoom(m_gameState.GameId);

            m_hud.InitializeMatch(baseGameInfo, AppCtx.Client.User, m_gameState);
            m_hud.InitializeManagerJoin(ApiClient, m_gameState, () => AppCtx.Client.User);
            if (ServiceLocator.Current.GetOrDefault<ILobbyApiClient>() is IUserPresenceApi presenceApi)
                m_hud.InitializeUsers(presenceApi, m_gameState.GameId, () => AppCtx.Client.User?.IsAdmin ?? false);

            CommandManager = new ClientCmdManager(
                ApiClient,
                () => m_hasFreshDetails && AppCtx.Client.IsClientConnected
            );
            TurnManager = new ClientTurnManager(
                m_hud,
                m_gameState,
                baseGameInfo.FixedGameInfo.BaseServerTimeUnixSeconds
            );

            TurnManager.Init(ServerTime());

            // UpdateGameStatus(baseGameInfo);

            ApiClient.UserJoinedRoom();
        }

        internal void LeaveGame()
        {
            if (
                !m_hasFreshDetails
                || !AppCtx.Client.IsClientConnected
                || ApiClient == null
                || m_gameState == null
                || !GameRoomLeavePolicy.CanLeave(AppCtx.Client.User, m_gameState.GameId, m_gameState.Status)
            )
                return;
            ApiClient.RequestLeaveGame();
        }

        new void Start()
        {
            this.LogTrace("++");
            UpdateViewportScale();
        }

        private void SpawnTeams()
        {
            m_teamSpawner.Spawn(m_hud.HomeTeam, m_hud.AwayTeam, m_gameState);
        }

        private float m_updateLastServerTime = 0;
        private const float k_updateInterval = 0.2f;

        private void Update()
        {
            if (NetworkManager.Singleton == null || !m_hasFreshDetails || !AppCtx.Client.IsClientConnected)
                return;
            float serverTime = ServerTime();
            if (m_updateLastServerTime + k_updateInterval > serverTime)
            {
                return;
            }
            m_updateLastServerTime = serverTime;
            TurnManager.Update(serverTime);
        }

        // private void UpdateGameStatus(BaseGameInfo info)
        // {
        //     if (NetworkManager.Singleton == null)
        //         return;
        //     TurnManager.UpdateOnce(ServerTime());
        // }

        new void OnDestroy()
        {
            m_isLeavingRoom = true;
            if (AppCtx.Client != null)
                AppCtx.Client.ClientConnectionStateChanged -= OnConnectionChanged;
            this.LogTrace("++");
            UnregisterEvents();
            if (m_pitchViewport != null)
                m_pitchViewport.ViewportChanged -= UpdateViewportScale;
            this.m_gameRoomLifecycleToken?.Dispose();

            if (m_instance == this)
                m_instance = null;
            //  ApiClient.Dispose();
        }

        // public void ChangeState(ClientTurnStateEnum newState)
        // {
        //     State = newState;
        // }

        internal void SelectPlayer(PlayerKey playerKey)
        {
            SelectPlayer(TeamsRegistry.GetPlayer(playerKey));
        }

        internal void SelectPlayer(PlayerMB player)
        {
            if (m_selectedPlayer == player)
                return;

            var oldSelectedPlayer = m_selectedPlayer;
            m_selectedPlayer = player;

            if (oldSelectedPlayer != null)
                oldSelectedPlayer.SetSelected(false);

            if (m_selectedPlayer != null)
            {
                m_selectedPlayer.SetSelected(true);
                m_hud.SwitchPlayer(player.PlayerVM);
            }
            else
            {
                m_hud.SwitchPlayer(null);
            }
        }

        // ++ Events ++
        private void OnStartTimeChanged(System.DateTimeOffset start)
        {
            m_gameState.RescheduleStart(start);
            TurnManager.UpdateOnce(ServerTime());
        }

        private void OnGameDetailsReady(GameDetailsContract contract)
        {
            this.LAssert(GameId == contract.GameId);
            Ball.MoveToLogicPosition(contract.BallPosition);
            m_gameState.LoadDetails(contract);
            m_hud.RefreshPlayerList();
            m_hud.UpdateScore(contract.HomeTeam.Score, contract.AwayTeam.Score);
            SelectPlayer((PlayerMB)null);
            SpawnTeams();
            m_hud.RefreshPlayerWithBall();
            TurnManager.UpdateOnce(ServerTime());
            this.LogAssert(AppCtx.Client.User != null);

            SetCaptainOfTeam(contract);

            var userSlot = AppCtx.Client.User.GameSlot;
            if (
                userSlot.HasValue
                && userSlot.Value.GameId == GameId
                && userSlot.Value.TryGetPlayerKey(out var userPlayerKey)
            )
                SelectPlayer(userPlayerKey);
            m_hud.RefreshManagerJoin();
            m_hasFreshDetails = true;
            m_hud.SetSessionReady(true);
        }

        private void OnConnectionChanged()
        {
            if (!AppCtx.Client.IsClientConnected)
            {
                m_isLeavingRoom = false;
                m_hasFreshDetails = false;
                m_hud.SetSessionReady(false);
            }
        }

        private static float ServerTime() => NetworkManager.Singleton.ServerTime.TimeAsFloat;

        private void SetCaptainOfTeam(GameDetailsContract contract)
        {
            if (m_gameState.MatchType == FoobbuteoGameType.MvM)
            {
                m_hud.SetCaptainOfTeam(null);
                return;
            }
            if (AppCtx.Client.User.UserId == contract.HomeTeam.Id)
            {
                m_hud.SetCaptainOfTeam(TeamIndex.Home);
            }
            else if (AppCtx.Client.User.UserId == contract.HomeTeam.Id)
            {
                m_hud.SetCaptainOfTeam(TeamIndex.Away);
            }
            else
            {
                m_hud.SetCaptainOfTeam(null);
            }
        }

        #region ++ Events ++

        private void OnTurnStarted(TurnStateChangedMessage msg)
        {
            TargetMarker.HideTargetMarker();
            // this.LogTrace(msg);
            TeamsRegistry.TurnStarted();
            m_matchSounds.TurnStarted(m_gameState.Status, msg.GameStatus);
            TurnManager.TurnStarted(msg);
            m_hud.RefreshManagerJoin();
            if (SelectedPlayer != null)
            {
                SelectedPlayer.ActionCtx.ShowZone();
            }

            AppCtx.Client.ShowBanner($"Start Turn {msg.TurnNumber}", 2);
        }

        private void OnProcessingStarted(TurnStateChangedMessage msg)
        {
            // this.LogTrace("++");
            TurnManager.ProcessingStarted(msg);
            if (SelectedPlayer != null)
                SelectedPlayer.EndPlayerTurn();
        }

        private void OnTurnResultReady(TurnResultReadyMessage msg)
        {
            // this.LogInfo("++");
            // TurnManager.SetState(ClientTurnStateEnum.Playback, msg.PlaybackEndsAtUtc);
            if (!m_matchSounds.BeginPlayback(msg.TurnNumber))
                return;
            TurnManager.TurnResultReady(msg);
            StartCoroutine(PlayTurnResults(msg));
        }

        private IEnumerator PlayTurnResults(TurnResultReadyMessage msg)
        {
            var result = msg.Result;
            yield return m_playbackManager.Play(msg, m_hud.AddCommentary, m_matchSounds);
            m_matchSounds.EndPlayback();
            SetPlayerWithBall(result.PlayerWithBall);
            m_hud.RefreshPlayerList();
            if (result.TurnResultType == TurnResultTypeEnum.Goal)
            {
                this.m_hud.UpdateScore(result.HomeScore, result.AwayScore);
            }
            else if (result.TurnResultType == TurnResultTypeEnum.Offside)
            {
                var offsidePlayer = m_gameState.GetPlayer(result.OffsidePlayer);
                var freeKickTeam = m_gameState.Team(result.PlayerWithBall.TeamIdx);
                AppCtx.Client.ShowToast(
                    $"Player {offsidePlayer.Name} was offside. Free kick for Team {freeKickTeam.Name}."
                );
            }
        }

        private void OnCommandResponseReceived(PlayerCommandResponse resp)
        {
            if (!resp.IsAccepted)
            {
                AppCtx.Client.ShowToast($"{resp.Reason} [{resp.Command.CommandId}]", 5f);
                return;
            }

            var playerState = m_gameState.GetPlayer(resp.Command.TeamIdx, resp.Command.PlayerIdx);
            playerState.Client_UpdateActionPoints(resp.RemaingActionPoints);

            var playerMB = TeamsRegistry.GetPlayer(resp.Command.TeamIdx, resp.Command.PlayerIdx);

            var position = resp.Command.TargetPosition.LogicToWorld();

            playerMB.ActionCtx.SetTargetLocalPosition(position, resp.Command.Action);
            if (playerMB.IsSelected)
            {
                playerMB.ActionCtx.ShowZone();
            }
        }

        private void OnPlayerControlClaimed(GameDetails_PlayerContract contract)
        {
            var playerMB = TeamsRegistry.GetPlayer(contract.TeamIdx, contract.PlayerIdx);
            if (playerMB != null)
            {
                playerMB.OnPlayerControlGranted(contract);
                m_hud.OnPlayerControlGranted(playerMB);
                m_hud.RefreshSelectedPlayerPermissions();
                SelectedPlayer?.ActionCtx.SetActive(true);
            }
        }

        private void OnTeamInfoChanged(TeamIndex teamIdx, NetFoobbuteoTeam teamInfo)
        {
            // this.LDebug($"{teamIdx}, {teamInfo}");
            m_gameState.UpdateTeamInfo(teamIdx, teamInfo);
            m_hud.UpdateTeamInfo(teamIdx, teamInfo);
            m_hud.RefreshManagerJoin();
        }

        private void UpdateUserInfo(GameUser user)
        {
            // throw new NotImplementedException();
            m_hud.UpdateUser(user);
            m_hud.RefreshManagerJoin();
        }

        private void OnGameEnded(TurnStateChangedMessage message)
        {
            m_matchSounds.MatchEnded();
            m_gameState.UpdateTurn(message);
            m_hud.GameEnded();
            m_hud.RefreshManagerJoin();
        }

        private void UpdateViewportScale()
        {
            if (m_pitchCamera == null)
                m_pitchCamera = Camera.main;
            m_viewportPixelsPerUnit = 0f;
            if (
                m_pitchCamera == null
                || !m_pitchCamera.orthographic
                || m_pitchCamera.pixelHeight <= 0
                || m_pitchCamera.orthographicSize <= 0f
            )
                return;

            m_viewportPixelsPerUnit = m_pitchCamera.pixelHeight / (2f * m_pitchCamera.orthographicSize);
            bool touchSupported = Input.touchSupported;
#if UNITY_EDITOR
            touchSupported = DeviceSimulatorUtility.IsRunningInDeviceSimulator();
#endif
            m_viewportDensity = touchSupported && Screen.dpi > 0f ? Mathf.Clamp(Screen.dpi / 160f, 1f, 3f) : 1f;
            // this.LDebug(new { touchSupported, Screen.dpi, m_viewportDensity, m_viewportPixelsPerUnit });
            TeamsRegistry.ApplyViewportScale(m_viewportPixelsPerUnit, m_viewportDensity);
        }

        #endregion -- events --

        internal void ApplyViewportScale(PlayerMB player)
        {
            if (m_viewportPixelsPerUnit > 0f)
                player.ApplyViewportScale(m_viewportPixelsPerUnit, m_viewportDensity);
        }

        private void SetPlayerWithBall(PlayerKey playerIdentify)
        {
            TeamsRegistry.CleanPlayerWithBall();
            if (!playerIdentify.IsEmpty)
            {
                var playerMB = TeamsRegistry.GetPlayer(playerIdentify.TeamIdx, playerIdentify.PlayerIdx);
                playerMB.SetHasBall(true);
            }
            m_hud.RefreshPlayerWithBall();
        }

        internal void RequestPlayerControl(PlayerVM selectedPlayer)
        {
            ApiClient.RequestPlayerControl(selectedPlayer.PlayerKey);
        }
    }
}
