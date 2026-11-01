// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Foobbuteo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class StartupLoadingView : MonoBehaviour
    {
        private const float k_overlaySortingOrder = 100f;
        private const long k_startupTimeoutMilliseconds = 60000;
        private static readonly string[] s_sessionPanelNames =
        {
            "session-loading",
            "main-view__login",
            "connection-problem",
            "main-view__lobby",
        };

        private PanelRenderer m_renderer;
        private PanelSettings m_originalSettings;
        private PanelSettings m_runtimeSettings;
        private VisualElement m_root;
        private VisualElement m_lobbyRoot;
        private IClientAppCtrl m_client;
        private SessionLoadingSpinner m_spinner;
        private IVisualElementScheduledItem m_timeout;
        private Coroutine m_handover;
        private bool m_showing;
        private bool m_failed;

        private void Awake()
        {
            m_renderer = GetComponent<PanelRenderer>();
            m_renderer.enabled = false;
            if (Application.platform == RuntimePlatform.Android)
                Show();
        }

        public void Show()
        {
            if (m_showing)
                return;
            m_showing = true;
            m_failed = false;
            m_renderer = GetComponent<PanelRenderer>();
            m_originalSettings = m_renderer.panelSettings;
            m_runtimeSettings = Instantiate(m_originalSettings);
            m_runtimeSettings.name = m_originalSettings.name + " (Startup Runtime)";
            m_runtimeSettings.hideFlags = HideFlags.DontSave;
            m_runtimeSettings.sortingOrder = k_overlaySortingOrder;
            AndroidUiDensity.RefreshSettings(m_runtimeSettings, m_originalSettings);
            m_renderer.panelSettings = m_runtimeSettings;
            m_renderer.RegisterUIReloadCallback(OnUIReload);
            m_renderer.enabled = true;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            m_spinner?.Dispose();
            m_timeout?.Pause();
            m_root = root;
            m_spinner = new SessionLoadingSpinner(root.Q("session-loading-spinner"));
            if (m_failed)
            {
                ShowError();
                return;
            }
            m_spinner.SetActive(true);
            m_timeout = root.schedule.Execute(ShowError).StartingIn(k_startupTimeoutMilliseconds);
        }

        public void ShowError()
        {
            m_failed = true;
            UnobserveLobby();
            m_spinner?.SetActive(false);
            m_timeout?.Pause();
            if (m_root == null)
                return;
            m_root.Q("startup-loading").AddToClassList("session-loading--failed");
            m_root.Q<Label>("session-loading-message").text = "Unable to start the game. Please restart the app.";
        }

        internal void ObserveLobby(VisualElement root, IClientAppCtrl client)
        {
            UnobserveLobby();
            if (m_failed)
                return;
            m_lobbyRoot = root;
            m_client = client;
            foreach (string name in s_sessionPanelNames)
                root.Q(name).RegisterCallback<GeometryChangedEvent>(OnLobbyGeometryChanged);
            m_client.AuthenticationStateChanged += CheckLobbyReady;
            m_client.ClientConnectionStateChanged += CheckLobbyReady;
            CheckLobbyReady();
        }

        private void OnLobbyGeometryChanged(GeometryChangedEvent evt) => CheckLobbyReady();

        private void CheckLobbyReady()
        {
            if (m_handover == null && m_client != null && IsLobbyReady(m_lobbyRoot, m_client.SessionView))
                m_handover = StartCoroutine(HandoverAfterRender());
        }

        private IEnumerator HandoverAfterRender()
        {
            yield return new WaitForEndOfFrame();
            m_handover = null;
            if (m_client != null && IsLobbyReady(m_lobbyRoot, m_client.SessionView))
                Destroy(gameObject);
        }

        internal static bool IsLobbyReady(VisualElement root, ClientSessionView view)
        {
            if (root?.panel == null)
                return false;
            string expected = view switch
            {
                ClientSessionView.Loading => "session-loading",
                ClientSessionView.Login => "main-view__login",
                ClientSessionView.Problem => "connection-problem",
                _ => "main-view__lobby",
            };
            foreach (string name in s_sessionPanelNames)
            {
                var element = root.Q(name);
                if (element == null)
                    return false;
                bool displayed = element.resolvedStyle.display != DisplayStyle.None;
                if (displayed != (name == expected))
                    return false;
                if (displayed && !UiScreenLayout.IsValidSize(element.layout.size))
                    return false;
            }
            return true;
        }

        private void UnobserveLobby()
        {
            if (m_handover != null)
                StopCoroutine(m_handover);
            m_handover = null;
            if (m_client != null)
            {
                m_client.AuthenticationStateChanged -= CheckLobbyReady;
                m_client.ClientConnectionStateChanged -= CheckLobbyReady;
            }
            if (m_lobbyRoot != null)
            {
                foreach (string name in s_sessionPanelNames)
                    m_lobbyRoot.Q(name)?.UnregisterCallback<GeometryChangedEvent>(OnLobbyGeometryChanged);
            }
            m_client = null;
            m_lobbyRoot = null;
        }

        private void OnDisable()
        {
            UnobserveLobby();
            m_spinner?.Dispose();
            m_spinner = null;
            m_timeout?.Pause();
            m_timeout = null;
            m_root = null;
            if (m_renderer != null && m_showing)
            {
                m_renderer.UnregisterUIReloadCallback(OnUIReload);
                m_renderer.enabled = false;
                m_renderer.panelSettings = m_originalSettings;
            }
            if (m_runtimeSettings != null)
            {
                if (Application.isPlaying)
                    Destroy(m_runtimeSettings);
                else
                    DestroyImmediate(m_runtimeSettings);
            }
            m_runtimeSettings = null;
            m_originalSettings = null;
            m_showing = false;
        }
    }
}
