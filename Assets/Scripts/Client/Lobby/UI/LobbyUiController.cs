// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    public sealed class LobbyUiController : IDisposable
    {
        private static int s_dev_nextId = 0;
        private readonly int m_dev_objectId = 0;
        private string TraceId => $"[GL{m_dev_objectId}]";

        private PanelRenderer m_panelRenderer;
        private LoginUiController m_loginUi;
        private NewGameUiController m_newGameUi;
        private UserListsUiController m_usersUi;
        private readonly LoginVM m_loginVM;
        private bool m_disposed;

        private int m_UiVersion = 0;

        public LobbyUiController(PanelRenderer panelRenderer, ILobbyApiClient lobbyApi)
        {
            if (AppCtx.Client == null)
                this.LWarn("AppCtx.Client == null");
            m_dev_objectId = ++s_dev_nextId;
            m_lobbyVM = new LobbyVM();
            m_loginVM = new LoginVM(AppCtx.Client);
            Init(panelRenderer, lobbyApi);
            AppCtx.Client.ClientConnectionStateChanged += UpdateConnectionState;
            AppCtx.Client.AuthenticationStateChanged += UpdateConnectionState;
            UpdateConnectionState();
        }

        public void Init(PanelRenderer panelRenderer, ILobbyApiClient lobbyApi)
        {
            this.LDebug($"++");
            if (m_disposed)
                throw new ObjectDisposedException(nameof(LobbyUiController));
            if (m_panelRenderer != null)
                m_panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnbindUiEvents();
            m_panelRenderer = panelRenderer;
            m_UiVersion = 0;
            LobbyApi = lobbyApi;
            m_panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        internal void DetachPanel(PanelRenderer panelRenderer)
        {
            if (m_panelRenderer != panelRenderer)
                return;
            if (m_panelRenderer != null)
                m_panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnbindUiEvents();
            m_panelRenderer = null;
            m_UiVersion = 0;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
        {
            this.LDebug($"++ {version}");
            if (version == m_UiVersion)
                return;
            m_UiVersion = version;

            AppCtx.Client.SetRootElement(rootElement);
            UnbindUiEvents();
            m_rootElement = rootElement;
            BindUiEvents(rootElement);
            m_loginUi = new LoginUiController(rootElement, m_loginVM);
            UpdateResponsiveLayout();
            var client = ClientAppCtrl.Instance;
            if (client != null)
                client.NotifyLobbyUiReady(rootElement);
        }

        private readonly LobbyVM m_lobbyVM;

        private ILobbyApiClient m_lobbyApi;
        private ILobbyApiClient LobbyApi
        {
            get => m_lobbyApi;
            set
            {
                this.LDebug(() => $"{TraceId} prev: '{m_lobbyApi != null}', new: '{value != null}'");
                if (value == null)
                {
                    this.LWarn($"{TraceId} LobbyApi set to null!");
                }

                if (m_lobbyApi != null)
                {
                    if (m_lobbyApi == value)
                        return;
                    throw new AppException("GameListController: LobbyApi already set!");
                }
                m_lobbyApi = value;
                if (m_lobbyApi != null)
                {
                    this.LDebug(() => $"{TraceId} LobbyApi set value: {value}");
                    m_newGameUi = new NewGameUiController(
                        m_lobbyApi,
                        () => m_lobbyVM.CanCreateNewGame,
                        () => m_lobbyVM.User
                    );
                    if (m_lobbyApi is IUserPresenceApi presenceApi)
                        m_usersUi = new UserListsUiController(presenceApi, () => m_lobbyVM.IsAdmin);
                    m_lobbyApi.ServerInfoUpdated += UpdateGameList;
                    AppCtx.Client.UserInfoUpdated += UpdateUserInfo;
                    m_lobbyApi.AlertReceived += HandleAlert;
                }
            }
        }

        private const string k_gameListName = "main-view__game-list";
        private const string k_mainContainerName = "main-view";
        private const string k_joinBtnName = "details-container__join-btn";
        private const string k_deleteBtnName = "details-container__delete-btn";
        private const string k_newGameBtnName = "main-view__new-game-btn";
        private const long k_layoutPollIntervalMilliseconds = 100;

        private VisualElement m_rootElement;
        private VisualElement m_mainContainer;
        private Rect m_lastSafeArea;
        private Vector2 m_lastRootSize;
        private bool m_hasResponsiveLayout;
        private Vector2 m_lastLogicalSize;
        private IVisualElementScheduledItem m_layoutUpdate;
        private ListView m_GameListView;
        private Button m_joinBtn;
        private Button m_deleteBtn;
        private Button m_newGameBtn;
        private Button m_logoutBtn;
        private Button m_profileButton;
        private Button m_connectionRetry;
        private SessionLoadingSpinner m_spinnerAnimation;

        private void BindUiEvents(VisualElement root)
        {
            this.LTrace(() => $"{TraceId}");

            BindGamListView(root);

            m_mainContainer = root.Q<VisualElement>(k_mainContainerName);
            m_mainContainer.dataSource = m_lobbyVM;
            m_mainContainer.dataSourcePath = new PropertyPath();

#if UNITY_WEBGL
            m_mainContainer.AddToClassList("web-gl");
#endif

            m_joinBtn = root.Q<Button>(k_joinBtnName);
            this.LAssert(m_joinBtn != null, $"m_joinBtn==null");
            m_joinBtn.clicked += OnJoinClick;

            m_deleteBtn = root.Query<Button>(k_deleteBtnName);
            this.LAssert(m_joinBtn != null, $"m_deleteBtn==null");
            m_deleteBtn.clicked += OnDeleteGameClick;

            m_newGameBtn = root.Q<Button>(k_newGameBtnName);
            this.LAssert(m_newGameBtn != null, $"m_newGameBtn==null");
            m_newGameBtn.clicked += OnNewGameClick;
            m_newGameUi?.Bind(root);
            m_usersUi?.Bind(root);

            m_logoutBtn = root.Q<Button>("login-logout");
            m_logoutBtn.clicked += OnLogoutClick;
            m_profileButton = root.Q<Button>("profile-menu-toggle");
            m_profileButton.clicked += m_lobbyVM.ToggleProfileMenu;
            m_connectionRetry = root.Q<Button>("connection-retry");
            m_connectionRetry.clicked += OnConnectionRetry;

            m_spinnerAnimation = new SessionLoadingSpinner(root.Q("session-loading-spinner"));
            UpdateConnectionState();

            m_rootElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_layoutUpdate = m_rootElement
                .schedule.Execute(UpdateResponsiveLayout)
                .Every(k_layoutPollIntervalMilliseconds);
        }

        private void BindGamListView(VisualElement root)
        {
            m_GameListView = root.Q<ListView>(k_gameListName);

            m_GameListView.selectionChanged += OnGameSelected;
            m_GameListView.itemsChosen += OnListItemDoubleClicked;

            m_GameListView.bindItem = (row, index) =>
            {
                row.dataSourcePath = new PropertyPath();
                row.dataSource = m_GameListView.itemsSource[index];
            };

            m_GameListView.unbindItem = (row, index) =>
            {
                row.dataSource = null;
            };
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateResponsiveLayout();
        }

        private void UpdateResponsiveLayout()
        {
            if (m_rootElement == null || m_mainContainer == null)
                return;

            Rect rootRect = m_rootElement.contentRect;
            if (!UiViewportMetrics.TryRead(m_rootElement, out var metrics))
                return;
            Rect safeArea = metrics.SafeAreaPanelUnits;
            Vector2 rootSize = rootRect.size;

            if (
                m_hasResponsiveLayout
                && safeArea == m_lastSafeArea
                && rootSize == m_lastRootSize
                && metrics.Layout.LogicalSize == m_lastLogicalSize
            )
                return;

            m_lastSafeArea = safeArea;
            m_lastRootSize = rootSize;
            m_hasResponsiveLayout = true;
            m_lastLogicalSize = metrics.Layout.LogicalSize;

            this.LTrace(() =>
                $"[ResponsiveLayout] panelUnits={rootSize}, safeAreaPanelUnits={safeArea}, "
                + $"logicalSize={metrics.Layout.LogicalSize}"
            );

            m_mainContainer.style.marginTop = safeArea.yMin - rootRect.yMin;
            m_mainContainer.style.marginRight = rootRect.xMax - safeArea.xMax;
            m_mainContainer.style.marginBottom = rootRect.yMax - safeArea.yMax;
            m_mainContainer.style.marginLeft = safeArea.xMin - rootRect.xMin;
            ApplyLayout(m_mainContainer, metrics.Layout);
        }

        internal static void ApplyLayout(VisualElement main, UiScreenLayout layout)
        {
            main.EnableInClassList("narrow-screen", layout.IsNarrow);
            main.EnableInClassList("lobby--short", layout.IsShort);
        }

        private void OnListItemDoubleClicked(IEnumerable<object> objects)
        {
            if (objects.FirstOrDefault() is GameListItemVM vm)
            {
                LobbyApi.SendGameJoinRequest(vm.GameId);
            }
        }

        private void OnJoinClick()
        {
            if (m_lobbyVM.SelectedGame != null)
            {
                LobbyApi.SendGameJoinRequest(m_lobbyVM.SelectedGame.GameId);
            }
        }

        private void OnDeleteGameClick()
        {
            if (m_lobbyVM.SelectedGame != null)
            {
                LobbyApi.DeleteGame(m_lobbyVM.SelectedGame.GameId);
            }
        }

        private void OnNewGameClick()
        {
            m_newGameUi?.Open();
        }

        private static void OnLogoutClick() => _ = AppCtx.Client.LogoutAsync();

        private static void OnConnectionRetry() => _ = AppCtx.Client.RetryRememberedLoginAsync();

        private void UpdateConnectionState()
        {
            m_lobbyVM.UpdateConnectionState(AppCtx.Client.IsClientConnected, AppCtx.Client.IsClientConnecting);
            m_lobbyVM.UpdateSessionState(
                AppCtx.Client.SessionView,
                AppCtx.Client.AuthenticationMessage,
                AppCtx.Client.CanLogout
            );
            m_spinnerAnimation?.SetActive(m_lobbyVM.IsLoadingVisible);
            if (!AppCtx.Client.IsClientConnected)
                m_newGameUi?.Reset();
            else
                m_newGameUi?.Refresh();
        }

        internal void UpdateGameList(ServerInfoContract serverInfo)
        {
            this.LDebug(() => $"{TraceId}, Games count: {serverInfo.Games.Length}");
            m_lobbyVM.Games = serverInfo.Games.Select(g => GameListItemVM.FromContract(g)).ToList();
            m_newGameUi?.Refresh();
        }

        private void OnGameSelected(IEnumerable<object> enumerable)
        {
            m_lobbyVM.SelectedGame = m_GameListView.selectedItem as GameListItemVM;
        }

        private void UpdateUserInfo(GameUser user)
        {
            m_lobbyVM.User = user;
            if (user == null)
                m_newGameUi?.Reset();
            else
                m_newGameUi?.Refresh();
        }

        private static void HandleAlert(string message)
        {
            AppCtx.Client.ShowToast(message);
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            this.LDebug(() => $"-- {TraceId}");
            if (m_panelRenderer != null)
                m_panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            UnbindUiEvents();
            m_loginVM.Dispose();
            m_newGameUi?.Dispose();
            m_usersUi?.Dispose();
            AppCtx.Client.ClientConnectionStateChanged -= UpdateConnectionState;
            AppCtx.Client.AuthenticationStateChanged -= UpdateConnectionState;
            if (m_lobbyApi != null)
            {
                m_lobbyApi.ServerInfoUpdated -= UpdateGameList;
                AppCtx.Client.UserInfoUpdated -= UpdateUserInfo;
                m_lobbyApi.AlertReceived -= HandleAlert;
            }
        }

        private void UnbindUiEvents()
        {
            m_spinnerAnimation?.Dispose();
            m_spinnerAnimation = null;
            if (m_profileButton != null)
                m_profileButton.clicked -= m_lobbyVM.ToggleProfileMenu;
            if (m_connectionRetry != null)
                m_connectionRetry.clicked -= OnConnectionRetry;
            m_layoutUpdate?.Pause();
            m_layoutUpdate = null;
            m_newGameUi?.Unbind();
            m_usersUi?.Unbind();
            m_loginUi?.Dispose();
            m_loginUi = null;
            this.LTrace(() => $"-- {TraceId}");
            m_rootElement?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (m_mainContainer != null)
                m_mainContainer.dataSource = null;
            m_rootElement = null;
            m_mainContainer = null;
            m_hasResponsiveLayout = false;
            if (m_GameListView != null)
            {
                m_GameListView.selectionChanged -= OnGameSelected;
                m_GameListView.itemsChosen -= OnListItemDoubleClicked;
            }
            if (m_joinBtn != null)
            {
                m_joinBtn.clicked -= OnJoinClick;
            }

            if (m_deleteBtn != null)
            {
                m_deleteBtn.clicked -= OnDeleteGameClick;
            }

            if (m_newGameBtn != null)
            {
                m_newGameBtn.clicked -= OnNewGameClick;
            }

            if (m_logoutBtn != null)
                m_logoutBtn.clicked -= OnLogoutClick;
        }
    }
}
