// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if UNITY_EDITOR
using Imma.Foobbuteo.Common.dev;
#endif

namespace Imma.Foobbuteo.Client
{
    [Serializable]
    internal sealed class ClientSettings
    {
        [JsonProperty("logging")]
        public LoggingSettings Logging { get; private set; }
        public string loginUrl;
        public string refreshUrl;
        public string revokeUrl;
        public string signupUrl;
        public string webSessionTokenUrl = "";

        public static ClientSettings Load(string environment = null)
        {
#if UNITY_EDITOR
            if (environment == null)
            {
                var profile = EditorSettings.CurrentProfile;
                var settings = Parse(profile["authentication"].ToString());
                var defaults = UnityEngine.Resources.Load<UnityEngine.TextAsset>("ClientSettings");
                if (defaults == null)
                    throw new InvalidOperationException("Missing Resources/ClientSettings.json.");
                settings.Logging = Parse(defaults.text, (string)profile["name"] == "dev" ? "dev" : "prod").Logging;
                return settings;
            }
#endif
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("ClientSettings");
            if (asset == null)
                throw new InvalidOperationException("Missing Resources/ClientSettings.json.");
            return Parse(asset.text, environment ?? ClientEnvironment.Production);
        }

        internal static ClientSettings Parse(string json, string environment = null)
        {
            try
            {
                JObject data = JObject.Parse(json);
                if (environment != null)
                {
                    ClientEnvironment.Validate(environment);
                    data =
                        data[environment] as JObject
                        ?? throw new InvalidOperationException("Missing selected client settings environment.");
                }

                foreach (string key in new[] { "loginUrl", "refreshUrl", "revokeUrl", "signupUrl" })
                {
                    if (data[key]?.Type != JTokenType.String)
                        throw new InvalidOperationException($"Authentication settings requires string '{key}'.");

                    string url = (string)data[key];
                    if (url.Length != 0 && !(key == "signupUrl" ? IsTrustedSignupUrl(url) : IsTrustedHttpsUrl(url)))
                        throw new InvalidOperationException(
                            $"Authentication settings requires a trusted HTTPS '{key}' / '{url}."
                        );
                }
                if (data["logging"]?.Type == JTokenType.Null)
                    throw new InvalidOperationException("Logging settings must be an object.");
                if (
                    data["webSessionTokenUrl"] != null
                    && (
                        data["webSessionTokenUrl"].Type != JTokenType.String
                        || ((string)data["webSessionTokenUrl"]).Length != 0
                            && !IsTrustedHttpsUrl((string)data["webSessionTokenUrl"])
                    )
                )
                    throw new InvalidOperationException("Invalid webSessionTokenUrl; use a trusted HTTPS URL.");
                return data.ToObject<ClientSettings>();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Invalid JSON in client authentication settings.", ex);
            }
        }

        private static bool IsTrustedSignupUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrEmpty(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        internal static bool IsTrustedHttpsUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrEmpty(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }
    }
}
