// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client
{
    public partial class ClientAppCtrl : BaseMonoBehaviour, IClientAppCtrl
    {
        internal static ClientAppCtrl Instance { get; private set; }

        [SerializeField]
        private ClientImageCatalog m_images;
        internal ClientImageCatalog Images => m_images;

        [SerializeField]
        private NetworkManager m_NetworkManager;

        private ClientNetCtrl m_clientNetCtrl;
        private ClientConnectionConfig m_pendingConfig;
        private ClientAuthenticationCoordinator m_authentication;
        private readonly ClientConnectionRecovery m_recovery = new();
        private bool m_paused;
        private bool m_hasConnected;
        private bool m_recoveryStopped;
        private bool m_waitForEnvironment;
        private bool m_configurationFailed;
        private bool m_isAndroidStartup;
        private bool m_manualLoginStarted;
        private ConnectionStatusUiController m_connectionStatus;
        private string m_environment;
        private string m_configurationMessage = "Waiting for client environment configuration.";

        private VisualElement m_rootElement;
        private VisualElement m_toastContainer;
        private VisualElement m_bannerContainer;
        private const string k_LobbySceneName = "LobbyScene";
        private const string k_GameSceneName = "GameScene";
        private int m_openGameId;
        public int OpenGameId => m_openGameId;

        public void OnLobbySpawned()
        {
            if (m_openGameId == 0)
                OpenLobbyScene();
        }

        [SerializeField]
        private GameUser m_user;

        public GameUser User
        {
            get => m_user;
            set
            {
                m_user = value;
                UserInfoUpdated?.Invoke(value);
            }
        }
        internal ClientSettings ClientSettings { get; private set; }
        internal UiLayoutDiagnosticsController UiLayoutDiagnostics { get; } = new();

        public event Action<GameUser> UserInfoUpdated;
        public bool IsClientConnected => m_NetworkManager != null && m_NetworkManager.IsConnectedClient;
        public bool IsClientConnecting => m_clientNetCtrl?.IsConnecting ?? false;
        public event Action ClientConnectionStateChanged;
        public bool IsAuthenticationReady => m_authentication != null;
        public bool IsAuthenticating => m_authentication?.IsAuthenticating ?? false;
        public bool CanRememberLogin => m_authentication?.CanRemember ?? false;
        public bool HasRememberedLogin => m_authentication?.HasRememberedSession ?? false;
        public string AuthenticationMessage =>
            m_configurationFailed ? m_configurationMessage : m_authentication?.Message ?? m_configurationMessage;
        public ClientSessionView SessionView
        {
            get
            {
                if (m_configurationFailed)
                    return ClientSessionView.Problem;
                if (!IsAuthenticationReady)
                    return ClientSessionView.Loading;
                if (m_isAndroidStartup && m_manualLoginStarted && !m_hasConnected && !IsClientConnected)
                    return ClientSessionView.Login;
                if (IsAuthenticating || IsClientConnecting)
                    return m_hasConnected ? ClientSessionView.Session : ClientSessionView.Loading;
                if (m_authentication.RequiresLogin)
                    return ClientSessionView.Login;
                return m_hasConnected || IsClientConnected ? ClientSessionView.Session : ClientSessionView.Problem;
            }
        }
        public bool CanLogout => !m_waitForEnvironment;
        public event Action AuthenticationStateChanged;

        public Task LoginAsync(string login, string password, bool remember)
        {
            if (IsNetworkingActive || !IsAuthenticationReady)
                return Task.CompletedTask;
            m_manualLoginStarted = true;
            m_recoveryStopped = false;
            m_recovery.Cancel();
#if UNITY_ANDROID && !UNITY_EDITOR
            remember = true;
#endif
            return m_authentication.LoginAsync(login, password, remember);
        }

        public Task RetryRememberedLoginAsync() =>
            IsNetworkingActive || !IsAuthenticationReady ? Task.CompletedTask : m_authentication.RecoverAsync();

        private bool IsNetworkingActive => m_NetworkManager != null && m_NetworkManager.IsListening;

        public void Init()
        {
            if (AppCtx.Client != null)
            {
                this.LogDebug(() => $"AppCtx.Client already set");
                if (!ReferenceEquals(this, AppCtx.Client))
                {
                    Destroy(gameObject);
                }
                return;
            }
            this.LogDebug(() => $"Init new AppCtx.Client");
            AppCtx.Client = this;
            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
#if UNITY_EDITOR
                InitializeClient(false, null);
#elif UNITY_WEBGL
                InitializeClient(true, null);
#elif UNITY_ANDROID
                string overridePath = System.IO.Path.Combine(
                    Application.persistentDataPath,
                    ClientEnvironment.OverrideFileName
                );
                this.LogInfo($"Client environment override: {overridePath}");
                var startup = ClientEnvironment.LoadAndroidOverride(Application.persistentDataPath);
                InitializeClient(false, startup.Environment, startup.UiLayoutDiagnostics);
#endif
            }
            catch (InvalidOperationException ex)
            {
                ReportConfigurationError(ex.Message);
            }
        }

        internal void InitializeClient(bool waitForEnvironment, string environment, bool uiLayoutDiagnostics = false)
        {
            if (m_clientNetCtrl != null)
                return;
            m_waitForEnvironment = waitForEnvironment;
            m_clientNetCtrl = new ClientNetCtrl(m_NetworkManager);
            m_clientNetCtrl.ConnectionStateChanged += OnClientConnectionStateChanged;
            if (ClientSettings == null && !waitForEnvironment)
            {
                ClientSettings = ClientSettings.Load(environment);
                ImmLogger.Configure(ClientSettings.Logging);
                m_environment = environment;
                UiLayoutDiagnostics.Configure(uiLayoutDiagnostics);
            }
            UiLayoutDiagnostics.Initialize();
            if (ClientSettings != null)
                InitializeAuthentication();
            if (IsClientConnected)
                OnClientConnectionStateChanged();
        }

        private void InitializeAuthentication()
        {
            IRememberedSessionStore sessionStore;
#if UNITY_EDITOR
            sessionStore = FakeRememberedSessionStore.Instance;
#elif UNITY_ANDROID
            sessionStore = new EnvironmentRememberedSessionStore(new AndroidRememberedSessionStore(), m_environment);
#else
            sessionStore = new EmptyRememberedSessionStore();
#endif
            var web = new UnityAuthenticationWebClient(ClientSettings);
            m_authentication = new ClientAuthenticationCoordinator(
                web,
                sessionStore,
                webSession: m_waitForEnvironment ? web.RefreshWebSessionAsync : null
            );
            m_authentication.ConnectionReady += StartClientWithConfig;
            m_authentication.StateChanged += OnAuthenticationStateChanged;
            OnAuthenticationStateChanged();
            if (m_pendingConfig != null)
            {
                var config = m_pendingConfig;
                m_pendingConfig = null;
                m_authentication.AcceptExternalConfig(config);
            }
        }

        private void ReportConfigurationError(string error)
        {
            this.LogError($"Invalid client configuration: {error}");
            if (m_hasConnected || IsClientConnecting || IsAuthenticating || IsNetworkingActive)
                return;
            m_configurationFailed = true;
            m_configurationMessage = $"Client configuration error: {error}";
            OnAuthenticationStateChanged();
        }

        private void OnClientConnectionStateChanged()
        {
            if (IsClientConnected)
            {
                m_hasConnected = true;
                m_manualLoginStarted = false;
                m_recovery.Cancel();
            }
            ClearPendingToken();
            m_authentication?.UpdateConnectionState(
                IsClientConnected,
                IsClientConnecting,
                m_clientNetCtrl.DisconnectReason
            );
            if (!IsClientConnected && !IsClientConnecting)
            {
                string reason = m_clientNetCtrl.DisconnectReason;
                if (ClientDisconnectReason.IsInvalidToken(reason))
                {
                    // A consumed/expired game token does not invalidate the remembered or website session.
                    bool canRenewToken =
                        (m_authentication?.CanRecover ?? false) && (HasRememberedLogin || m_hasConnected);
                    if (!canRenewToken)
                        m_authentication?.RequireLogin();
                    else if (m_hasConnected && !m_recoveryStopped)
                        m_recovery.Schedule(Time.realtimeSinceStartupAsDouble);
                }
                else if (!ClientDisconnectReason.CanRetry(reason))
                {
                    m_recoveryStopped = true;
                    m_recovery.Cancel();
                }
                else if (m_hasConnected && !m_recoveryStopped && (m_authentication?.CanRecover ?? false))
                    m_recovery.Schedule(Time.realtimeSinceStartupAsDouble);
            }
            if (IsClientConnected && !string.IsNullOrEmpty(AuthenticationMessage))
                ShowToast(AuthenticationMessage);
            ClientConnectionStateChanged?.Invoke();
        }

        private void OnAuthenticationStateChanged()
        {
            if (m_authentication?.RequiresLogin == true)
            {
                m_recovery.Cancel();
                if (m_hasConnected)
                {
                    m_hasConnected = false;
                    OpenLobbyScene();
                }
            }
            AuthenticationStateChanged?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            m_paused = paused;
        }

        private void Update()
        {
            if (m_NetworkManager == null)
                return;
            if (
                m_recovery.TryBegin(
                    Time.realtimeSinceStartupAsDouble,
                    m_paused,
                    IsClientConnected
                        || IsNetworkingActive
                        || IsClientConnecting
                        || m_NetworkManager.ShutdownInProgress
                        || IsAuthenticating
                )
            )
                _ = RetryRememberedLoginAsync();
        }

        private void ClearPendingToken()
        {
            if (m_pendingConfig != null)
                m_pendingConfig.token = null;
            m_pendingConfig = null;
        }

        private new void OnDisable()
        {
            this.LogInfo("++");
            base.OnDisable();
            StopAndroidStartup();
            UiLayoutDiagnostics.Dispose();
            m_recovery.Cancel();
            m_connectionStatus?.Dispose();
            m_clientNetCtrl?.UnregisterNetworkEvents();
            m_clientNetCtrl?.ClearAuthToken();
            ClearPendingToken();
            m_authentication?.Dispose();
        }

        public void ReceiveClientConfig(string json)
        {
            if (!ClientBootstrapConfig.TryParse(json, out ClientBootstrapConfig message, out var error))
            {
                ReportConfigurationError(error);
                return;
            }
            if (message.IsLoggingOnly)
            {
                ImmLogger.Configure(message.Logging, ImmLogger.ConfigurationSource.JavaScript);
                return;
            }
            var config = message.Connection;
            if (IsClientConnecting || IsAuthenticating || IsNetworkingActive)
            {
                if (config != null)
                    config.token = null;
                this.LogWarn("Cannot replace client configuration while networking is active.");
                return;
            }
            if (
                message.Environment != null
                && (m_environment != null || IsAuthenticationReady)
                && message.Environment != m_environment
            )
            {
                if (config != null)
                    config.token = null;
                ReportConfigurationError("Client environment cannot change until the application is restarted.");
                return;
            }
            if (!UiLayoutDiagnostics.ValidateStartupConfiguration(message.UiLayoutDiagnostics, out error))
            {
                if (config != null)
                    config.token = null;
                ReportConfigurationError(error);
                return;
            }
            if (ClientSettings == null)
            {
                if (message.Environment == null)
                {
#if  UNITY_EDITOR || UNITY_ANDROID
                    // Native/Editor startup selects settings itself; retain early legacy connection messages.
                    if (m_clientNetCtrl == null)
                    {
                        ClearPendingToken();
                        m_pendingConfig = config;
                        ImmLogger.Configure(message.Logging, ImmLogger.ConfigurationSource.JavaScript);
                        return;
                    }
#endif
                    if (config != null)
                        config.token = null;
                    ReportConfigurationError("Supply the client environment before connection settings.");
                    return;
                }
                try
                {
                    var settings = ClientSettings.Load(message.Environment);
                    ClientSettings = settings;
                    ImmLogger.Configure(settings.Logging);
                    m_environment = message.Environment;
                    UiLayoutDiagnostics.Configure(message.UiLayoutDiagnostics ?? false);
                }
                catch (InvalidOperationException ex)
                {
                    if (config != null)
                        config.token = null;
                    ReportConfigurationError(ex.Message);
                    return;
                }
            }
            ImmLogger.Configure(message.Logging, ImmLogger.ConfigurationSource.JavaScript);
            m_configurationFailed = false;
            if (config != null)
            {
                m_recoveryStopped = false;
                ClearPendingToken();
                m_pendingConfig = config;
            }
            if (m_clientNetCtrl == null)
                return;
            if (!IsAuthenticationReady)
                InitializeAuthentication();
            else if (m_pendingConfig != null)
            {
                config = m_pendingConfig;
                m_pendingConfig = null;
                m_authentication.AcceptExternalConfig(config);
            }
            OnAuthenticationStateChanged();
        }

        private void StartClientWithConfig(ClientConnectionConfig config)
        {
            m_clientNetCtrl.SetClientConfig(config);
            m_clientNetCtrl.StartClient();
        }

        public void StartClient()
        {
            this.LogInfo($"++ App StartClient ++++++++++++++++");
            this.OpenLobbyScene();
            if (!IsAuthenticationReady)
                return;
            if (IsClientConnected || IsClientConnecting || IsAuthenticating || IsNetworkingActive)
                return;
            if (m_clientNetCtrl.HasUsableToken)
                m_clientNetCtrl.StartClient();
            else if (HasRememberedLogin)
                _ = RetryRememberedLoginAsync();
#if UNITY_EDITOR
            else if (m_authentication != null && !m_isAndroidStartup)
                m_clientNetCtrl.StartClient();
#endif
        }

        public bool OpenLobbyScene()
        {
            m_openGameId = 0;
            this.LogInfo($"::IsClient: {m_NetworkManager.IsClient}");
            if (SceneManager.GetActiveScene().name == k_LobbySceneName)
            {
                return false;
            }
            this.LogDebug(() => $"Scene load {k_LobbySceneName}, prev: {SceneManager.GetActiveScene().name}");
            SceneManager.LoadScene(k_LobbySceneName);
            return true;
        }

        public void OpenGameScene(int gameId)
        {
            this.LogInfo($"OpenGame ${gameId}");
            if (m_openGameId != gameId && SceneManager.GetActiveScene().name == k_GameSceneName)
            {
                m_openGameId = gameId;
                SceneManager.LoadScene(k_GameSceneName);
                return;
            }
            m_openGameId = gameId;
            LoadGameScene();
        }

        private bool LoadGameScene()
        {
            if (SceneManager.GetActiveScene().name == k_GameSceneName)
                return false;

            this.LogDebug(() => $"Scene load {k_GameSceneName}, prev: {SceneManager.GetActiveScene().name}");
            SceneManager.LoadScene(k_GameSceneName);

            return true;
        }

        #region ++ GUI ++

        public void SetRootElement(VisualElement rootElement)
        {
            m_connectionStatus?.Dispose();
            m_rootElement = rootElement;
            m_connectionStatus = new ConnectionStatusUiController(this, rootElement);
            m_toastContainer = m_rootElement?.Q<VisualElement>("toast-container");
            m_bannerContainer = m_rootElement?.Q<VisualElement>("banner-container");
            m_rootElement?.RegisterCallback<DetachFromPanelEvent>(OnRootElementDetached);
        }

        private void OnRootElementDetached(DetachFromPanelEvent evt)
        {
            if (!ReferenceEquals(evt.target, m_rootElement))
                return;

            m_rootElement = null;
            m_toastContainer = null;
            m_bannerContainer = null;
        }

        public void ShowToast(string message, float durationInSeconds = 3.0f)
        {
            ShowPanel(m_toastContainer, message, durationInSeconds, "toast-alert", replaceContent: false);
        }

        public void ShowBanner(string message, float durationInSeconds = 3.0f)
        {
            ShowPanel(m_bannerContainer, message, durationInSeconds, "banner-alert", replaceContent: true);
        }

        private VisualElement ShowPanel(
            VisualElement parent,
            string content,
            float durationInSeconds,
            string styleName,
            bool replaceContent
        )
        {
            if (parent == null)
            {
                this.LogWarn("Missing parent container can't show panel");
                return null;
            }
            if (parent.panel == null)
            {
                this.LogWarn("Detached parent container can't show panel");
                return null;
            }

            if (replaceContent)
                parent.Clear();

            Label label = new Label(content);
            label.AddToClassList(styleName);
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);

            long lifeTime = (long)(durationInSeconds * 1000);
            label.schedule.Execute(() => label.AddToClassList($"{styleName}--visible")).StartingIn(1);
            label.schedule.Execute(() => label.RemoveFromClassList($"{styleName}--visible")).StartingIn(lifeTime - 300);

            // Schedule its destruction after the duration
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn(lifeTime);
            return label;
        }

        #endregion -- GUI -

        public void DevDisconnect()
        {
            m_recoveryStopped = true;
            m_recovery.Cancel();
            this.LogInfo("-- Dev Disconnect Client -----------");
            m_NetworkManager.Shutdown();
        }

        public async Task LogoutAsync()
        {
            m_recoveryStopped = true;
            m_recovery.Cancel();
            m_hasConnected = false;
            m_manualLoginStarted = false;
            ClearPendingToken();
            m_clientNetCtrl?.Logout();
            User = default;
            OpenLobbyScene();
            if (m_authentication != null)
                await m_authentication.LogoutAsync();
        }
    }
}
