// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class UnityAuthenticationWebClient : IAuthenticationWebClient
    {
        private readonly ClientSettings m_settings;

        public UnityAuthenticationWebClient(ClientSettings settings) => m_settings = settings;

        public Task<AuthenticationResult> LoginAsync(
            string login,
            string password,
            bool remember,
            CancellationToken cancellation
        )
        {
            ImmLogger.LInfo<UnityAuthenticationWebClient>($"Remember: {remember}");
            return SendAsync(
                m_settings.loginUrl,
                LoginFields(login, password, remember),
                remember,
                false,
                cancellation
            );
        }

        public Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellation)
        {
            ImmLogger.LInfo<UnityAuthenticationWebClient>($"{refreshToken?.Length}");
            return SendAsync(
                m_settings.refreshUrl,
                new Dictionary<string, string> { ["refreshToken"] = refreshToken },
                true,
                false,
                cancellation
            );
        }

        public Task<AuthenticationResult> RefreshWebSessionAsync(
            ClientConnectionConfig connection,
            CancellationToken cancellation
        )
        {
            string url = m_settings.webSessionTokenUrl;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsSameOrigin(Application.absoluteURL, url))
                return Task.FromResult(
                    AuthenticationResult.Failure(
                        AuthenticationResult.ErrorCodes.NotConfigured,
                        "Website token endpoint must share the page origin."
                    )
                );
#endif
            return SendAsync(url, new Dictionary<string, string>(), false, false, cancellation, connection);
        }

        internal static bool IsSameOrigin(string pageUrl, string endpoint) =>
            Uri.TryCreate(pageUrl, UriKind.Absolute, out var page)
            && Uri.TryCreate(endpoint, UriKind.Absolute, out var api)
            && page.Scheme == api.Scheme
            && page.Host == api.Host
            && page.Port == api.Port;

        public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellation)
        {
            ImmLogger.LInfo<UnityAuthenticationWebClient>($"{refreshToken?.Length}");
            var result = await SendAsync(
                m_settings.revokeUrl,
                new Dictionary<string, string> { ["refreshToken"] = refreshToken },
                false,
                true,
                cancellation
            );
            return result.Error == AuthenticationResult.ErrorCodes.Revoked;
        }

        internal static Dictionary<string, string> LoginFields(string login, string password, bool remember) =>
            new Dictionary<string, string>
            {
                ["login"] = login,
                ["password"] = password,
                ["rememberMe"] = remember ? "1" : "0",
            };

        private static async Task<AuthenticationResult> SendAsync(
            string url,
            Dictionary<string, string> fields,
            bool expectSession,
            bool revoke,
            CancellationToken cancellation,
            ClientConnectionConfig webSessionConnection = null
        )
        {
            if (!ClientSettings.IsTrustedHttpsUrl(url))
            {
                fields.Clear();
                return AuthenticationResult.Failure(
                    AuthenticationResult.ErrorCodes.NotConfigured,
                    "missing configuration"
                );
            }

            using var request = CreateRequest(url, fields);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellation.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();

                ImmLogger.LDebug<AuthenticationResult>(() =>
                    $"Response code: {request.responseCode}, expectSession: {expectSession}"
                );

                if (request.responseCode >= 300 && request.responseCode < 400)
                    return AuthenticationResult.Failure(
                        AuthenticationResult.ErrorCodes.InvalidResponse,
                        $"response code: {request.responseCode}"
                    );
                if (
                    request.result
                    is UnityWebRequest.Result.ConnectionError
                        or UnityWebRequest.Result.DataProcessingError
                )
                    return AuthenticationResult.Failure(
                        AuthenticationResult.ErrorCodes.NetworkError,
                        $"request result: {request.result}"
                    );
                if (revoke)
                {
                    if (request.responseCode == 204)
                        return AuthenticationResult.Failure(AuthenticationResult.ErrorCodes.Revoked, "ok");

                    return AuthenticationResult.Failure(
                        AuthenticationResult.ErrorCodes.ServiceUnavailable,
                        $"[revoke] {request.responseCode}"
                    );
                }

                if (webSessionConnection != null)
                    return AuthenticationResult.ParseWebSession(
                        request.responseCode,
                        request.GetResponseHeader("Content-Type"),
                        request.downloadHandler.text,
                        webSessionConnection,
                        DateTimeOffset.UtcNow
                    );
                return AuthenticationResult.Parse(
                    request.responseCode,
                    request.GetResponseHeader("Content-Type"),
                    request.downloadHandler.text,
                    expectSession,
                    DateTimeOffset.UtcNow
                );
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
        }

        internal static UnityWebRequest CreateRequest(string url, Dictionary<string, string> fields)
        {
            var request = UnityWebRequest.Post(url, fields);
            fields.Clear();
            request.timeout = 15;
            request.redirectLimit = 0;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            return request;
        }
    }
}
