// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.Editor.Server
{
    public class ServerLoggingTests
    {
        [TearDown]
        public void TearDown() => ImmLogger.Reset();

        [TestCase("{ 'logging': { 'logDirectory':'nested-logs' } }", "nested-logs")]
        [TestCase("{ 'Logging': { 'LogDirectory':'nested-logs' } }", "nested-logs")]
        [TestCase("{ 'logging': { 'logDirectory':null } }", null)]
        [TestCase("{ 'logging': { 'logDirectory':'' } }", "")]
        [TestCase("{ 'logging': { 'logDirectory':'  ' } }", "  ")]
        [TestCase("{ 'logging':null }", null)]
        [TestCase("{}", null)]
        [TestCase("{ 'LogDirectory':'legacy-logs' }", null)]
        public void LogDirectory_IsReadDirectlyFromLoggingWithoutDefaultOrLegacyFallback(string json, string expected)
        {
            var config = JsonConvert.DeserializeObject<ServerConfig>(json);
            config.ConfigureLogging();
            Assert.AreEqual(
                expected,
                config.Logging.LogDirectory,
                "The log writer must receive only the nested directory; omitted values must remain null to disable file logging."
            );
        }

        [Test]
        public void Logging_DeserializesAsValidatedTypedSettings()
        {
            var config = JsonConvert.DeserializeObject<ServerConfig>(
                "{ 'logging': { 'logLevel':'debug', 'defaultSubLogLevel':'Info', "
                    + "'logDirectory':'server-logs', 'categories': { 'Auth':'Warn' } } }"
            );
            Assert.IsInstanceOf<LoggingSettings>(
                config.Logging,
                "Appsettings must deserialize logging into the shared typed configuration."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                config.Logging.LogLevel,
                "Typed logging must preserve case-insensitive named global levels."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                config.Logging.DefaultSubLogLevel,
                "Typed logging must preserve the default type threshold."
            );
            Assert.AreEqual(
                "server-logs",
                config.Logging.LogDirectory,
                "The log directory must be available directly on typed logging settings."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Warn,
                config.Logging.Categories["auth"],
                "Typed categories must retain case-insensitive lookup."
            );
        }

        [TestCase("{}")]
        [TestCase(
            "{ 'logLevel':'Debug', 'defaultSubLogLevel':'Info', 'logDirectory':'server-logs', 'categories': { 'Auth':'Warn' } }"
        )]
        public void Logging_JsonRoundTripPreservesValuesAndOmittedLevels(string json)
        {
            var original = JsonConvert.DeserializeObject<LoggingSettings>(json);
            var restored = JsonConvert.DeserializeObject<LoggingSettings>(JsonConvert.SerializeObject(original));
            Assert.AreEqual(
                original.LogLevel,
                restored.LogLevel,
                "Serialization must retain global levels, including an omitted level."
            );
            Assert.AreEqual(
                original.DefaultSubLogLevel,
                restored.DefaultSubLogLevel,
                "Serialization must retain default levels, including an omitted level."
            );
            Assert.AreEqual(
                original.LogDirectory,
                restored.LogDirectory,
                "Serialization must retain the optional log directory."
            );
            CollectionAssert.AreEquivalent(
                original.Categories,
                restored.Categories,
                "Serialization must retain category names and named levels."
            );
        }

        [Test]
        public void Logging_OmittedNestedLevelUsesLegacyFallbackIndependently()
        {
            var config = JsonConvert.DeserializeObject<ServerConfig>(
                "{ 'LogLevel':'Debug', 'DefaultSubLogLevel':'None', 'logging': { 'defaultSubLogLevel':'Info' } }"
            );
            config.ConfigureLogging();
            Assert.AreEqual(
                ImmLogger.LogLevel.Debug,
                ImmLogger.Level,
                "An omitted nested global level must retain the legacy global fallback."
            );
            Assert.AreEqual(
                ImmLogger.LogLevel.Info,
                ImmLogger.DefaultSubLogLevel,
                "A supplied nested default must override its legacy fallback independently."
            );
        }

        [TestCase("{ 'logging': { 'logLevel':7 } }")]
        [TestCase("{ 'logging': { 'defaultSubLogLevel':'Unknown' } }")]
        [TestCase("{ 'logging': { 'categories': { 'Auth':7 } } }")]
        public void Logging_RejectsInvalidSettingsDuringDeserialization(string json)
        {
            Assert.Catch(
                () => JsonConvert.DeserializeObject<ServerConfig>(json),
                "Typed appsettings deserialization must retain named-level and category validation."
            );
        }

        [Test]
        public void LegacyLevels_EnableDebugInEditor()
        {
            var config = JsonConvert.DeserializeObject<ServerConfig>(
                "{ 'LogLevel':'Debug', 'DefaultSubLogLevel':'Debug' }"
            );
            config.ConfigureLogging();
            Assert.IsTrue(
                ImmLogger.IsEnabled<ServerLoggingTests>(ImmLogger.LogLevel.Debug),
                "Legacy appsettings levels must enable Debug in Editor hosts as well as server players."
            );
        }

        [Test]
        public void NestedLevels_OverrideLegacyKeysButPreserveEditorPriority()
        {
            var config = JsonConvert.DeserializeObject<ServerConfig>(
                "{ 'LogLevel':'None', 'DefaultSubLogLevel':'None', "
                    + "'logging': { 'logLevel':'Debug', 'defaultSubLogLevel':'Debug' } }"
            );
            config.ConfigureLogging();
            Assert.IsTrue(
                ImmLogger.IsEnabled<ServerLoggingTests>(ImmLogger.LogLevel.Debug),
                "Nested server logging levels must take precedence over legacy root keys."
            );
            ImmLogger.Configure(
                LoggingSettings.Parse(JObject.Parse("{ 'logLevel':'Warn' }")),
                ImmLogger.ConfigurationSource.Editor
            );
            config.ConfigureLogging();
            Assert.AreEqual(
                ImmLogger.LogLevel.Warn,
                ImmLogger.Level,
                "Loading server settings later must preserve the Editor's global override."
            );
        }
    }
}
