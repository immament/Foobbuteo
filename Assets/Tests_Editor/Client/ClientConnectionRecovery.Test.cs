// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Shared;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.Editor.Client
{
    public sealed class ClientConnectionRecoveryTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        private const string Token = "single-use-token";

        [Test]
        public void Recovery_WaitsForForegroundAndIdleTransportThenUsesCappedBackoff()
        {
            var recovery = new ClientConnectionRecovery(() => 1);
            recovery.Schedule(0);
            Assert.IsFalse(recovery.TryBegin(0, true, false), "A backgrounded client must wait for resume.");
            Assert.IsFalse(recovery.TryBegin(0, false, true), "Recovery must wait for shutdown or authentication.");
            double elapsed = 0;
            foreach (double delay in new double[] { 2, 4, 8, 16, 30, 30 })
            {
                Assert.IsTrue(recovery.TryBegin(elapsed, false, false), "A due retry must start once.");
                recovery.Schedule(elapsed);
                Assert.IsFalse(
                    recovery.TryBegin(elapsed + delay - 0.01, false, false),
                    $"Repeated disconnect callbacks must not shorten the {delay}-second delay."
                );
                elapsed += delay;
            }
            recovery.Cancel();
            Assert.IsFalse(recovery.TryBegin(elapsed, false, false), "Cancellation must stop scheduled retries.");
        }

        [TestCase(ClientDisconnectReason.Kicked, false)]
        [TestCase(ClientDisconnectReason.SessionReplaced, false)]
        [TestCase(ClientDisconnectReason.InvalidToken, false)]
        [TestCase("Unknown server rejection", false)]
        [TestCase(ClientDisconnectReason.ServiceUnavailable, true)]
        [TestCase(ClientDisconnectReason.ServerBusy, true)]
        [TestCase("", true)]
        public void DisconnectReason_OnlyTransientFailuresAllowRecovery(string reason, bool retry)
        {
            Assert.AreEqual(
                retry,
                ClientDisconnectReason.CanRetry(reason),
                $"Recovery policy must classify '{reason}' without relying on UI wording."
            );
        }

        [Test]
        public void WebSessionResponse_ReusesConnectionAndAcceptsOnlyFreshTokenAndExpiry()
        {
            var original = Connection();
            var result = WebsiteToken();
            Assert.IsTrue(result.Succeeded, "A valid token-only response must be accepted.");
            Assert.AreEqual(original.host, result.Connection.host, "The endpoint must retain the configured host.");
            Assert.AreEqual(original.port, result.Connection.port, "The endpoint must retain the configured port.");
            Assert.AreEqual(
                original.webSocketPath,
                result.Connection.webSocketPath,
                "The endpoint must retain the configured WebSocket path."
            );
            Assert.AreEqual(Token, result.Connection.token, "The new temporary token must reach the next connection.");
        }

        [TestCase("{}")]
        [TestCase("{\"token\":\"token\"}")]
        [TestCase("{\"token\":\"token\",\"tokenExpiresAtUtc\":\"2020-01-01T00:00:00Z\"}")]
        [TestCase("{\"token\":\"token\",\"tokenExpiresAtUtc\":\"2099-01-01T00:00:00Z\"}")]
        [TestCase("{\"token\":\"token\",\"tokenExpiresAtUtc\":\"2026-10-27T12:00:50Z\",\"host\":\"evil.test\"}")]
        [TestCase("{\"token\":\"first\",\"token\":\"second\",\"tokenExpiresAtUtc\":\"2026-10-27T12:00:50Z\"}")]
        public void WebSessionResponse_RejectsMalformedExpiredOrUnexpectedData(string json)
        {
            var result = AuthenticationResult.ParseWebSession(200, "application/json", json, Connection(), Now);
            Assert.IsFalse(result.Succeeded, "Untrusted response data must not create a game connection.");
        }

        [TestCase("https://www.footstar.org/game", "https://www.footstar.org/api/token", true)]
        [TestCase("https://www.footstar.org/game", "https://nd.footstar.org/api/token", false)]
        [TestCase("https://www.footstar.org/game", "http://www.footstar.org/api/token", false)]
        [TestCase("https://www.footstar.org/game", "https://www.footstar.org:444/api/token", false)]
        public void WebSessionEndpoint_RequiresPageOrigin(string page, string endpoint, bool sameOrigin)
        {
            Assert.AreEqual(
                sameOrigin,
                UnityAuthenticationWebClient.IsSameOrigin(page, endpoint),
                "Cookie-based recovery must match scheme, hostname, and port."
            );
        }

        [Test]
        public async Task ExternalAuthentication_RequestsFreshTokensForEveryRecovery()
        {
            int requests = 0;
            using var auth = new ClientAuthenticationCoordinator(
                new FakeWeb(),
                new EmptyRememberedSessionStore(),
                () => Now,
                (connection, _) =>
                {
                    requests++;
                    Assert.IsNull(connection.token, "The consumed token must never be retained for reuse.");
                    return Task.FromResult(WebsiteToken());
                }
            );
            var handedOffTokens = new List<string>();
            auth.ConnectionReady += connection =>
            {
                handedOffTokens.Add(connection.token);
                connection.token = null;
            };
            auth.AcceptExternalConfig(Connection());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                auth.UpdateConnectionState(false, false);
                await auth.RecoverAsync();
            }
            Assert.AreEqual(2, requests, "Every recovery must request a fresh website token.");
            Assert.AreEqual(
                3,
                handedOffTokens.Count,
                "Initial login and both recoveries must start one connection each."
            );
            Assert.IsFalse(auth.RequiresLogin, "Transient transport loss must not require password login.");
        }

        [Test]
        public async Task PasswordAuthentication_WithoutRememberingNeverUsesWebsiteRecovery()
        {
            int websiteRequests = 0;
            using var auth = new ClientAuthenticationCoordinator(
                new FakeWeb(),
                new EmptyRememberedSessionStore(),
                () => Now,
                (_, _) =>
                {
                    websiteRequests++;
                    return Task.FromResult(WebsiteToken());
                }
            );
            await auth.LoginAsync("player", "password", false);
            auth.UpdateConnectionState(true, false);
            auth.UpdateConnectionState(false, false);
            await auth.RecoverAsync();
            Assert.IsFalse(auth.CanRecover, "WebGL form login must not enable automatic recovery.");
            Assert.IsFalse(
                auth.RequiresLogin,
                "Transport loss alone must preserve the displayed authenticated session."
            );
            Assert.AreEqual(0, websiteRequests, "Form login must never use the surrounding website session.");
        }

        [TestCase(AuthenticationResult.ErrorCodes.InvalidSession, true)]
        [TestCase(AuthenticationResult.ErrorCodes.NetworkError, false)]
        public async Task ExternalRecovery_OnlyInvalidWebsiteSessionRequiresLogin(string error, bool requiresLogin)
        {
            using var auth = new ClientAuthenticationCoordinator(
                new FakeWeb(),
                new EmptyRememberedSessionStore(),
                () => Now,
                (_, _) => Task.FromResult(AuthenticationResult.Failure(error, "test response"))
            );
            auth.AcceptExternalConfig(Connection());
            auth.UpdateConnectionState(false, false);
            await auth.RecoverAsync();
            Assert.AreEqual(
                requiresLogin,
                auth.RequiresLogin,
                "Only definitive loss of website authentication must display login."
            );
            Assert.AreEqual(
                !requiresLogin,
                auth.CanRecover,
                "Transient failures must preserve the external recovery source."
            );
        }

        [Test]
        public async Task Logout_DiscardsLateWebsiteToken()
        {
            var pending = new TaskCompletionSource<AuthenticationResult>();
            using var auth = new ClientAuthenticationCoordinator(
                new FakeWeb(),
                new EmptyRememberedSessionStore(),
                () => Now,
                (_, _) => pending.Task
            );
            int connections = 0;
            auth.ConnectionReady += _ => connections++;
            auth.AcceptExternalConfig(Connection());
            auth.UpdateConnectionState(false, false);
            Task recovering = auth.RecoverAsync();
            await auth.LogoutAsync();
            var lateResponse = WebsiteToken();
            pending.SetResult(lateResponse);
            await recovering;
            Assert.AreEqual(1, connections, "Logout must prevent a late response from reconnecting.");
            Assert.IsNull(lateResponse.Connection.token, "A late temporary token must be cleared.");
        }

        private static ClientConnectionConfig Connection() =>
            new()
            {
                token = Token,
                host = "game.example.test",
                port = 443,
                webSocketPath = "/game",
                useEncryption = true,
            };

        private static AuthenticationResult WebsiteToken() =>
            AuthenticationResult.ParseWebSession(
                200,
                "application/json",
                new JObject
                {
                    ["token"] = Token,
                    ["tokenExpiresAtUtc"] = Now.AddSeconds(50).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                }.ToString(),
                Connection(),
                Now
            );

        private sealed class FakeWeb : IAuthenticationWebClient
        {
            public Task<AuthenticationResult> LoginAsync(
                string login,
                string password,
                bool remember,
                CancellationToken cancellation
            ) => Task.FromResult(WebsiteToken());

            public Task<AuthenticationResult> RefreshTokenAsync(string token, CancellationToken cancellation) =>
                throw new InvalidOperationException("This fixture has no remembered Android session.");

            public Task<bool> RevokeAsync(string token, CancellationToken cancellation) => Task.FromResult(true);
        }
    }
}
