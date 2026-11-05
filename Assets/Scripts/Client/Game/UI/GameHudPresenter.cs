// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Client.UI.Help;
using Imma.Foobbuteo.Client.UI.Modals;
using Imma.Foobbuteo.Client.UI.Tooltip;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    // Server builds strip PanelRenderer while processing the scene.
#if !UNITY_SERVER
    [RequireComponent(typeof(PanelRenderer))]
#endif
    internal class GameHudPresenter : BaseMonoBehaviour
    {
        [SerializeField]
        private PlayerSpriteDatabase m_spriteDB;

        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("m_vm")]
        private GameHudVM m_vmTemplate;
        private GameHudVM m_vm;
        private GameSettingsVM m_settings;

        private VisualElement m_rootElement;

        [SerializeField]
        private Camera m_pitchCamera;

        [SerializeField]
        private Physics2DRaycaster m_pitchRaycaster;

        [SerializeField]
        private TooltipService m_tooltipService;
        private ModalHostController m_modals;
        private HelpUiController m_helpUi;
        private bool m_pitchBlocked;
        private bool m_pitchWasEnabled;
        private readonly List<Action> m_unbindClicks = new();
        private readonly MatchReportSession m_reportSession = new();
        private readonly MatchReportView m_reportView = new();
        private UserListsUiController m_usersUi;
        private MvMJoinUiController m_managerJoinUi;
        private MvMLineupUiController m_lineupUi;
        private ClientImageCatalog m_images;

        internal void InitializeImages(ClientImageCatalog images)
        {
            m_images = images;
            InitializeHelpImages();
        }

        private void InitializeHelpImages()
        {
            if (m_images != null)
                m_rootElement?.Query<HelpScreen>().ForEach(screen => screen.SetImages(m_images));
        }

        internal void InitializeManagerJoin(IClientGameRoomNB api, ClientGameState game, Func<GameUser> getUser)
        {
            m_managerJoinUi?.Dispose();
            m_managerJoinUi = new MvMJoinUiController(api, game, getUser);
            m_lineupUi?.Dispose();
            m_lineupUi = new MvMLineupUiController(
                api,
                game,
                getUser,
                team => team == TeamIndex.Home ? HomeTeam : AwayTeam,
                m_images,
                m_modals
            );
            m_lineupUi.Bind(m_rootElement);
            m_managerJoinUi.Bind(m_rootElement);
        }

        internal void RefreshManagerJoin()
        {
            m_managerJoinUi?.Refresh();
            m_lineupUi?.Refresh();
        }

        internal void InitializeUsers(IUserPresenceApi api, int roomId, Func<bool> isAdmin)
        {
            m_usersUi?.Dispose();
            m_usersUi = new UserListsUiController(
                api,
                isAdmin,
                roomId,
                team => team == TeamIndex.Home ? HomeTeam : AwayTeam
            );
            m_usersUi.Bind(m_rootElement);
        }

        internal void StopUserLists()
        {
            m_modals?.ForceCloseActive(ModalCloseReason.SessionEnded);
            m_usersUi?.Unbind();
            m_managerJoinUi?.Dispose();
            m_managerJoinUi = null;
            m_lineupUi?.Dispose();
            m_lineupUi = null;
        }

        private readonly GameTextFormatter m_textFormatter = new();

        [SerializeField]
        private MatchAudioPlayer m_audioPlayer;
        private GameSettingsUiController m_soundSettingsUi;
        private PlayerListUiController m_playerListUi;

        internal void RefreshPlayerList() => m_playerListUi?.Refresh();

        internal void SetSessionReady(bool ready) => m_vm.IsSessionReady = ready;

        internal TeamVM HomeTeam => m_vm.TeamA;
        internal TeamVM AwayTeam => m_vm.TeamB;

        private void OnHudPropertyChanged(object sender, BindablePropertyChangedEventArgs evt)
        {
            if (evt.propertyName == nameof(GameHudVM.IsTimerUrgent))
                RefreshTimerStyle();
        }

        private void RefreshTimerStyle()
        {
            m_rootElement?.Q("StatusPanel")?.EnableInClassList("timer-urgent", m_vm.IsTimerUrgent);
        }

        private VisualElement m_actionsContainer;

        private const string k_claimPlayerBtnName = "ClaimPlayerBtn";
        private Button m_claimPlayerBtn;
        private const string k_selectedPlayerPopupName = "SelectedPlayerPopup";
        private VisualElement m_selectedPlayerPopup;
        private const string k_selectedPlayerClaimBtnName = "SelectedPlayerClaimBtn";
        private Button m_selectedPlayerClaimBtn;
        private const string k_gameCloseBtnName = "GameCloseButton";
        private Button m_gameCloseBtn;
        private const string k_mobileGameCloseBtnName = "pitch-close-button";
        private Button m_mobileGameCloseBtn;
        private const string k_currentUserPlayerBtnName = "CurrentUserPlayerBtn";
        private Button m_currentUserPlayerBtn;
        private const string k_playerWithBallBtnName = "PlayerWithBallBtn";
        private Button m_playerWithBallBtn;

        internal void EnsureInitialized()
        {
            if (m_vm != null)
                return;
            m_vm = GameHudVM.CreateRuntime(m_vmTemplate);
            var preferences = ClientAppCtrl.Instance.Preferences;
            m_audioPlayer.Initialize(preferences);
            m_settings = new GameSettingsVM(preferences);
            m_modals = new ModalHostController(new ModalHostState());
            m_modals.State.propertyChanged += OnModalChanged;
            m_helpUi = new HelpUiController(m_modals);
            m_soundSettingsUi = new GameSettingsUiController(m_settings, m_modals);
        }

        new void Awake()
        {
            this.LogDebug("+++");
            EnsureInitialized();
            m_vm.propertyChanged += OnHudPropertyChanged;
            m_vm.SwitchPlayer(null);
            m_vm.IsSessionReady = false;
            // m_vm.UserPlayerKey = null;
            m_vm.OnePlayerHasBall = false;
        }

        new void OnEnable()
        {
            EnsureInitialized();
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        new void OnDisable()
        {
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
            UnbindHud();
            m_helpUi?.Unbind();
            m_soundSettingsUi?.Unbind();
            m_playerListUi?.Unbind();
            m_lineupUi?.Unbind();
            m_usersUi?.Unbind();
            m_managerJoinUi?.Unbind();
            m_reportView.Dispose();
            if (m_pitchBlocked && m_pitchRaycaster != null)
                m_pitchRaycaster.enabled = m_pitchWasEnabled;
            m_pitchBlocked = false;
            if (m_tooltipService != null)
                m_tooltipService.SetModalScope(null);
        }

        new void OnDestroy()
        {
            UnbindHud();
            m_helpUi?.Dispose();
            m_modals.ForceCloseActive(ModalCloseReason.SessionEnded);
            m_modals.State.propertyChanged -= OnModalChanged;
            m_modals.Dispose();
            m_vm.propertyChanged -= OnHudPropertyChanged;
            m_settings?.Dispose();
            m_reportView.Dispose();
            m_usersUi?.Dispose();
            m_managerJoinUi?.Dispose();
            m_soundSettingsUi?.Dispose();
            m_lineupUi?.Dispose();
            m_playerListUi?.Dispose();
            m_reportSession.Clear();
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
            if (m_vm != null)
                Destroy(m_vm);
        }

        void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            if (rootElement.Q("game-room-root") == null)
                return;
            UnbindHud();
            m_rootElement = rootElement;
            m_modals.Bind(rootElement);
            m_helpUi.Bind(rootElement);
            InitializeHelpImages();
            rootElement.Q("MainLayout").dataSource = m_vm;
            m_vm.RefreshRoomPermissions();
            m_usersUi?.Bind(rootElement);
            m_managerJoinUi?.Bind(rootElement);
            m_lineupUi?.Bind(rootElement);
            m_soundSettingsUi.Bind(rootElement);
            m_playerListUi?.Bind(rootElement);
            RefreshTimerStyle();
            RefreshModalScope();
            AppCtx.Client.SetRootElement(rootElement);
            m_reportView.Bind(Q<ScrollView>("MatchReportScroll"), m_reportSession);

            m_actionsContainer = Q<VisualElement>("ActionsContainer");
            m_actionsContainer.dataSource = m_vm;

            m_claimPlayerBtn = Q<Button>(k_claimPlayerBtnName);
            BindClick(m_claimPlayerBtn, OnClaimPlayerBtnClick);

            m_selectedPlayerPopup = Q<VisualElement>(k_selectedPlayerPopupName);
            m_selectedPlayerPopup.dataSource = m_vm;

            m_selectedPlayerClaimBtn = Q<Button>(k_selectedPlayerClaimBtnName);
            BindClick(m_selectedPlayerClaimBtn, OnClaimPlayerBtnClick);

            m_currentUserPlayerBtn = Q<Button>(k_currentUserPlayerBtnName);
            BindClick(m_currentUserPlayerBtn, OnCurrentUserPlayerBtnClick);

            m_playerWithBallBtn = Q<Button>(k_playerWithBallBtnName);
            BindClick(m_playerWithBallBtn, OnPlayerWithBallBtnClick);

            m_gameCloseBtn = Q<Button>(k_gameCloseBtnName);
            BindClick(m_gameCloseBtn, OnGameCloseBtnClick);

            m_mobileGameCloseBtn = Q<Button>(k_mobileGameCloseBtnName);
            BindClick(m_mobileGameCloseBtn, OnGameCloseBtnClick);

            InitTooltip();

#if UNITY_EDITOR // ++ DEV: TEST BUTTON ++
            const string testButtonName = "TestButton";
            var testButton = Q<Button>(testButtonName);
            testButton?.RegisterCallback<ClickEvent>(evt =>
            {
                // m_vm.SelectedPlayer.HasBall = !m_vm.SelectedPlayer.HasBall;
                return;
            });
#endif      // -- DEV: TEST BUTTON --
        }

        private void BindClick(Button button, EventCallback<ClickEvent> handler)
        {
            if (button == null)
                return;
            button.RegisterCallback(handler);
            m_unbindClicks.Add(() => button.UnregisterCallback(handler));
        }

        private void UnbindHud()
        {
            foreach (var release in m_unbindClicks)
                release();
            m_unbindClicks.Clear();
            m_rootElement?.UnregisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            m_rootElement?.UnregisterCallback<PointerOutEvent>(OnPointerOut, TrickleDown.TrickleDown);
            StopTooltipTimer();
            m_modals?.Unbind();
        }

        private void OnModalChanged(object sender, BindablePropertyChangedEventArgs change)
        {
            if (change.propertyName == nameof(ModalHostState.HasActivePanel))
                RefreshModalScope();
        }

        private void RefreshModalScope()
        {
            bool blocked = isActiveAndEnabled && m_modals.State.HasActivePanel;
            if (m_pitchRaycaster != null && blocked != m_pitchBlocked)
            {
                if (blocked)
                {
                    m_pitchWasEnabled = m_pitchRaycaster.enabled;
                    m_pitchRaycaster.enabled = false;
                }
                else
                    m_pitchRaycaster.enabled = m_pitchWasEnabled;
            }
            m_pitchBlocked = blocked;
            StopTooltipTimer();
            if (m_tooltipService != null)
                m_tooltipService.SetModalScope(m_modals.Active?.Root);
        }

        private void LateUpdate()
        {
            if (
                m_selectedPlayerPopup?.panel == null
                || m_modals.State.HasActivePanel
                || m_selectedPlayerPopup.resolvedStyle.display == DisplayStyle.None
            )
                return;

            PlayerMB selectedPlayer = GameManager.Instance.SelectedPlayer;
            if (selectedPlayer == null || m_pitchCamera == null)
                return;

            Vector3 screenPosition = m_pitchCamera.WorldToScreenPoint(selectedPlayer.PopupAnchorPosition);
            if (screenPosition.z <= 0 || !UiViewportMetrics.TryRead(m_rootElement, out var metrics))
                return;
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(
                m_selectedPlayerPopup.panel,
                new Vector2(screenPosition.x, UnityEngine.Device.Screen.height - screenPosition.y)
            );
            Rect safeArea = m_rootElement.LocalToWorld(metrics.SafeAreaPanelUnits);
            ContextualPopupPosition.Place(m_selectedPlayerPopup, panelPosition, safeArea);
        }

        private T Q<T>(string query)
            where T : VisualElement
        {
            var element = m_rootElement.Q<T>(query);
            if (element == null)
                ImmLogger.LError<Validation>($"Missing element '{query}'");
            return element;
        }

        // private T Q<T>(string query, ref T elem)
        //     where T : VisualElement
        // {
        //     elem = m_RootElement.Q<T>(query);
        //     if (elem == null)
        //         ImmLogger.LError<Validation>($"Missing element '{query}'");
        //     return elem;
        // }

        private void OnClaimPlayerBtnClick(ClickEvent evt)
        {
            GameManager.Instance.RequestPlayerControl(m_vm.SelectedPlayer);
        }

        private void OnCurrentUserPlayerBtnClick(ClickEvent evt)
        {
            if (m_vm.User.GameSlot.HasValue && m_vm.User.GameSlot.Value.TryGetPlayerKey(out var key))
                GameManager.Instance.SelectPlayer(key);
        }

        private void OnPlayerWithBallBtnClick(ClickEvent evt)
        {
            var player = m_vm.PlayerWithBall;
            if (player != null)
                GameManager.Instance.SelectPlayer(player.Slot.PlayerKey);
        }

        internal void RefreshPlayerWithBall()
        {
            m_vm.OnePlayerHasBall = m_vm.PlayerWithBall != null;
        }

        private void OnGameCloseBtnClick(ClickEvent evt)
        {
            GameManager.Instance.LeaveGame();
        }

        internal void InitializeMatch(BaseGameInfo baseGameInfo, GameUser user, ClientGameState gameState)
        {
            m_vm.ResetRuntimeState();
            m_playerListUi?.Dispose();
            m_playerListUi = new PlayerListUiController(
                team => m_vm.GameState?.Team(team),
                team => (team == TeamIndex.Home ? HomeTeam : AwayTeam)?.Name ?? "Team",
                m_images,
                m_modals
            );
            if (m_rootElement != null)
                m_playerListUi.Bind(m_rootElement);
            m_reportSession.Clear();
            if (m_rootElement != null)
                m_reportView.Bind(Q<ScrollView>("MatchReportScroll"), m_reportSession);
            m_vm.User = user;
            m_settings.SetAdmin(user?.IsAdmin == true);
            m_vm.GameState = gameState;
            m_vm.TeamA = CreateTeamVm(TeamIndex.Home, baseGameInfo.HomeTeam);
            m_vm.TeamB = CreateTeamVm(TeamIndex.Away, baseGameInfo.AwayTeam);
            // TODO Score
            m_vm.ScoreA = 0;
            m_vm.ScoreB = 0;
        }

        private TeamVM CreateTeamVm(TeamIndex teamIdx, NetFoobbuteoTeam team)
        {
            return new TeamVM(
                teamIdx,
                team.Id,
                team.Name,
                m_spriteDB.GetPlayerSpriteDefault(team.Color),
                m_spriteDB.GetShirtSprite(team.Color),
                team.Color
            );
        }

        internal void AddCommentary(int turn, int cueIndex, MatchCommentaryCue cue)
        {
            string playerName;
            Sprite marker;
            if (cue.Player.IsEmpty)
            {
                playerName = string.Empty;
                marker = null;
            }
            else
            {
                playerName = $"{cue.Player.PlayerIdx}. {m_vm.GameState.GetPlayer(cue.Player).Name}";
                marker = (cue.Player.InHomeTeam ? HomeTeam : AwayTeam).Logo;
            }

            var entry = new MatchReportEntry(
                turn,
                cue,
                MatchReportText.Format(m_textFormatter, cue, playerName),
                marker
            );
            if (!m_reportSession.Add(cueIndex, entry))
                return;
            m_reportView.Append(entry);
        }

        internal void UpdateTeamInfo(TeamIndex teamIdx, NetFoobbuteoTeam teamInfo)
        {
            m_vm.UpdateTeamInfo(teamIdx, teamInfo);
            RefreshPlayerList();
        }

        internal void StartTurn(int current, int total)
        {
            m_vm.TurnText = $"Turn {current} / {total}";
            m_vm.RefreshSelectedPlayer();
            RefreshPlayerList();
        }

        internal void Pending(DateTimeOffset startTime, long baseServerTimeUnixSeconds, double serverTime)
        {
            if (m_vm.GameState.HomeTeam != null && m_vm.GameState.IsWaitingForOpponent)
            {
                m_vm.TurnText = "";
                m_vm.TimerText = "Waiting for opponent";
                return;
            }
            var serverDateTime = DateTimeOffset.FromUnixTimeSeconds(baseServerTimeUnixSeconds).AddSeconds(serverTime);
            if (startTime.Date == serverDateTime.Date)
            {
                m_vm.TurnText = $"Start Time: {startTime.ToString("HH:mm")}";
            }
            else
            {
                m_vm.TurnText = $"Start Time: {startTime.ToString("dd/MM/yyyy HH:mm")}";
            }
            PendingStartLeft(startTime, serverDateTime);
        }

        internal void PendingStartLeft(DateTimeOffset startTime, long baseServerTimeUnixSeconds, double serverTime)
        {
            var serverDateTime = DateTimeOffset.FromUnixTimeSeconds(baseServerTimeUnixSeconds).AddSeconds(serverTime);
            PendingStartLeft(startTime, serverDateTime);
        }

        private void PendingStartLeft(DateTimeOffset startTime, DateTimeOffset serverDateTime)
        {
            var timeLeft = startTime - serverDateTime;

            if (timeLeft.TotalSeconds <= 1d)
            {
                m_vm.UpdateTimer(GameHudVM.TimerType.Starting);
                return;
            }

            if (timeLeft.TotalSeconds >= 120d)
            {
                m_vm.UpdateTimer(GameHudVM.TimerType.MinutesLeft, (int)timeLeft.TotalMinutes);
            }
            else
            {
                m_vm.UpdateTimer(GameHudVM.TimerType.SecondsLeft, (int)timeLeft.TotalSeconds);
            }
        }

        internal void GameEnded()
        {
            m_vm.RefreshRoomPermissions();
            m_vm.TurnText = "Full Time";
            m_vm.TimerText = $"Final Result {m_vm.ScoreText}";
        }

        internal void UpdateStageEndTimer(int secondsLeft)
        {
            m_vm.UpdateTimer(GameHudVM.TimerType.TimeToMove, secondsLeft);
        }

        internal void EndPlayerMove()
        {
            m_vm.UpdateTimer(GameHudVM.TimerType.EndPlayerMove);
            m_vm.RefreshSelectedPlayer();
        }

        internal void UpdateShowResultsStage(int secondsLeft)
        {
            // m_vm.TimerText = $"Displaying turn: {seconds}s TODO";
            m_vm.UpdateTimer(GameHudVM.TimerType.RunningTurn, secondsLeft);
        }

        internal void Processing()
        {
            var result = m_vm.UpdateTimer(GameHudVM.TimerType.Processing);
            m_vm.RefreshSelectedPlayer();
            this.LogTrace(() => $"--Processing result: {result}");
        }

        internal void RefreshCommandAvailability()
        {
            m_vm.RefreshSelectedPlayer();
        }

        internal void UpdateScore(byte homeScore, byte awayScore)
        {
            m_vm.ScoreA = homeScore;
            m_vm.ScoreB = awayScore;
        }

        internal void SwitchPlayer(PlayerVM player)
        {
            m_vm.SwitchPlayer(player);
            // m_actionsContainerActions.style.visibility = player == null ? Visibility.Hidden : Visibility.Visible;

            m_actionsContainer.dataSource = m_vm;
        }

        // internal void SetUserPlayer(GameSlot slot)
        // {
        //      m_vm.UserPlayerKey = slot.PlayerKey;
        // }

        // internal void SetUserInOtherGame(bool value)
        // {
        //     m_vm.UserInOtherGame = value;
        // }

        #region ++ Tooltip ++

        [SerializeField]
        private float m_tooltipDelay = 0.5f;
        private Coroutine m_tooltipCoroutine;
        private VisualElement m_tooltipOrigin;

        private void InitTooltip()
        {
            VisualElement tooltipRoot = m_rootElement;
            if (tooltipRoot != null)
            {
                this.LogDebug("++" + tooltipRoot.pickingMode);
                tooltipRoot.RegisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
                tooltipRoot.RegisterCallback<PointerOutEvent>(OnPointerOut, TrickleDown.TrickleDown);
            }
        }

        private void OnPointerOver(PointerOverEvent evt)
        {
            if (evt.target is VisualElement element && !string.IsNullOrEmpty(element.tooltip))
            {
                // this.LogTrace($"{element.localBound}, {element.worldBound}, {GetTooltipPosition(element)}");
                m_tooltipOrigin = element;
                StartTooltipTimer(new TooltipData(string.Empty, element.tooltip), GetTooltipPosition(element));
            }
        }

        private Vector2 GetTooltipPosition(VisualElement element)
        {
            return new Vector2(element.worldBound.xMax, element.worldBound.center.y);
            // RuntimePanelUtils..ScreenToPanel(m_tooltip.panel, screenPosition);
        }

        private void OnPointerOut(PointerOutEvent evt)
        {
            StopTooltipTimer();
            HideTooltip();
        }

        private void StartTooltipTimer(TooltipData data, Vector2 position)
        {
            StopTooltipTimer();
            m_tooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay(data, position));
        }

        private void StopTooltipTimer()
        {
            if (m_tooltipCoroutine != null)
            {
                StopCoroutine(m_tooltipCoroutine);
                m_tooltipCoroutine = null;
            }
        }

        private IEnumerator ShowTooltipAfterDelay(TooltipData data, Vector2 position)
        {
            yield return new WaitForSeconds(m_tooltipDelay);
            m_tooltipService.Show(data, position, false, m_tooltipOrigin);
            m_tooltipCoroutine = null;
        }

        private void HideTooltip()
        {
            if (m_tooltipService != null)
                m_tooltipService.Hide();
        }

        internal void SetCaptainOfTeam(TeamIndex? teamIdx)
        {
            // m_vm.CaptainOfTeam = teamIdx;
            // TODO:
        }

        internal void UpdateUser(GameUser user)
        {
            m_vm.User = user;
            m_settings.SetAdmin(user?.IsAdmin == true);
        }

        internal void RefreshRoomPermissions() => m_vm.RefreshRoomPermissions();

        internal void RefreshSelectedPlayerPermissions() => m_vm.RefreshSelectedPlayer();

        internal void OnPlayerControlGranted(PlayerMB playerMB)
        {
            RefreshPlayerList();
            if (m_vm.SelectedPlayer == playerMB.PlayerVM)
            {
                m_vm.RefreshSelectedPlayer();
            }
        }

        #endregion -- Tooltip --
    }
}
