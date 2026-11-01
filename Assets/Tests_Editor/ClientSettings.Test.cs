using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Imma.Common;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Common.dev;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using EditorSettings = Imma.Foobbuteo.Common.dev.EditorSettings;

namespace Tests.Editor
{
    public class ClientSettingsTests
    {
        private string m_projectRoot;
        private string SettingsPath => Path.Combine(m_projectRoot, "editor.settings.json");

        [SetUp]
        public void SetUp()
        {
            m_projectRoot = Path.Combine(Path.GetTempPath(), "FoobbuteoSettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_projectRoot);
            File.Copy(
                Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "editor.settings.tmpl.json"),
                Path.Combine(m_projectRoot, "editor.settings.tmpl.json")
            );
        }

        [TearDown]
        public void TearDown() => Directory.Delete(m_projectRoot, true);

        [TestCase("dev", "dev.example.test")]
        [TestCase("prod", "prod.example.test")]
        public void Authentication_SelectsRequestedEnvironment(string environment, string expectedHost)
        {
            var environments = new JObject
            {
                ["dev"] = Authentication("dev.example.test"),
                ["prod"] = Authentication("prod.example.test"),
            };
            var settings = ClientSettings.Parse(environments.ToString(), environment);
            Assert.AreEqual(
                expectedHost,
                new Uri(settings.loginUrl).Host,
                "Authentication URLs must come from the explicitly selected environment."
            );
        }

        [Test]
        public void Authentication_MissingProductionDoesNotFallBackToDev()
        {
            var settings = new JObject { ["dev"] = Authentication("dev.example.test") };
            Assert.Throws<InvalidOperationException>(
                () => ClientSettings.Parse(settings.ToString(), ClientEnvironment.Production),
                "A missing production section must never redirect credentials to dev."
            );
        }

        [TestCase("not-json")]
        [TestCase("{\"loginUrl\":\"http://unsafe.example.test\",\"refreshUrl\":\"\",\"revokeUrl\":\"\"}")]
        [TestCase("{\"loginUrl\":123,\"refreshUrl\":\"\",\"revokeUrl\":\"\"}")]
        public void Authentication_InvalidSettingsReportsError(string json)
        {
            Assert.Throws<InvalidOperationException>(
                () => ClientSettings.Parse(json),
                "Malformed settings and unsafe endpoints must be rejected before sending credentials."
            );
        }

        [Test]
        public void Authentication_BlankProductionRemainsUnconfigured()
        {
            var settings = ClientSettings.Parse(
                "{\"loginUrl\":\"\",\"refreshUrl\":\"\",\"revokeUrl\":\"\",\"signupUrl\":\"\"}"
            );
            Assert.IsEmpty(settings.loginUrl, "Blank production endpoints must remain deliberately unconfigured.");
        }

        [Test]
        public void Authentication_SignupMayNavigateToRegistrationFragment()
        {
            var data = Authentication("auth.example.test");
            const string signupUrl = "https://auth.example.test/default.asp#registo";
            data["signupUrl"] = signupUrl;
            var settings = ClientSettings.Parse(data.ToString());
            Assert.AreEqual(
                signupUrl,
                settings.signupUrl,
                "Signup navigation must preserve the configured registration fragment."
            );
        }

        [TestCase("http://auth.example.test/signup")]
        [TestCase("https://user:password@auth.example.test/signup")]
        public void Authentication_UnsafeSignupUrlIsRejected(string signupUrl)
        {
            var data = Authentication("auth.example.test");
            data["signupUrl"] = signupUrl;
            Assert.Throws<InvalidOperationException>(
                () => ClientSettings.Parse(data.ToString()),
                "Signup navigation must still require HTTPS without embedded credentials."
            );
        }

        [Test]
        public void MainEditor_ResolvesItsOwnProjectRoot()
        {
            string resolved = EditorSettings.ResolveProjectRoot(Path.Combine(m_projectRoot, "Assets"));
            Assert.AreEqual(m_projectRoot, resolved, "A normal Editor must load settings from its own project root.");
        }

        [Test]
        public void VirtualEditor_LoadsMainSettingsWithoutLocalTemplateOrTokens()
        {
            Directory.CreateDirectory(Path.Combine(m_projectRoot, "Assets"));
            Directory.CreateDirectory(Path.Combine(m_projectRoot, "ProjectSettings"));
            string virtualRoot = Path.Combine(m_projectRoot, "Library", "VP", "test-instance");
            Directory.CreateDirectory(Path.Combine(virtualRoot, "Assets"));
            var original = EditorSettings.Load(m_projectRoot);

            string resolved = EditorSettings.ResolveProjectRoot(Path.Combine(virtualRoot, "Assets"));
            var settings = EditorSettings.Load(resolved);

            Assert.AreEqual(
                m_projectRoot,
                resolved,
                "An MPPM virtual Editor must resolve settings to the main Unity project."
            );
            Assert.IsTrue(
                JToken.DeepEquals(settings, original),
                "Virtual Editors must share the main Editor's endpoints, profiles, and tokens."
            );
            Assert.IsFalse(
                File.Exists(Path.Combine(virtualRoot, "editor.settings.json")),
                "Loading virtual Editor settings must not generate a separate configuration or tokens inside Library/VP."
            );
        }

