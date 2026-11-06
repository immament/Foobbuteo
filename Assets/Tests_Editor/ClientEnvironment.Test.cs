using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor
{
    public class ClientEnvironmentTests
    {
        private string m_directory;
        private GameObject m_instance;
        private ClientAppCtrl m_client;
        private bool m_storeSupported;
        private RememberedSession m_savedSession;
        private const string ConnectionJson =
            "{\"token\":\"game-token\",\"host\":\"localhost\",\"port\":7777,"
            + "\"webSocketPath\":\"/\",\"useEncryption\":true}";

        [SetUp]
        public void SetUp()
        {
            m_directory = Path.Combine(Path.GetTempPath(), "FoobbuteoEnvironmentTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_directory);
            m_storeSupported = FakeRememberedSessionStore.Instance.IsSupported;
            m_savedSession = FakeRememberedSessionStore.Instance.Saved;
            FakeRememberedSessionStore.Instance.IsSupported = false;
            m_instance = new GameObject("Client environment test");
            var manager = m_instance.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            m_client = m_instance.AddComponent<ClientAppCtrl>();
            typeof(ClientAppCtrl)
                .GetField("m_NetworkManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(m_client, manager);
        }

        [TearDown]
        public void TearDown()
        {
            m_client.UiLayoutDiagnostics.Dispose();
            UnityEngine.Object.DestroyImmediate(m_instance);
            FakeRememberedSessionStore.Instance.IsSupported = m_storeSupported;
            FakeRememberedSessionStore.Instance.Saved = m_savedSession;
            Directory.Delete(m_directory, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HostConnection_ShowsSessionWhetherClientInitializesBeforeOrAfterConnection(bool alreadyConnected)
        {
            var manager = m_instance.GetComponent<NetworkManager>();
            const BindingFlags networkPropertyFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            void SetConnection(bool connected)
            {
                typeof(NetworkClient).GetProperty("IsServer", networkPropertyFlags).SetValue(manager.LocalClient, true);
                typeof(NetworkClient).GetProperty("IsClient", networkPropertyFlags).SetValue(manager.LocalClient, true);
                typeof(NetworkClient)
                    .GetProperty("IsConnected", networkPropertyFlags)
                    .SetValue(manager.LocalClient, connected);
            }
            try
            {
                if (alreadyConnected)
                    SetConnection(true);
                m_client.InitializeClient(false, "dev");
                SetConnection(true);
                var connection = typeof(ClientAppCtrl)
                    .GetField("m_clientNetCtrl", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(m_client);
                if (!alreadyConnected)
                    typeof(ClientNetCtrl)
                        .GetMethod("OnClientConnected", networkPropertyFlags)
                        .Invoke(connection, new object[] { NetworkManager.ServerClientId });

                Assert.IsTrue(m_client.IsClientConnected, "The local host client must be connected.");
                Assert.AreEqual(
                    ClientSessionView.Session,
                    m_client.SessionView,
                    "An approved local host must show its session without HTTP or remembered-login authentication."
                );

                int stateChanges = 0;
                m_client.ClientConnectionStateChanged += () => stateChanges++;
                const ulong remoteClientId = 7;
                foreach (string callback in new[] { "OnClientConnected", "OnClientDisconnect" })
                    typeof(ClientNetCtrl)
                        .GetMethod(callback, networkPropertyFlags)
                        .Invoke(connection, new object[] { remoteClientId });
                Assert.AreEqual(
                    0,
                    stateChanges,
                    "Remote clients joining or leaving the host must not change its local session state."
                );

                SetConnection(false);
                typeof(ClientNetCtrl)
                    .GetMethod("OnClientStopped", networkPropertyFlags)
                    .Invoke(connection, new object[] { true });
                Assert.AreEqual(1, stateChanges, "Stopping the host client must publish its disconnected state.");
                Assert.IsFalse(m_client.IsClientConnected, "The stopped local host must be disconnected.");
                Assert.AreEqual(
                    ClientSessionView.Session,
                    m_client.SessionView,
                    "Stopping the host connection must preserve its displayed session rather than open login."
                );
            }
            finally
            {
                foreach (string property in new[] { "IsConnected", "IsClient", "IsServer" })
                    typeof(NetworkClient)
                        .GetProperty(property, networkPropertyFlags)
                        .SetValue(manager.LocalClient, false);
            }
        }

        [TestCase("{\"environment\":\"dev\"}", "dev")]
        [TestCase("{\"environment\":\"prod\"}", "prod")]
        [TestCase("{\"environment\":\"dev\",\"token\":null}", "dev")]
        [TestCase("{\"environment\":\"dev\",\"token\":\"\"}", "dev")]
        public void Bootstrap_EnvironmentOnlyDoesNotContainConnection(string json, string expected)
        {
            Assert.IsTrue(
                ClientBootstrapConfig.TryParse(json, out var config, out var error),
                $"The hosting page must be able to select {expected} without a token: {error}"
            );
            Assert.AreEqual(expected, config.Environment, "The requested environment must be preserved.");
            Assert.IsNull(config.Connection, "Selecting an environment must not manufacture connection settings.");
        }

        [TestCase(true, "")]
        [TestCase(true, null)]
        [TestCase(false, null)]
        public void Bootstrap_CompleteTransportWithoutTokenDisplaysLogin(bool includeToken, string token)
        {
            var message = new JObject
            {
                ["environment"] = "dev",
                ["host"] = "localhost",
                ["port"] = 7777,
                ["webSocketPath"] = "/",
            };
            if (includeToken)
                message["token"] = token;
            m_client.InitializeClient(true, null);

            m_client.ReceiveClientConfig(message.ToString());

            Assert.AreEqual(
                ClientSessionView.Login,
                m_client.SessionView,
                "Missing, null, and empty JavaScript tokens must display login when configuration is otherwise valid."
            );
            Assert.IsFalse(m_client.IsClientConnecting, "A missing token must never start the game connection.");
        }

        [TestCase("{\"environment\":\"qa\"}")]
        [TestCase("{\"environment\":null}")]
        [TestCase("{\"environment\":\"dev\",\"token\":\"game-token\"}")]
        [TestCase("{\"environment\":\"dev\",\"useEncryption\":true}")]
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase(null)]
        public void Bootstrap_InvalidMessageReturnsNoPartialConfiguration(string json)
        {
            Assert.IsFalse(
                ClientBootstrapConfig.TryParse(json, out var config, out var error),
                "Unknown environments and incomplete connection contracts must be rejected."
            );
            Assert.IsNull(config, "Invalid messages must not expose partially parsed settings.");
            Assert.IsNotEmpty(error, "Rejected configuration must explain the error.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bootstrap_CompleteConnectionSupportsOptionalEnvironment(bool includeEnvironment)
        {
            string json = includeEnvironment ? ConnectionJson.Insert(1, "\"environment\":\"prod\",") : ConnectionJson;
            Assert.IsTrue(
                ClientBootstrapConfig.TryParse(json, out var config, out var error),
                $"Complete connection messages must retain their existing contract: {error}"
            );
            Assert.AreEqual(
                "game-token",
                config.Connection.token,
                "The supplied game token must reach the connection contract."
            );
        }

#if UNITY_ANDROID
        [Test]
        public void Android_MissingOverrideDefaultsToProduction()
        {
            Assert.AreEqual(
                ClientEnvironment.Production,
                ClientEnvironment.LoadAndroidOverride(m_directory).Environment,
                "An installation without a developer override must use production."
            );
        }

        [TestCase("prod")]
        [TestCase("dev")]
        public void Android_OverrideIsReadOnNextStartupAndRemovalRestoresProduction(string environment)
        {
            string path = Path.Combine(m_directory, ClientEnvironment.OverrideFileName);
            File.WriteAllText(path, "{\"environment\":\"" + environment + "\"}");
            Assert.AreEqual(
                environment,
                ClientEnvironment.LoadAndroidOverride(m_directory).Environment,
                "The startup reader must select the explicit developer override."
            );
            File.Delete(path);
            Assert.AreEqual(
                ClientEnvironment.Production,
                ClientEnvironment.LoadAndroidOverride(m_directory).Environment,
                "Removing the override must restore production on the next startup."
            );
        }

        [TestCase("not-json")]
        [TestCase("{\"environment\":\"qa\"}")]
        [TestCase("{\"environment\":123}")]
        [TestCase("{\"environment\":\"dev\",\"loginUrl\":\"https://other.test\"}")]
        public void Android_InvalidOverrideDoesNotFallBack(string json)
        {
            File.WriteAllText(Path.Combine(m_directory, ClientEnvironment.OverrideFileName), json);
            Assert.Throws<InvalidOperationException>(
                () => ClientEnvironment.LoadAndroidOverride(m_directory),
                "An existing invalid override must block authentication rather than silently use production."
            );
        }

        [Test]
        public void Android_UnreadableOverrideDoesNotFallBack()
        {
            string path = Path.Combine(m_directory, ClientEnvironment.OverrideFileName);
            File.WriteAllText(path, "{\"environment\":\"dev\"}");
            using var lockedFile = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Throws<InvalidOperationException>(
                () => ClientEnvironment.LoadAndroidOverride(m_directory),
                "An inaccessible existing override must report a configuration error."
            );
        }
#endif

        [TestCase("qa")]
        [TestCase("")]
        [TestCase("DEV")]
        public void Settings_UnknownEnvironmentIsRejected(string environment)
        {
            Assert.Throws<InvalidOperationException>(
                () => ClientSettings.Parse("{}", environment),
                "Only the exact bundled environment names may select authentication URLs."
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Web_EnvironmentMessageBeforeOrAfterInitializationEnablesAuthenticationWithoutConnecting(
            bool beforeInitialization
        )
        {
            if (!beforeInitialization)
            {
                m_client.InitializeClient(true, null);
                Assert.IsFalse(m_client.IsAuthenticationReady, "Web authentication must wait for the hosting page.");
                Assert.AreEqual(
                    ClientSessionView.Loading,
                    m_client.SessionView,
                    "WebGL must show loading rather than the login form before configuration arrives."
                );
                Assert.IsFalse(m_client.CanLogout, "WebGL must not offer Logout.");
                await m_client.LoginAsync("user", "password", false);
                Assert.IsFalse(m_client.IsAuthenticating, "Login attempts while waiting must not send credentials.");
            }
            m_client.ReceiveClientConfig("{\"environment\":\"dev\"}");
            if (beforeInitialization)
            {
                Assert.IsFalse(m_client.IsAuthenticationReady, "An early message must wait for client initialization.");
                m_client.InitializeClient(true, null);
            }
            Assert.IsTrue(m_client.IsAuthenticationReady, "A valid environment must initialize authentication.");
            Assert.AreEqual(
                ClientSessionView.Login,
                m_client.SessionView,
                "Configuration without a token must display the login form."
            );
            Assert.AreEqual(
                ClientSettings.Load("dev").loginUrl,
                m_client.ClientSettings.loginUrl,
                "Authentication must use the selected bundled URLs."
            );
            Assert.IsFalse(m_client.IsClientConnecting, "An environment-only message must not start networking.");
            Assert.IsFalse(
                m_instance.GetComponent<NetworkManager>().IsListening,
                "An environment-only message must not start the network manager."
            );
        }

        [Test]
        public void Web_PartialConnectionDoesNotSelectEnvironment()
        {
            m_client.InitializeClient(true, null);
            ExpectConfigurationError();
            m_client.ReceiveClientConfig("{\"environment\":\"dev\",\"token\":\"game-token\"}");
            Assert.IsFalse(m_client.IsAuthenticationReady, "An invalid combined message must not enable login.");
            Assert.IsNull(m_client.ClientSettings, "Settings must remain unset after partial validation fails.");
            m_client.ReceiveClientConfig("{\"environment\":\"prod\"}");
            Assert.IsTrue(m_client.IsAuthenticationReady, "A valid retry must be accepted after a rejected message.");
        }

        [Test]
        public void Web_ConnectionWithoutInitialEnvironmentIsRejected()
        {
            m_client.InitializeClient(true, null);
            ExpectConfigurationError();
            m_client.ReceiveClientConfig(ConnectionJson);
            Assert.IsFalse(m_client.IsAuthenticationReady, "Connection messages cannot implicitly select production.");
            Assert.That(
                m_client.AuthenticationMessage,
                Does.Contain("environment"),
                "The startup error must explain that the hosting page needs to supply the environment."
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Web_LaterConnectionUsesLockedEnvironment(bool repeatEnvironment)
        {
            m_client.InitializeClient(true, null);
            m_client.ReceiveClientConfig("{\"environment\":\"dev\",\"uiLayoutDiagnostics\":true}");
            var authentication = (ClientAuthenticationCoordinator)
                typeof(ClientAppCtrl)
                    .GetField("m_authentication", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(m_client);
            var start =
                (Action<ClientConnectionConfig>)
                    Delegate.CreateDelegate(
                        typeof(Action<ClientConnectionConfig>),
                        m_client,
                        typeof(ClientAppCtrl).GetMethod(
                            "StartClientWithConfig",
                            BindingFlags.NonPublic | BindingFlags.Instance
                        )
                    );
            authentication.ConnectionReady -= start;
            ClientConnectionConfig received = null;
            authentication.ConnectionReady += config => received = config;
            string json = repeatEnvironment ? ConnectionJson.Insert(1, "\"environment\":\"dev\",") : ConnectionJson;
            m_client.ReceiveClientConfig(json);
            Assert.IsTrue(
                m_client.UiLayoutDiagnostics.Enabled,
                "Later connections omitting diagnostics must preserve startup selection."
            );
            Assert.IsNotNull(received, "A complete later message must reach the connection workflow.");
            Assert.AreEqual("game-token", received.token, "The connection must receive the supplied game token.");
        }

        [Test]
        public void Web_EnvironmentIsLockedAndRepeatedSelectionDoesNotRecreateAuthentication()
        {
            m_client.InitializeClient(true, null);
            m_client.ReceiveClientConfig("{\"environment\":\"dev\"}");
            var original = m_client.ClientSettings;
            ExpectConfigurationError();
            m_client.ReceiveClientConfig("{\"environment\":\"prod\"}");
            m_client.ReceiveClientConfig("{\"environment\":\"dev\"}");
            Assert.AreSame(
                original,
                m_client.ClientSettings,
                "Neither another environment nor a repeated selection may replace initialized settings."
            );
        }

        [Test]
        public void Editor_EarlyLegacyConnectionDoesNotReplaceProfileSettings()
        {
            m_client.ReceiveClientConfig(ConnectionJson);
            Assert.IsFalse(
                m_client.IsAuthenticationReady,
                "An early Editor connection must wait for initialization without selecting an environment."
            );
            Assert.IsNull(m_client.ClientSettings, "Editor profile selection must remain deferred to normal startup.");
            Assert.IsFalse(
                m_instance.GetComponent<NetworkManager>().IsListening,
                "An early legacy connection must not start networking before initialization."
            );
        }

        [Test]
        public void Native_DefaultProductionInitializesImmediately()
        {
            m_client.InitializeClient(false, ClientEnvironment.Production);
            Assert.IsTrue(m_client.IsAuthenticationReady, "Native production players must not wait for a web page.");
            Assert.AreEqual(
                ClientSettings.Load("prod").loginUrl,
                m_client.ClientSettings.loginUrl,
                "Native players must initialize with production authentication URLs."
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Bootstrap_DiagnosticsBooleanIsPreserved(bool enabled)
        {
            string json =
                "{\"environment\":\"prod\",\"uiLayoutDiagnostics\":" + enabled.ToString().ToLowerInvariant() + "}";
            Assert.IsTrue(
                ClientBootstrapConfig.TryParse(json, out var config, out var error),
                $"Boolean diagnostics must parse: {error}"
            );
            Assert.AreEqual(enabled, config.UiLayoutDiagnostics, "The explicit diagnostics setting must be preserved.");
        }

        [TestCase("null")]
        [TestCase("1")]
        [TestCase("\"true\"")]
        public void Diagnostics_InvalidValuesRejectBothContracts(string value)
        {
            string json = "{\"environment\":\"prod\",\"uiLayoutDiagnostics\":" + value + "}";
            Assert.IsFalse(
                ClientBootstrapConfig.TryParse(json, out var config, out _),
                "Only JSON Booleans may select diagnostics."
            );
            Assert.IsNull(config, "Invalid diagnostics must not expose a partial configuration.");
#if UNITY_ANDROID
            File.WriteAllText(Path.Combine(m_directory, ClientEnvironment.OverrideFileName), json);
            Assert.Throws<InvalidOperationException>(
                () => ClientEnvironment.LoadAndroidOverride(m_directory),
                "Android must reject invalid diagnostics instead of falling back."
            );
#endif
        }

#if UNITY_ANDROID
        [TestCase(true)]
        [TestCase(false)]
        public void Android_DiagnosticsBooleanIsPreserved(bool enabled)
        {
            File.WriteAllText(
                Path.Combine(m_directory, ClientEnvironment.OverrideFileName),
                "{\"environment\":\"dev\",\"uiLayoutDiagnostics\":" + enabled.ToString().ToLowerInvariant() + "}"
            );
            var config = ClientEnvironment.LoadAndroidOverride(m_directory);
            Assert.AreEqual("dev", config.Environment, "Diagnostics must not change the selected environment.");
            Assert.AreEqual(
                enabled,
                config.UiLayoutDiagnostics,
                "Android must preserve the explicit diagnostics flag."
            );
        }
#endif

        [Test]
        public void Diagnostics_OmissionDefaultsToDisabled()
        {
            Assert.IsTrue(
                ClientBootstrapConfig.TryParse("{\"environment\":\"prod\"}", out var config, out _),
                "Existing web configuration must remain valid."
            );
            Assert.IsNull(config.UiLayoutDiagnostics, "Omission must remain distinguishable in later messages.");
#if UNITY_ANDROID
            Assert.IsFalse(
                ClientEnvironment.LoadAndroidOverride(m_directory).UiLayoutDiagnostics,
                "Missing Android files must disable diagnostics."
            );
            File.WriteAllText(
                Path.Combine(m_directory, ClientEnvironment.OverrideFileName),
                "{\"environment\":\"dev\"}"
            );
            Assert.IsFalse(
                ClientEnvironment.LoadAndroidOverride(m_directory).UiLayoutDiagnostics,
                "Existing Android files without the flag must disable diagnostics."
            );
#endif
            m_client.InitializeClient(true, null);
            m_client.ReceiveClientConfig("{\"environment\":\"prod\"}");
            Assert.IsFalse(
                m_client.UiLayoutDiagnostics.Enabled,
                "Web startup without the flag must disable diagnostics."
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Web_DiagnosticsSelectedBeforeOrAfterInitializationRemainLocked(bool beforeInitialization)
        {
            if (!beforeInitialization)
                m_client.InitializeClient(true, null);
            m_client.ReceiveClientConfig("{\"environment\":\"prod\",\"uiLayoutDiagnostics\":true}");
            if (beforeInitialization)
                m_client.InitializeClient(true, null);
            Assert.IsTrue(
                m_client.UiLayoutDiagnostics.Enabled,
                "Startup must retain diagnostics regardless of initialization order."
            );
            m_client.ReceiveClientConfig("{\"environment\":\"prod\"}");
            m_client.ReceiveClientConfig("{\"environment\":\"prod\",\"uiLayoutDiagnostics\":true}");
            ExpectConfigurationError();
            m_client.ReceiveClientConfig(ConnectionJson.Insert(1, "\"uiLayoutDiagnostics\":false,"));
            Assert.IsTrue(
                m_client.UiLayoutDiagnostics.Enabled,
                "A later opposite value must not replace startup diagnostics."
            );
            Assert.IsFalse(m_client.IsClientConnecting, "Rejected diagnostics must not partially start a connection.");
        }

        [Test]
        public void Native_StartupDiagnosticsAreAppliedAndLocked()
        {
            m_client.InitializeClient(false, ClientEnvironment.Production, true);
            Assert.IsTrue(m_client.UiLayoutDiagnostics.Enabled, "Android startup must be able to enable diagnostics.");
            ExpectConfigurationError();
            m_client.ReceiveClientConfig("{\"environment\":\"prod\",\"uiLayoutDiagnostics\":false}");
            Assert.IsTrue(
                m_client.UiLayoutDiagnostics.Enabled,
                "Startup diagnostics cannot be changed by a later message."
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Diagnostics_SceneDefaultsAndTransitionsUseStartupSetting(bool enabled)
        {
            m_client.InitializeClient(true, null);
            m_client.ReceiveClientConfig(
                "{\"environment\":\"prod\",\"uiLayoutDiagnostics\":" + enabled.ToString().ToLowerInvariant() + "}"
            );
            foreach (string sceneName in new[] { "LobbyScene", "GameScene", "LobbyScene" })
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(
                    $"Assets/Scenes/{sceneName}.unity"
                );
                try
                {
                    var diagnostics = System.Linq.Enumerable.Single(
                        System.Linq.Enumerable.SelectMany(
                            scene.GetRootGameObjects(),
                            root => root.GetComponentsInChildren<Imma.Foobbuteo.Client.UI.UiLayoutDiagnostics>(true)
                        )
                    );
                    Assert.IsFalse(diagnostics.enabled, $"{sceneName} must serialize diagnostics as disabled.");
                    Assert.AreEqual(
                        sceneName == "LobbyScene" ? "PanelRenderer" : "MainUI",
                        diagnostics.gameObject.name,
                        $"{sceneName} must have diagnostics on its intended panel."
                    );
                    m_client.UiLayoutDiagnostics.ApplyToScene(scene);
                    Assert.AreEqual(
                        enabled,
                        diagnostics.enabled,
                        $"Entering {sceneName} must apply the retained startup setting even to disabled components."
                    );
                }
                finally
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        [Test]
        public void Web_InvalidDiagnosticsDoNotPartiallySelectEnvironment()
        {
            m_client.InitializeClient(true, null);
            ExpectConfigurationError();
            m_client.ReceiveClientConfig("{\"environment\":\"dev\",\"uiLayoutDiagnostics\":1}");
            Assert.IsNull(m_client.ClientSettings, "Invalid diagnostics must leave startup environment unselected.");
            Assert.IsFalse(m_client.UiLayoutDiagnostics.Enabled, "Invalid diagnostics must leave logging disabled.");
            m_client.ReceiveClientConfig("{\"environment\":\"prod\",\"uiLayoutDiagnostics\":true}");
            Assert.IsTrue(
                m_client.UiLayoutDiagnostics.Enabled,
                "A valid retry must still be able to select diagnostics."
            );
        }

        private static void ExpectConfigurationError()
        {
            Imma.Common.ImmLogger.Level = Imma.Common.ImmLogger.LogLevel.Error;
            LogAssert.Expect(LogType.Error, new Regex("Invalid client configuration:"));
        }
    }
}
