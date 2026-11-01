// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Reflection;
using Imma.Common;
using Imma.Foobbuteo.Client;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace Tests.Editor.Client
{
    public class ClientLoggingTests
    {
        [TearDown]
        public void TearDown() => ImmLogger.Reset();

        [Test]
        public void LoggingOnlyMessage_ParsesWithoutEnvironmentOrConnection()
        {
            Assert.IsTrue(
                ClientBootstrapConfig.TryParse(
                    "{ 'logging': { 'categories': { 'Auth':'Debug' } } }",
                    out var config,
                    out string error
                ),
                $"A logging-only update must parse successfully: {error}"
            );
            Assert.IsTrue(
                config.IsLoggingOnly,
                "Category-only updates must not enter authentication or connection initialization."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                config.Logging.Categories["Auth"],
                "The bootstrap parser must preserve category levels."
            );
        }

        [TestCase("{ 'logging': { 'categories': { 'Auth':'Unknown' } } }")]
        [TestCase("{ 'logging': null }")]
        [TestCase("{ 'environment':'dev', 'logging': { 'categories': { 'Auth':7 } } }")]
        public void InvalidLogging_RejectsEntireBootstrapMessage(string json)
        {
            Assert.IsFalse(
                ClientBootstrapConfig.TryParse(json, out var config, out string error),
                "Malformed logging must reject the complete bootstrap message."
            );
            Assert.IsNull(config, "Rejected messages must not expose a partially parsed configuration.");
            Assert.IsNotEmpty(error, "Rejected logging settings must explain the invalid field.");
        }

        [Test]
        public void ClientSettings_ParsesLoggingFromSelectedEnvironment()
        {
            const string urls = "'loginUrl':'', 'refreshUrl':'', 'revokeUrl':'', 'signupUrl':''";
            var settings = ClientSettings.Parse(
                "{ 'prod': { " + urls + ", 'logging': { 'categories': { 'Auth':'Warn' } } } }",
                "prod"
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Warn,
                settings.Logging.Categories["Auth"],
                "Logging defaults must come from the selected client environment."
            );
        }

        [Test]
        public void EarlyLoggingOnlyUpdate_SurvivesSubsequentBaseConfiguration()
        {
            var instance = new GameObject("Logging configuration test");
            try
            {
                var client = instance.AddComponent<ClientAppCtrl>();
                var config = ImmLogger.AddType(typeof(ClientLoggingTests), "Auth");
                client.ReceiveClientConfig("{ 'logging': { 'categories': { 'Auth':'Trace' } } }");
                ImmLogger.Configure(LoggingSettings.Parse(JObject.Parse("{ 'categories': { 'Auth':'Warn' } }")));
                Assert.AreEqual(
                    ImmLogger.LogLevel.Trace,
                    config.minLevel,
                    "An early JS override must outrank a file loaded later."
                );
                Assert.IsNull(client.ClientSettings, "A logging-only update must not select an environment.");
                Assert.IsFalse(
                    client.IsAuthenticationReady,
                    "A logging-only update must not create authentication services."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LoggingOnlyUpdate_IsAcceptedWhileClientIsConnecting()
        {
            var instance = new GameObject("Connected logging configuration test");
            try
            {
                var manager = instance.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                var client = instance.AddComponent<ClientAppCtrl>();
                var networking = new ClientNetCtrl(manager);
                // Model an in-flight connection without opening a real transport.
                typeof(ClientNetCtrl)
                    .GetField("m_isConnecting", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(networking, true);
                typeof(ClientAppCtrl)
                    .GetField("m_clientNetCtrl", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(client, networking);
                var config = ImmLogger.AddType(typeof(ClientLoggingTests), "Auth");
                client.ReceiveClientConfig("{ 'logging': { 'categories': { 'Auth':'Info' } } }");
                Assert.AreEqual(
                    ImmLogger.LogLevel.Info,
                    config.minLevel,
                    "Connected/connecting clients must accept logging-only changes."
                );
                Assert.IsTrue(client.IsClientConnecting, "Logging updates must preserve connection state.");
                Assert.IsFalse(
                    client.IsAuthenticationReady,
                    "Logging updates must not initialize authentication while connecting."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