        [Test]
        public void VirtualEditor_RejectsMissingMainProject()
        {
            string virtualAssets = Path.Combine(m_projectRoot, "Library", "VP", "test-instance", "Assets");
            Assert.Throws<InvalidOperationException>(
                () => EditorSettings.ResolveProjectRoot(virtualAssets),
                "A virtual project without a valid main Unity project must report the root resolution failure."
            );
        }

        [Test]
        public void OrdinaryProjectUnderVpDirectory_RemainsIndependent()
        {
            string ordinaryRoot = Path.Combine(m_projectRoot, "VP", "independent-project");
            Assert.AreEqual(
                ordinaryRoot,
                EditorSettings.ResolveProjectRoot(Path.Combine(ordinaryRoot, "Assets")),
                "A VP directory outside Library must not be treated as an MPPM clone."
            );
        }

        [TestCase("Web - Desktop - Development")]
        [TestCase("Web - Desktop - Fast")]
        [TestCase("Web - Desktop - Release")]
        [TestCase("Android™")]
        [TestCase("Windows")]
        public void BuildProfiles_DoNotSelectClientEnvironment(string profileName)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(
                $"Assets/Settings/Build Profiles/{profileName}.asset"
            );
            Assert.IsNotNull(profile, $"Expected build profile '{profileName}' must exist.");
            Assert.IsFalse(
                Array.Exists(profile.scriptingDefines, define => define == "FOOBBUTEO_DEV_ENV"),
                $"Profile '{profileName}' must leave client environment selection to runtime configuration."
            );
        }

        [Test]
        public void EditorCreation_GeneratesUniqueTokensForTemplateProfiles()
        {
            var users = EditorSettings.Load(m_projectRoot)["devAuth"]["users"].ToObject<DevAuthUser[]>();
            Assert.That(
                Array.ConvertAll(users, user => user.token),
                Is.Unique,
                "Each newly created development profile must receive a different token."
            );
            foreach (var user in users)
                Assert.That(
                    user.token,
                    Does.StartWith("dev_"),
                    $"New development profile '{user.profile}' must receive a generated token."
                );
        }

