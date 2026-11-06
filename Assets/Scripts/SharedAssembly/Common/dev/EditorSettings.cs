#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imma.Foobbuteo.Common.dev
{
    public static class EditorSettings
    {
        private static JObject s_config;
        private static string ProjectRoot => ResolveProjectRoot(Application.dataPath);
        public static JObject Config => s_config ??= Load(ProjectRoot);
        public static JObject CurrentProfile => SelectProfile(Config);

        internal static string ResolveProjectRoot(string assetsPath)
        {
            var project =
                Directory.GetParent(Path.GetFullPath(assetsPath))
                ?? throw new InvalidOperationException("Cannot resolve the Editor project root.");
            var virtualPlayers = project.Parent;
            var library = virtualPlayers?.Parent;
            // MPPM projects live at <main project>/Library/VP/<instance>, sharing Assets but not root JSON files.
            if (
                string.Equals(virtualPlayers?.Name, "VP", StringComparison.OrdinalIgnoreCase)
                && string.Equals(library?.Name, "Library", StringComparison.OrdinalIgnoreCase)
            )
            {
                var mainProject = library.Parent;
                if (
                    mainProject != null
                    && Directory.Exists(Path.Combine(mainProject.FullName, "Assets"))
                    && Directory.Exists(Path.Combine(mainProject.FullName, "ProjectSettings"))
                )
                    return mainProject.FullName;
                throw new InvalidOperationException("Cannot resolve the main Unity project for MPPM Editor settings.");
            }
            return project.FullName;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => s_config = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() =>
            ImmLogger.Configure(Config["logging"]?.ToObject<LoggingSettings>(), ImmLogger.ConfigurationSource.Editor);

        public static JObject ReadConnection() => (JObject)SelectProfile(Load(ProjectRoot))["connection"];

        internal static JObject SelectProfile(JObject config)
        {
            if (
                config["currentProfile"]?.Type != JTokenType.String
                || string.IsNullOrWhiteSpace((string)config["currentProfile"])
            )
                throw new InvalidOperationException("Editor settings requires a non-empty currentProfile.");
            if (config["profiles"] is not JArray profiles || profiles.Count == 0)
                throw new InvalidOperationException("Editor settings requires at least one configuration profile.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            JObject selected = null;
            foreach (JToken entry in profiles)
            {
                if (
                    entry is not JObject profile
                    || profile["name"]?.Type != JTokenType.String
                    || string.IsNullOrWhiteSpace((string)profile["name"])
                )
                    throw new InvalidOperationException("Each Editor configuration profile requires a non-empty name.");
                if (!names.Add((string)profile["name"]))
                    throw new InvalidOperationException("Editor configuration profile names must be unique.");
                foreach (string section in new[] { "authentication", "connection" })
                    if (profile[section] is not JObject)
                        throw new InvalidOperationException(
                            $"Each Editor profile requires an object section '{section}'."
                        );
                if (string.Equals((string)profile["name"], (string)config["currentProfile"], StringComparison.Ordinal))
                    selected = profile;
            }
            return selected
                ?? throw new InvalidOperationException("Editor currentProfile does not match any profile name.");
        }

        public static JObject Load(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "editor.settings.json");
            if (!File.Exists(path))
            {
                var settings = Read(Path.Combine(projectRoot, "editor.settings.tmpl.json"));
                Validate(settings);
                var users = (JArray)settings["devAuth"]["users"];
                foreach (JObject user in users)
                    if (string.IsNullOrWhiteSpace((string)user["token"]))
                        user["token"] = $"dev_{Guid.NewGuid():N}";

                // CreateNew prevents overwriting settings created by another Editor instance.
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream);
                writer.Write(settings.ToString());
                return settings;
            }
            var config = Read(path);
            Validate(config);
            return config;
        }

        private static JObject Read(string path)
        {
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                // Parser messages can contain authentication tokens from the document.
                throw new InvalidOperationException($"Invalid JSON in Editor settings: {path}");
            }
        }

        private static void Validate(JObject config)
        {
            _ = SelectProfile(config);
            if (config["logging"]?.Type == JTokenType.Null)
                throw new InvalidOperationException("Logging settings must be an object.");
            _ = config["logging"]?.ToObject<LoggingSettings>();
            if (config["devAuth"] is not JObject)
                throw new InvalidOperationException("Editor settings requires an object section 'devAuth'.");
            if (config["devAuth"]?["users"] is not JArray users || users.Count == 0)
                throw new InvalidOperationException("Editor settings requires at least one devAuth user.");
            foreach (JToken user in users)
                if (
                    user is not JObject
                    || user["profile"]?.Type != JTokenType.String
                    || user["token"]?.Type != JTokenType.String
                )
                    throw new InvalidOperationException("Each devAuth user requires string profile and token fields.");
        }
    }
}
#endif
