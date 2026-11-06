// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imma.Foobbuteo.Client
{
    internal sealed class ClientBootstrapConfig
    {
        public string Environment { get; private set; }
        public LoggingSettings Logging { get; private set; }
        public bool IsLoggingOnly =>
            Logging != null && Environment == null && Connection == null && UiLayoutDiagnostics == null;
        public ClientConnectionConfig Connection { get; private set; }
        public bool? UiLayoutDiagnostics { get; private set; }

        public static bool TryParse(string json, out ClientBootstrapConfig config, out string error)
        {
            config = null;
            JObject data;
            try
            {
                data = JObject.Parse(json ?? "");
            }
            catch (JsonException)
            {
                error = "Expected a client configuration JSON object.";
                return false;
            }

            LoggingSettings logging;
            try
            {
                if (data["logging"]?.Type == JTokenType.Null)
                    throw new InvalidOperationException("Logging settings must be an object.");
                logging = data["logging"]?.ToObject<LoggingSettings>();
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }

            string environment = null;
            if (data.Property("environment") != null)
            {
                if (data["environment"]?.Type != JTokenType.String)
                {
                    error = "Client environment must be 'prod' or 'dev'.";
                    return false;
                }
                environment = (string)data["environment"];
                try
                {
                    ClientEnvironment.Validate(environment);
                }
                catch (InvalidOperationException)
                {
                    error = "Client environment must be 'prod' or 'dev'.";
                    return false;
                }
            }

            bool? uiLayoutDiagnostics = null;
            if (data.Property("uiLayoutDiagnostics") != null)
            {
                if (data["uiLayoutDiagnostics"].Type != JTokenType.Boolean)
                {
                    error = "Client uiLayoutDiagnostics must be a Boolean.";
                    return false;
                }
                uiLayoutDiagnostics = (bool)data["uiLayoutDiagnostics"];
            }

            ClientConnectionConfig connection = null;
            bool hasConnection =
                data.Property("token") != null
                || data.Property("host") != null
                || data.Property("port") != null
                || data.Property("webSocketPath") != null
                || data.Property("useEncryption") != null;
            if (hasConnection)
            {
                bool missingToken =
                    data["token"] == null
                    || data["token"].Type == JTokenType.Null
                    || data["token"].Type == JTokenType.String && string.IsNullOrWhiteSpace((string)data["token"]);
                bool hasTransport =
                    data.Property("host") != null
                    || data.Property("port") != null
                    || data.Property("webSocketPath") != null
                    || data.Property("useEncryption") != null;
                if (!missingToken || hasTransport)
                {
                    if (!ClientConnectionConfig.TryParse(json, !missingToken, out var parsedConnection, out error))
                        return false;
                    if (!missingToken)
                        connection = parsedConnection;
                }
            }
            if (environment == null && connection == null && logging == null)
            {
                error =
                    "Client configuration requires an environment, complete connection settings, or logging settings.";
                return false;
            }

            config = new ClientBootstrapConfig
            {
                Environment = environment,
                Logging = logging,
                Connection = connection,
                UiLayoutDiagnostics = uiLayoutDiagnostics,
            };
            error = null;
            return true;
        }
    }
}