        [TestCase("dev", "remote.example.test")]
        [TestCase("local", "127.0.0.1")]
        public void EditorProfile_SelectsAuthenticationAndConnectionTogether(string profileName, string expectedHost)
        {
            var settings = EditorSettings.Load(m_projectRoot);
            var profiles = settings["profiles"].Children<JObject>();
            var dev = profiles.Single(profile => (string)profile["name"] == "dev");
            var local = profiles.Single(profile => (string)profile["name"] == "local");
            dev["authentication"] = Authentication("remote.example.test");
            dev["connection"]["host"] = "remote.example.test";
            local["authentication"] = Authentication("127.0.0.1");
            settings["currentProfile"] = profileName;

            var selected = EditorSettings.SelectProfile(settings);
            var authentication = ClientSettings.Parse(selected["authentication"].ToString());
            var connection = ClientConnectionConfig.ParseEditorConnection(selected["connection"].ToString());

            Assert.AreEqual(
                expectedHost,
                new Uri(authentication.loginUrl).Host,
                $"Profile '{profileName}' must supply its own authentication URLs."
            );
            Assert.AreEqual(
                expectedHost,
                connection.host,
                $"Profile '{profileName}' must supply the corresponding game connection."
            );
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown")]
        public void EditorProfile_InvalidSelectionDoesNotFallBack(string profileName)
        {
            var settings = EditorSettings.Load(m_projectRoot);
            settings["currentProfile"] = profileName;
            Assert.Throws<InvalidOperationException>(
                () => EditorSettings.SelectProfile(settings),
                "Missing, empty, or unknown currentProfile must not silently select another environment."
            );
        }

        [Test]
        public void EditorProfile_DuplicateNamesAreRejected()
        {
            var settings = EditorSettings.Load(m_projectRoot);
            settings["profiles"][1]["name"] = settings["profiles"][0]["name"].DeepClone();
            Assert.Throws<InvalidOperationException>(
                () => EditorSettings.SelectProfile(settings),
                "Duplicate configuration names must be rejected because profile selection would be ambiguous."
            );
        }

        [Test]
        public void EditorProfile_RereadingSelectionPreservesSharedUsersAndFile()
        {
            var settings = EditorSettings.Load(m_projectRoot);
            var originalUsers = settings["devAuth"].DeepClone();
            settings["currentProfile"] = "dev";
            string updatedFile = settings.ToString();
            File.WriteAllText(SettingsPath, updatedFile);

            var reloaded = EditorSettings.Load(m_projectRoot);
            var selected = EditorSettings.SelectProfile(reloaded);

            Assert.AreEqual(
                "dev",
                (string)selected["name"],
                "A connection settings reread must apply the edited currentProfile."
            );
            Assert.IsTrue(
                JToken.DeepEquals(reloaded["devAuth"], originalUsers),
                "Switching configuration profiles must preserve shared development users and their tokens."
            );
            Assert.AreEqual(
                updatedFile,
                File.ReadAllText(SettingsPath),
                "Loading an existing settings file must not rewrite it or generate replacement tokens."
            );
        }

        [Test]
        public void EditorConnection_RereadingFileAppliesChanges()
        {
            var settings = EditorSettings.Load(m_projectRoot);
            const string updatedHost = "updated.example.test";
            EditorSettings.SelectProfile(settings)["connection"]["host"] = updatedHost;
            File.WriteAllText(SettingsPath, settings.ToString());

            var connection = ClientConnectionConfig.ParseEditorConnection(
                EditorSettings.SelectProfile(EditorSettings.Load(m_projectRoot))["connection"].ToString()
            );

            Assert.AreEqual(
                updatedHost,
                connection.host,
                "A new settings read must use the edited connection without recompiling."
            );
            Assert.IsNull(connection.token, "Editor connection settings must not supply an authentication token.");
        }

        [TestCase("connection")]
        [TestCase("authentication")]
        [TestCase("devAuth")]
        public void EditorSettings_MissingSectionReportsError(string section)
        {
            var settings = EditorSettings.Load(m_projectRoot);
            if (section == "devAuth")
                settings.Remove(section);
            else
                EditorSettings.SelectProfile(settings).Remove(section);
            File.WriteAllText(SettingsPath, settings.ToString());
            Assert.Throws<InvalidOperationException>(
                () => EditorSettings.Load(m_projectRoot),
                $"Missing '{section}' must be reported without substituting template values."
            );
        }

        [TestCase("host", "https://invalid.example.test")]
        [TestCase("webSocketPath", "//invalid")]
        [TestCase("useEncryption", "true")]
        public void EditorConnection_InvalidValuesPreventConnection(string key, string value)
        {
            var connection = EditorSettings.SelectProfile(EditorSettings.Load(m_projectRoot))["connection"];
            connection[key] = value;
            Assert.Throws<InvalidOperationException>(
                () => ClientConnectionConfig.ParseEditorConnection(connection.ToString()),
                $"Invalid Editor connection '{key}' must be rejected before starting transport."
            );
        }

        [TestCase(0)]
        [TestCase(65536)]
        public void EditorConnection_PortOutsideTransportRangeIsRejected(int port)
        {
            var connection = EditorSettings.SelectProfile(EditorSettings.Load(m_projectRoot))["connection"];
            connection["port"] = port;
            Assert.Throws<InvalidOperationException>(
                () => ClientConnectionConfig.ParseEditorConnection(connection.ToString()),
                $"Port {port} cannot be represented as a valid transport port."
            );
        }

        [Test]
        public void StartupReset_DiscardsCachedSettingsWithoutDomainReload()
        {
            var cache = typeof(EditorSettings).GetField("s_config", BindingFlags.NonPublic | BindingFlags.Static);
            object previous = cache.GetValue(null);
            try
            {
                cache.SetValue(null, EditorSettings.Load(m_projectRoot));
                typeof(EditorSettings)
                    .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
                Assert.IsNull(
                    cache.GetValue(null),
                    "Play Mode subsystem registration must discard settings cached by a previous session."
                );
            }
            finally
            {
                cache.SetValue(null, previous);
            }
        }

        [Test]
        public void SuppliedConnection_TakesPrecedenceOverEditorFallback()
        {
            var instance = new GameObject("ClientSettings precedence test");
            ClientNetCtrl controller = null;
            try
            {
                var manager = instance.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                var transport = instance.AddComponent<UnityTransport>();
                manager.NetworkConfig.NetworkTransport = transport;
                controller = new ClientNetCtrl(manager);
                const string suppliedHost = "supplied.example.test";
                controller.SetClientConfig(
                    new ClientConnectionConfig
                    {
                        host = suppliedHost,
                        port = 443,
                        webSocketPath = "/supplied",
                        token = "test-supplied-token",
                    }
                );

                typeof(ClientNetCtrl)
                    .GetMethod("ConfigureUnityTransport", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(controller, new object[] { transport });

                Assert.AreEqual(
                    suppliedHost,
                    transport.ConnectionData.Address,
                    "Login/browser configuration must take precedence over Editor settings."
                );
                Assert.AreEqual(
                    "/supplied",
                    transport.ConnectionData.WebSocketPath,
                    "Transport must retain the supplied configuration's WebSocket path."
                );
            }
            finally
            {
                controller?.UnregisterNetworkEvents();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static JObject Authentication(string host) =>
            new()
            {
                ["loginUrl"] = $"https://{host}/login",
                ["refreshUrl"] = $"https://{host}/refresh",
                ["revokeUrl"] = $"https://{host}/revoke",
                ["signupUrl"] = $"https://{host}/signup",
            };
    }
}
