// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imma.Foobbuteo.Client
{
    [Serializable]
    internal class ClientConnectionConfig
    {
        public string token;
        public string host;
        public int port;
        public string webSocketPath;
        public bool useEncryption = true;

        [NonSerialized]
        public DateTimeOffset? expiresAtUtc;

        public ClientConnectionConfig CopyConnection() =>
            new()
            {
                host = host,
                port = port,
                webSocketPath = webSocketPath,
                useEncryption = useEncryption,
            };

        public bool HasUsableToken(DateTimeOffset now) =>
            !string.IsNullOrWhiteSpace(token) && (!expiresAtUtc.HasValue || expiresAtUtc.Value > now);

#if UNITY_EDITOR
        internal static ClientConnectionConfig LoadEditorConnection() =>
            ParseEditorConnection(Imma.Foobbuteo.Common.dev.EditorSettings.ReadConnection().ToString());

        internal static ClientConnectionConfig ParseEditorConnection(string json)
        {
            JObject data;
            try
            {
                data = JObject.Parse(json);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Invalid JSON in Editor connection settings.");
            }
            if (data["useEncryption"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Editor connection settings requires boolean useEncryption.");
            if (!TryParse(json, false, out var config, out string error))
                throw new InvalidOperationException($"Invalid Editor connection settings: {error}");
            return config;
        }
#endif

        public static bool TryParse(string json, out ClientConnectionConfig config, out string error) =>
            TryParse(json, true, out config, out error);

        internal static bool TryParse(
            string json,
            bool requireToken,
            out ClientConnectionConfig config,
            out string error
        )
        {
            config = null;
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                error = "Expected a JSON object.";
                return false;
            }

            JObject data;
            try
            {
                data = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                ImmLogger.LWarn<ClientConnectionConfig>($"JSON parse problem: {ex.Message}");
                error = $"JSON parse problem.";
                return false;
            }

            if (requireToken && data["token"]?.Type != JTokenType.String)
            {
                error = "Expected string token.";
                return false;
            }

            if (data["host"]?.Type != JTokenType.String)
            {
                error = "Expected string host.";
                return false;
            }

            if (data["webSocketPath"]?.Type != JTokenType.String)
            {
                error = "Expected string webSocketPath.";
                return false;
            }

            if (data["port"]?.Type != JTokenType.Integer)
            {
                error = "Expected integer port.";
                return false;
            }

            if (!int.TryParse(data["port"].ToString(), out int port) || port < 1 || port > ushort.MaxValue)
            {
                error = "port must be an integer between 1 and 65535.";
                return false;
            }

            var parsed = new ClientConnectionConfig
            {
                token = requireToken ? (string)data["token"] : null,
                host = (string)data["host"],
                port = port,
                webSocketPath = (string)data["webSocketPath"],
                useEncryption = ParseUseEncryption(data),
            };
            if (requireToken && (string.IsNullOrWhiteSpace(parsed.token) || HasInvalidCharacters(parsed.token, false)))
            {
                error = "token must be a non-empty string without control characters.";
                return false;
            }

            if (
                string.IsNullOrWhiteSpace(parsed.host)
                || HasInvalidCharacters(parsed.host, true)
                || Uri.CheckHostName(parsed.host) == UriHostNameType.Unknown
            )
            {
                error = "host must be a hostname or IP address without a scheme, port, or path.";
                return false;
            }

            if (!IsValidWebSocketPath(parsed))
            {
                error =
                    "webSocketPath must start with a single / and contain no whitespace, query, fragment, or backslash.";
                return false;
            }

            config = parsed;
            error = null;
            return true;
        }

        private static bool ParseUseEncryption(JObject data)
        {
            return !Debug.isDebugBuild
                || (data["useEncryption"]?.Type) != JTokenType.Boolean
                || (bool)data["useEncryption"];
        }

        private static bool IsValidWebSocketPath(ClientConnectionConfig parsed)
        {
            return !string.IsNullOrEmpty(parsed.webSocketPath)
                && parsed.webSocketPath.StartsWith("/", StringComparison.Ordinal)
                && !parsed.webSocketPath.StartsWith("//", StringComparison.Ordinal)
                && parsed.webSocketPath.IndexOfAny(new[] { '?', '#', '\\' }) < 0
                && !HasInvalidCharacters(parsed.webSocketPath, true);
        }

        private static bool HasInvalidCharacters(string value, bool rejectWhitespace)
        {
            foreach (char character in value)
                if (char.IsControl(character) || (rejectWhitespace && char.IsWhiteSpace(character)))
                    return true;
            return false;
        }

        public override string ToString()
        {
            return $"{new { host, port, webSocketPath, useEncryption }}";
        }
    }
}
