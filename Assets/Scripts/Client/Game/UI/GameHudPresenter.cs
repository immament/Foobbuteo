// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using Imma.Common;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Client.UI.Help;
using Imma.Foobbuteo.Client.UI.Tooltip;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;
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
        private GameHudVM m_vm;

        private VisualElement m_rootElement;
        private int m_UiVersion = 0;
        private Camera m_pitchCamera;
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
                m_images
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
        private const string k_mobileGameCloseBtnName = "SideGameCloseButton";
        private Button m_mobileGameCloseBtn;
        private const string k_currentUserPlayerBtnName = "CurrentUserPlayerBtn";
        private Button m_currentUserPlayerBtn;
        private const string k_playerWithBallBtnName = "PlayerWithBallBtn";
        private Button m_playerWithBallBtn;

        private const string k_helpBtnName = "HelpButton";
        private Button m_helpBtn;
        private const string k_mobileHelpBtnName = "SideHelpButton";
        private Button m_mobileHelpBtn;

        private const string k_helpCloseBtnName = "HelpCloseButton";
        private Button m_helpCloseBtn;

        private const string k_helpContainerName = "HelpContainer";
        private VisualElement m_helpContainer;
        private const string k_popupBackgroundName = "PopupBackground";
        private VisualElement m_helpBackground;

        // private const string k_actionsContainerActionsName = "Actions";
        // private VisualElement m_actionsContainerActions;

        private const string k_PopupHiddenClass = "popup-container-hidden";
        private const string k_PopupBackgroundClass = "popup-background-hidden";

        new void Awake()
        {
            this.LogDebug("+++");
            this.LogAssert(m_vm != null, "m_vm==null");
            m_soundSettingsUi = new GameSettingsUiController(m_audioPlayer.Settings);
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
            m_vm.propertyChanged += OnHudPropertyChanged;
            m_vm.SwitchPlayer(null);
            m_vm.IsSessionReady = false;
            // m_vm.UserPlayerKey = null;
            m_vm.OnePlayerHasBall = false;
        }

        new void OnDestroy()
        {
            m_vm.propertyChanged -= OnHudPropertyChanged;
            m_reportView.Dispose();
            m_usersUi?.Dispose();
            m_managerJoinUi?.Dispose();
            m_soundSettingsUi?.Dispose();
            m_lineupUi?.Dispose();
            m_playerListUi?.Dispose();
            m_reportSession.Clear();
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        }

        void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            if (version == m_UiVersion)
                return;
            m_UiVersion = version;
            m_rootElement = rootElement;
            InitializeHelpImages();
            rootElement.Q("MainLayout").dataSource = m_vm;
            m_vm.RefreshRoomPermissions();
            m_usersUi?.Bind(rootElement);
            m_managerJoinUi?.Bind(rootElement);
            m_lineupUi?.Bind(rootElement);
            m_soundSettingsUi.Bind(rootElement);
            m_playerListUi?.Bind(rootElement);
            RefreshTimerStyle();
            AppCtx.Client.SetRootElement(rootElement);
            m_reportView.Bind(Q<ScrollView>("MatchReportScroll"), m_reportSession);

            m_actionsContainer = Q<VisualElement>("ActionsContainer");
            m_actionsContainer.dataSource = m_vm;

            m_claimPlayerBtn = Q<Button>(k_claimPlayerBtnName);
            m_claimPlayerBtn?.RegisterCallback<ClickEvent>(OnClaimPlayerBtnClick);

            m_selectedPlayerPopup = Q<VisualElement>(k_selectedPlayerPopupName);
            m_selectedPlayerPopup.dataSource = m_vm;

            m_selectedPlayerClaimBtn = Q<Button>(k_selectedPlayerClaimBtnName);
            m_selectedPlayerClaimBtn?.RegisterCallback<ClickEvent>(OnClaimPlayerBtnClick);

            m_currentUserPlayerBtn = Q<Button>(k_currentUserPlayerBtnName);
            m_currentUserPlayerBtn?.RegisterCallback<ClickEvent>(OnCurrentUserPlayerBtnClick);

            m_playerWithBallBtn = Q<Button>(k_playerWithBallBtnName);
            m_playerWithBallBtn?.RegisterCallback<ClickEvent>(OnPlayerWithBallBtnClick);

            m_gameCloseBtn = Q<Button>(k_gameCloseBtnName);
            m_gameCloseBtn?.RegisterCallback<ClickEvent>(OnGameCloseBtnClick);

            m_mobileGameCloseBtn = Q<Button>(k_mobileGameCloseBtnName);
            m_mobileGameCloseBtn?.RegisterCallback<ClickEvent>(OnGameCloseBtnClick);

            m_helpBtn = Q<Button>(k_helpBtnName);
            m_helpBtn?.RegisterCallback<ClickEvent>(OnHelpBtnClick);

            m_mobileHelpBtn = Q<Button>(k_mobileHelpBtnName);
            m_mobileHelpBtn?.RegisterCallback<ClickEvent>(OnHelpBtnClick);

            m_helpContainer = Q<VisualElement>(k_helpContainerName);

            m_helpBackground = Q<VisualElement>(k_popupBackgroundName);
            m_helpBackground.RegisterCallback<ClickEvent>(OnCloseHelpClick);

            m_helpCloseBtn = Q<Button>(k_helpCloseBtnName);
            m_helpCloseBtn.RegisterCallback<ClickEvent>(OnCloseHelpClick);

            // m_actionsContainerActions = Q<VisualElement>(k_actionsContainerActionsName);

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

        private void LateUpdate()
        {
            if (
                m_selectedPlayerPopup?.panel == null
                || m_selectedPlayerPopup.resolvedStyle.display == DisplayStyle.None
            )
                return;

            PlayerMB selectedPlayer = GameManager.Instance.SelectedPlayer;
            if (m_pitchCamera == null)
                m_pitchCamera = Camera.main;
            if (selectedPlayer == null || m_pitchCamera == null)
                return;

            Vector2 screenPosition = m_pitchCamera.WorldToScreenPoint(selectedPlayer.PopupAnchorPosition);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(m_selectedPlayerPopup.panel, screenPosition);
            float panelHeight = m_selectedPlayerPopup.panel.visualTree.resolvedStyle.height;

            m_selectedPlayerPopup.style.left = panelPosition.x;
            m_selectedPlayerPopup.style.top = panelHeight - panelPosition.y;
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

        private void OnHelpBtnClick(ClickEvent evt)
        {
            if (!m_helpContainer.ClassListContains(k_PopupHiddenClass))
            {
                return;
            }

            m_helpContainer.RemoveFromClassList(k_PopupHiddenClass);
            m_helpBackground.RemoveFromClassList(k_PopupBackgroundClass);
        }

        private void OnCloseHelpClick(ClickEvent evt)
        {
            if (m_helpContainer.ClassListContains(k_PopupHiddenClass))
            {
                return;
            }

            m_helpContainer.AddToClassList(k_PopupHiddenClass);
            m_helpBackground.AddToClassList(k_PopupBackgroundClass);
        }

        internal void InitializeMatch(BaseGameInfo baseGameInfo, GameUser user, ClientGameState gameState)
        {
            m_playerListUi?.Dispose();
            m_playerListUi = new PlayerListUiController(
                team => m_vm.GameState?.Team(team),
                team => (team == TeamIndex.Home ? HomeTeam : AwayTeam)?.Name ?? "Team",
                m_images
            );
            if (m_rootElement != null)
                m_playerListUi.Bind(m_rootElement);
            m_reportSession.Clear();
            if (m_rootElement != null)
                m_reportView.Bind(Q<ScrollView>("MatchReportScroll"), m_reportSession);
            m_vm.User = user;
            m_audioPlayer.Settings.SetAdmin(user?.IsAdmin == true);
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
            TooltipService.Instance.Show(data, position, false);
            m_tooltipCoroutine = null;
        }

        private void HideTooltip()
        {
            TooltipService.Instance.Hide();
        }

        internal void SetCaptainOfTeam(TeamIndex? teamIdx)
        {
            // m_vm.CaptainOfTeam = teamIdx;
            // TODO:
        }

        internal void UpdateUser(GameUser user)
        {
            m_vm.User = user;
            m_audioPlayer.Settings.SetAdmin(user?.IsAdmin == true);
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
