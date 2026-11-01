// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Globalization;
using System.IO;
using Imma.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class AuthenticationResult
    {
        // Allow subsecond server/client clock differences when checking the advertised maximum lifetime.
        // This does not extend the returned expiry or relax checks for an already expired session.
        private static readonly TimeSpan s_SessionLifetimeClockTolerance = TimeSpan.FromSeconds(1);

        private const int k_MaxResponseJsonLength = 16_384;
        private const int k_LoginTokenExpiryInSeconds = 61;
        private const int k_RefreshTokenExipryInDays = 90;

        public static class ErrorCodes
        {
            public const string InvalidSession = "invalid_session";
            public const string InvalidResponse = "invalid_response";
            public const string InvalidCredentials = "invalid_credentials";
            public const string RateLimited = "rate_limited";
            public const string ServiceUnavailable = "service_unavailable";
            public const string NotConfigured = "not_configured";
            public const string NetworkError = "network_error";
            public const string Revoked = "revoked";
        }

        public ClientConnectionConfig Connection { get; private set; }
        public RememberedSession Session { get; private set; }
        public string Error { get; private set; }
        public string ErrorDetails { get; private set; }
        public bool Succeeded => Connection != null;
        public bool IsInvalidSession => Error == ErrorCodes.InvalidSession;

        public static AuthenticationResult Failure(string error, string errorDetails)
        {
            ImmLogger.LWarn<AuthenticationResult>($"{error} - {errorDetails}");
            return new AuthenticationResult { Error = error, ErrorDetails = errorDetails };
        }

        public static AuthenticationResult InvalidResponse(string errorDetails)
        {
            ImmLogger.LWarn<AuthenticationResult>($"{ErrorCodes.InvalidResponse} - {errorDetails}");
            return new AuthenticationResult { Error = ErrorCodes.InvalidResponse, ErrorDetails = errorDetails };
        }

        internal static AuthenticationResult ParseWebSession(
            long status,
            string contentType,
            string json,
            ClientConnectionConfig connection,
            DateTimeOffset now
        )
        {
            if (status != 200)
                return Parse(status, contentType, json, false, now);
            if (
                string.IsNullOrEmpty(contentType)
                || !contentType.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(json)
                || json.Length > k_MaxResponseJsonLength
            )
                return InvalidResponse("Invalid website token response.");
            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
                var data = JObject.Load(
                    reader,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }
                );
                if (
                    reader.Read()
                    || data.Count != 2
                    || data["token"]?.Type != JTokenType.String
                    || data["tokenExpiresAtUtc"]?.Type != JTokenType.String
                )
                    return InvalidResponse("Expected only token and tokenExpiresAtUtc.");
                var full = JObject.FromObject(connection.CopyConnection());
                full["token"] = data["token"];
                full["expiresAtUtc"] = data["tokenExpiresAtUtc"];
                return Parse(200, contentType, full.ToString(), false, now);
            }
            catch (JsonException)
            {
                return InvalidResponse("Invalid website token JSON.");
            }
        }

        public static AuthenticationResult Parse(
            long status,
            string contentType,
            string json,
            bool expectSession,
            DateTimeOffset now
        )
        {
            if (status is >= 300 and < 400)
                return InvalidResponse($"Incorrect response status: {status}");

            if (string.IsNullOrEmpty(contentType))
                return InvalidResponse("Empty content type");

            if (!contentType.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
                return InvalidResponse("Invalid content type");

            if (string.IsNullOrWhiteSpace(json))
                return InvalidResponse("Empty response JSON");

            if (json.Length > k_MaxResponseJsonLength)
                return InvalidResponse("Response JSON is too long");

            JObject data;
            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
                data = JObject.Load(
                    reader,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }
                );
                if (reader.Read())
                    return InvalidResponse("has more data then expected");
            }
            catch (JsonException ex)
            {
                return InvalidResponse($"Json parse exception {ex.Message}");
            }

            if (status != 200)
            {
                string error = data["error"]?.Type == JTokenType.String ? (string)data["error"] : null;
                switch (status)
                {
                    case 401 when (error is ErrorCodes.InvalidCredentials or ErrorCodes.InvalidSession):
                    case 429 when error == ErrorCodes.RateLimited:
                    case 503 when error == ErrorCodes.ServiceUnavailable:
                        return Failure(error, $"Error from https backend, status: {status}");
                    default:
                        return Failure(ErrorCodes.ServiceUnavailable, "Https backend");
                }
            }
            AuthenticationResult failure = TryParseConnection(json, now, data, out ClientConnectionConfig connection);
            if (failure != null)
                return failure;

            RememberedSession session = null;
            if (expectSession)
            {
                if (data["refreshToken"]?.Type != JTokenType.String)
                    return InvalidResponse("Invalid refreshToken type");

                if (data["refreshExpiresAtUtc"]?.Type != JTokenType.String)
                    return InvalidResponse("Invalid refreshExpiresAtUtc type");

                session = new RememberedSession
                {
                    refreshToken = (string)data["refreshToken"],
                    refreshExpiresAtUtc = (string)data["refreshExpiresAtUtc"],
                };
                if (!session.IsValid(now))
                    return InvalidResponse("Invalid session");

                if (!TryUtcDate(session.refreshExpiresAtUtc, out var sessionExpiry))
                    return InvalidResponse("Invalid refreshExpiresAtUtc");

                if (sessionExpiry > now.AddDays(k_RefreshTokenExipryInDays).Add(s_SessionLifetimeClockTolerance))
                    return Failure(
                        ErrorCodes.InvalidResponse,
                        $"Remembered session lifetime exceeds {k_RefreshTokenExipryInDays} days"
                    );
            }
            else
            {
                if (data["refreshToken"] != null || data["refreshExpiresAtUtc"] != null)
                    return InvalidResponse("Unexpected session data");
            }

            return new AuthenticationResult { Connection = connection, Session = session };
        }

        private static AuthenticationResult TryParseConnection(
            string json,
            DateTimeOffset now,
            JObject data,
            out ClientConnectionConfig connection
        )
        {
            connection = null;
            if (data["useEncryption"]?.Type != JTokenType.Boolean)
                return InvalidResponse("useEncryption invalid type");

            // if (!Application.isEditor || !Application.isPlaying)
            // {
            //     if (!(bool)data["useEncryption"])
            //         return InvalidResponse("Encryption required");
            // }

            if (data["expiresAtUtc"]?.Type != JTokenType.String)
                return InvalidResponse("Invalid expiresAtUtc type");

            if (!ClientConnectionConfig.TryParse(json, out connection, out string jsonParseError))
                return InvalidResponse($"Problem during parse connection config json: {jsonParseError}");

            if (!IsValidSecret(connection.token))
                return InvalidResponse("!IsValidSecret(token)");

            if (!TryUtcDate((string)data["expiresAtUtc"], out var expiry))
                return InvalidResponse("ExpiresAtUtc parse problem");

            if (expiry <= now)
                return InvalidResponse("Already expired");

            if (expiry > now.AddSeconds(k_LoginTokenExpiryInSeconds))
                return InvalidResponse($"Token expiration exceeded {k_LoginTokenExpiryInSeconds} seconds");

            connection.expiresAtUtc = expiry;
            return null;
        }

        internal static bool TryUtcDate(string value, out DateTimeOffset date)
        {
            date = default;
            return DateTimeOffset.TryParseExact(
                    value,
                    new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out date
                )
                && date.Offset == TimeSpan.Zero;
        }

        internal static bool IsValidSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                return false;
            foreach (char c in value)
                if (char.IsControl(c) || char.IsWhiteSpace(c))
                    return false;
            return true;
        }

        public void ClearSecrets()
        {
            if (Connection != null)
                Connection.token = null;
            Session?.ClearSecret();
        }
    }
}
