// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Editor.Utils;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public sealed class AndroidStartupTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        private GameObject m_object;
        private ClientAppCtrl m_client;
        private NetworkManager m_manager;
        private ClientNetCtrl m_network;
        private ClientAuthenticationCoordinator m_authentication;
        private readonly PendingWeb m_web = new();

        [SetUp]
        public void SetUp()
        {
            m_web.Pending = new TaskCompletionSource<AuthenticationResult>();
            m_object = new GameObject("Android startup test");
            m_manager = m_object.AddComponent<NetworkManager>();
            m_manager.NetworkConfig = new NetworkConfig();
            m_client = m_object.AddComponent<ClientAppCtrl>();
            m_network = new ClientNetCtrl(m_manager);
            SetField(m_client, "m_NetworkManager", m_manager);
            SetField(m_client, "m_clientNetCtrl", m_network);
            SetField(m_client, "m_isAndroidStartup", true);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(NetworkClient).GetProperty("IsConnected", PrivateInstance).SetValue(m_manager.LocalClient, false);
            Object.DestroyImmediate(m_object);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task PasswordLogin_KeepsAndroidFormUntilConnectedAndPreservesOtherPlatforms(bool android)
        {
            SetField(m_client, "m_isAndroidStartup", android);
            ConfigureAuthentication();
            using var vm = new LoginVM(m_client) { Login = "player", Password = "password" };

            Task signingIn = vm.SubmitAsync();

            var pendingView = android ? ClientSessionView.Login : ClientSessionView.Loading;
            Assert.AreEqual(
                pendingView,
                m_client.SessionView,
                "Only Android must retain the form during password authentication."
            );
            Assert.IsFalse(vm.InputsEnabled, "Submitting credentials must disable duplicate login attempts.");
            Assert.AreEqual(
                "Signing in...",
                vm.StatusText,
                "The retained form must explain the pending authentication."
            );
            Assert.IsEmpty(vm.Password, "The retained form must clear the submitted password.");

            m_web.Pending.SetResult(Success());
            await signingIn;
            Assert.AreEqual(
                pendingView,
                m_client.SessionView,
                "The same presentation must continue through server connection."
            );
            Assert.IsFalse(vm.InputsEnabled, "Connection pending must keep the login controls disabled.");
            Assert.AreEqual("Connecting...", vm.StatusText, "The form must report the pending game connection.");

            ChangeConnection(true, false);
            Assert.AreEqual(
                ClientSessionView.Session,
                m_client.SessionView,
                "Server approval must replace the form with the lobby."
            );
            ChangeConnection(false, false);
            Assert.AreEqual(
                ClientSessionView.Session,
                m_client.SessionView,
                "An established session must remain visible during recovery."
            );
        }

        [TestCase(AuthenticationResult.ErrorCodes.InvalidCredentials, "Invalid login or password.")]
        [TestCase(
            AuthenticationResult.ErrorCodes.NetworkError,
            "Cannot reach the login service. Check your connection and retry."
        )]
        public async Task PasswordLogin_FailureStaysOnFormWithInlineError(string code, string message)
        {
            ConfigureAuthentication();
            using var vm = new LoginVM(m_client) { Login = "player", Password = "password" };
            Task signingIn = vm.SubmitAsync();

            m_web.Pending.SetResult(AuthenticationResult.Failure(code, ""));
            await signingIn;

            Assert.AreEqual(
                ClientSessionView.Login,
                m_client.SessionView,
                "A rejected manual login must keep the form visible."
            );
            Assert.AreEqual(message, vm.StatusText, "The manual-login failure must be shown on the form.");
            Assert.IsTrue(vm.InputsEnabled, "A failed request must let the player correct or retry the login.");
        }

        [Test]
        public async Task PasswordLogin_ServerFailureStaysOnFormAndAllowsRetry()
        {
            ConfigureAuthentication();
            using var vm = new LoginVM(m_client) { Login = "player", Password = "password" };
            Task signingIn = vm.SubmitAsync();
            m_web.Pending.SetResult(Success());
            await signingIn;

            ChangeConnection(false, false);

            Assert.AreEqual(
                ClientSessionView.Login,
                m_client.SessionView,
                "A failed manual game connection must return control to the form."
            );
            StringAssert.Contains("Unable to connect", vm.StatusText, "The form must explain the game-server failure.");
            Assert.IsTrue(vm.InputsEnabled, "A failed connection must permit another manual login.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Startup_MissingOrExpiredSessionShowsLogin(bool expired)
        {
            ConfigureAuthentication(expired ? SavedSession(Now.AddSeconds(-1)) : null);

            Assert.AreEqual(
                ClientSessionView.Login,
                m_client.SessionView,
                "Startup without a usable saved session must select login."
            );
        }

        [Test]
        public async Task Startup_ValidRememberedSessionShowsLoadingUntilConnected()
        {
            ConfigureAuthentication(SavedSession(Now.AddDays(90)));
            Task restore = m_client.RetryRememberedLoginAsync();
            Assert.AreEqual(
                ClientSessionView.Loading,
                m_client.SessionView,
                "Automatic restoration must use the startup loading presentation."
            );

            m_web.Pending.SetResult(Success());
            await restore;
            Assert.AreEqual(
                ClientSessionView.Loading,
                m_client.SessionView,
                "Automatic restoration must keep loading during server connection."
            );
            ChangeConnection(true, false);
            Assert.AreEqual(
                ClientSessionView.Session,
                m_client.SessionView,
                "An approved remembered session must open the lobby."
            );
        }

        [TestCase(AuthenticationResult.ErrorCodes.InvalidSession, ClientSessionView.Login)]
        [TestCase(AuthenticationResult.ErrorCodes.ServiceUnavailable, ClientSessionView.Problem)]
        public async Task Startup_RememberedSessionFailurePreservesExistingRecovery(
            string error,
            ClientSessionView expected
        )
        {
            ConfigureAuthentication(SavedSession(Now.AddDays(90)));
            Task restore = m_client.RetryRememberedLoginAsync();
            m_web.Pending.SetResult(AuthenticationResult.Failure(error, ""));
            await restore;

            Assert.AreEqual(
                expected,
                m_client.SessionView,
                "Invalid saved credentials must open login; transient failures must offer connection retry."
            );
        }

        [UnityTest]
        public IEnumerator LobbyReadiness_RequiresAttachedLayoutAndBindingsForTheCurrentSessionView()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                    .CloneTree();
                var panels = new[] { "session-loading", "main-view__login", "connection-problem", "main-view__lobby" }
                    .Select(name => root.Q(name))
                    .ToArray();
                foreach (var element in root.Query<VisualElement>().ToList())
                    if (!panels.Contains(element))
                        element.ClearBindings();
                var vm = new LobbyVM();
                root.Q("main-view").dataSource = vm;
                Assert.IsFalse(
                    StartupLoadingView.IsLobbyReady(root, ClientSessionView.Loading),
                    "Detached UI must never release the startup overlay."
                );
                window.Show();
                window.rootVisualElement.Add(root);

                foreach (
                    var view in new[]
                    {
                        ClientSessionView.Loading,
                        ClientSessionView.Login,
                        ClientSessionView.Problem,
                        ClientSessionView.Session,
                    }
                )
                {
                    vm.UpdateSessionState(view, "Service unavailable", true);
                    yield return TestUtils.WaitForUi(
                        () => StartupLoadingView.IsLobbyReady(root, view),
                        $"Rendered and bound {view} view"
                    );
                    var otherView =
                        view == ClientSessionView.Loading ? ClientSessionView.Login : ClientSessionView.Loading;
                    Assert.IsFalse(
                        StartupLoadingView.IsLobbyReady(root, otherView),
                        "A previous or unrelated visible view must not complete handover."
                    );
                }
                root.RemoveFromHierarchy();
                Assert.IsFalse(
                    StartupLoadingView.IsLobbyReady(root, ClientSessionView.Session),
                    "Detaching a previously ready document must revoke readiness."
                );
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator StartupView_ReloadAndFailureReleaseLobbySubscriptionsAndStopLoading()
        {
            ConfigureAuthentication();
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var viewObject = new GameObject("Startup loading lifecycle test");
            var view = viewObject.AddComponent<StartupLoadingView>();
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI Toolkit/Lobby/StartupLoading.tmpl.uxml"
            );
            var lobby = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                .CloneTree();
            var reload = typeof(StartupLoadingView).GetMethod("OnUIReload", PrivateInstance);
            try
            {
                window.Show();
                var root = template.CloneTree();
                window.rootVisualElement.Add(root);
                reload.Invoke(view, new object[] { null, root, 1 });
                view.ObserveLobby(lobby, m_client);
                view.ObserveLobby(lobby, m_client);
                Assert.AreEqual(
                    1,
                    AuthenticationSubscriberCount(),
                    "Replacing the observed lobby must not duplicate client subscriptions."
                );

                view.ShowError();
                yield return TestUtils.WaitForUi(
                    () => root.Q("session-loading-spinner").resolvedStyle.display == DisplayStyle.None,
                    "Startup error hides the spinner"
                );
                StringAssert.Contains(
                    "Unable to start",
                    root.Q<Label>("session-loading-message").text,
                    "A failed startup must show a readable error."
                );
                Assert.AreEqual(
                    0,
                    AuthenticationSubscriberCount(),
                    "Failure must release the lobby readiness subscriptions."
                );

                root.RemoveFromHierarchy();
                var replacement = template.CloneTree();
                window.rootVisualElement.Add(replacement);
                reload.Invoke(view, new object[] { null, replacement, 2 });
                yield return TestUtils.WaitForUi(
                    () => replacement.Q("session-loading-spinner").resolvedStyle.display == DisplayStyle.None,
                    "UI reload preserves the startup failure"
                );
                StringAssert.Contains(
                    "Unable to start",
                    replacement.Q<Label>("session-loading-message").text,
                    "Reloading the UI must retain the startup error instead of restarting loading."
                );
            }
            finally
            {
                typeof(StartupLoadingView).GetMethod("OnDisable", PrivateInstance).Invoke(view, null);
                Object.DestroyImmediate(viewObject);
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        private int AuthenticationSubscriberCount() =>
            (
                typeof(ClientAppCtrl).GetField("AuthenticationStateChanged", PrivateInstance).GetValue(m_client)
                as Delegate
            )
                ?.GetInvocationList()
                .Length ?? 0;

        private void ConfigureAuthentication(RememberedSession saved = null)
        {
            m_authentication = new ClientAuthenticationCoordinator(m_web, new MemoryStore { Saved = saved }, () => Now);
            SetField(m_client, "m_authentication", m_authentication);
            m_authentication.StateChanged += () =>
                typeof(ClientAppCtrl).GetMethod("OnAuthenticationStateChanged", PrivateInstance).Invoke(m_client, null);
            m_authentication.ConnectionReady += _ => ChangeConnection(false, true);
        }

        private void ChangeConnection(bool connected, bool connecting)
        {
            typeof(NetworkClient)
                .GetProperty("IsConnected", PrivateInstance)
                .SetValue(m_manager.LocalClient, connected);
            SetField(m_network, "m_isConnecting", connecting);
            typeof(ClientAppCtrl).GetMethod("OnClientConnectionStateChanged", PrivateInstance).Invoke(m_client, null);
        }

        private static void SetField(object instance, string field, object value) =>
            instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);

        private static RememberedSession SavedSession(DateTimeOffset expiry) =>
            new() { refreshToken = "saved-refresh", refreshExpiresAtUtc = expiry.ToString("yyyy-MM-ddTHH:mm:ssZ") };

        private static AuthenticationResult Success() =>
            AuthenticationResult.Parse(
                200,
                "application/json",
                new JObject
                {
                    ["token"] = "game-token",
                    ["host"] = "game.example.test",
                    ["port"] = 443,
                    ["webSocketPath"] = "/game",
                    ["useEncryption"] = true,
                    ["expiresAtUtc"] = Now.AddSeconds(50).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["refreshToken"] = "rotated-refresh",
                    ["refreshExpiresAtUtc"] = Now.AddDays(90).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                }.ToString(),
                true,
                Now
            );

        private sealed class MemoryStore : IRememberedSessionStore
        {
            public bool IsSupported => true;
            public RememberedSession Saved;

            public RememberedSession Load() => Saved;

            public void Save(RememberedSession session) => Saved = session;

            public void Clear() => Saved = null;
        }

        private sealed class PendingWeb : IAuthenticationWebClient
        {
            public TaskCompletionSource<AuthenticationResult> Pending;

            public Task<AuthenticationResult> LoginAsync(
                string login,
                string password,
                bool remember,
                CancellationToken cancellation
            ) => Pending.Task;

            public Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellation) =>
                Pending.Task;

            public Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellation) => Task.FromResult(true);
        }
    }
}
