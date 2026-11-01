using System.Reflection;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Shared.Game.Model;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using EditorSettings = Imma.Foobbuteo.Common.dev.EditorSettings;

namespace Tests.Editor.Server
{
    public class DevEditorAuthenticationTests
    {
        private const string Token = "editor-profile-test-token";
        private const int TeamId = 1234;
        private static readonly FieldInfo ConfigField = typeof(EditorSettings).GetField(
            "s_config",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        private object m_originalConfig;

        [SetUp]
        public void SetUp() => m_originalConfig = ConfigField.GetValue(null);

        [TearDown]
        public void TearDown() => ConfigField.SetValue(null, m_originalConfig);

        [TestCase(false)]
        [TestCase(true)]
        public void DevEditorValidate_ConfiguredTeams_PreservesTeamIdentityAndName(bool numericEnums)
        {
            var key = new TeamKey(SourceGame.Footstar, TeamType.Club, TeamId, AgeCategory.U19);
            var team = new JObject
            {
                ["key"] = new JObject
                {
                    ["sourceGame"] = numericEnums ? new JValue(1) : new JValue("Footstar"),
                    ["teamType"] = numericEnums ? new JValue(0) : new JValue("Club"),
                    ["teamId"] = TeamId,
                    ["ageCategory"] = numericEnums ? new JValue(19) : new JValue("U19"),
                },
                ["name"] = "Editor club",
            };
            ConfigureUser(new JArray(team));

            var user = UserHandler.DevEditorValidate(1, Token);

            Assert.IsNotNull(user, "A matching Editor profile token should authenticate the user.");
            Assert.AreEqual(1, user.Teams.Count, "The authenticated user should receive the configured team.");
            Assert.AreEqual(key, user.Teams[0].Key, "All team identity fields should survive profile deserialization.");
            Assert.AreEqual("Editor club", user.Teams[0].Name, "The configured team name should be preserved.");
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("empty")]
        public void DevEditorValidate_NoConfiguredTeams_AuthenticatesWithNoTeams(string configuration)
        {
            ConfigureUser(configuration == "empty" ? new JArray() : JValue.CreateNull(), configuration != "missing");

            var user = UserHandler.DevEditorValidate(1, Token);

            Assert.IsNotNull(user, "Profiles without managed teams should continue to authenticate.");
            Assert.IsEmpty(user.Teams, $"The {configuration} managedTeams configuration should yield no teams.");
        }

        [Test]
        public void DevEditorValidate_UnknownToken_RejectsUser()
        {
            ConfigureUser(new JArray());

            var user = UserHandler.DevEditorValidate(1, "unknown-editor-token");

            Assert.IsNull(user, "An unknown token should not authenticate an Editor user.");
        }

        private static void ConfigureUser(JToken teams, bool includeTeams = true)
        {
            var profile = new JObject
            {
                ["profile"] = "host",
                ["token"] = Token,
                ["userId"] = 92001,
                ["playerId"] = 93001,
                ["name"] = "Editor host",
                ["role"] = 1,
            };
            if (includeTeams)
                profile["managedTeams"] = teams;
            ConfigField.SetValue(null, new JObject { ["devAuth"] = new JObject { ["users"] = new JArray(profile) } });
        }
    }
}
