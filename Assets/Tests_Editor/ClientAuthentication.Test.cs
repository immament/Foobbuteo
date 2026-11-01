// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Auth;
using Imma.Foobbuteo.Client.Lobby;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Lobby.Contract;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Tests.Editor.Utils;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class ClientAuthenticationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        private static string Response(bool remember = false, string refresh = "new-refresh", int days = 90)
        {
            var data = new JObject
            {
                ["token"] = "one-time-game-token",
                ["host"] = "game.example.test",
                ["port"] = 443,
                ["webSocketPath"] = "/game",
                ["useEncryption"] = true,
                ["expiresAtUtc"] = Now.AddSeconds(50).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            if (remember)
            {
                data["refreshToken"] = refresh;
                data["refreshExpiresAtUtc"] = Now.AddDays(days).ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
            return data.ToString();
        }

        private static AuthenticationResult Success(
            bool remember = false,
            string refresh = "new-refresh",
            int days = 90
        ) =>
            AuthenticationResult.Parse(
                200,
                "application/json; charset=utf-8",
                Response(remember, refresh, days),
                remember,
                Now
            );

        [Test]
        public void FormEncoding_PreservesSpecialCharactersAndUnicode()
        {
            var fields = UnityAuthenticationWebClient.LoginFields("user+&= żółć", "pa&ss=+ %漢字", true);
            using var request = UnityWebRequest.Post("https://example.test/login", fields);
            var decoded = new Dictionary<string, string>();
            foreach (string pair in Encoding.UTF8.GetString(request.uploadHandler.data).Split('&'))
            {
                string[] parts = pair.Split('=');
                decoded[Uri.UnescapeDataString(parts[0].Replace("+", " "))] = Uri.UnescapeDataString(
                    parts[1].Replace("+", " ")
                );
            }
            Assert.AreEqual(
                fields["login"],
                decoded["login"],
                "Form encoding must preserve special characters and Unicode in the login."
            );
            Assert.AreEqual(
                fields["password"],
                decoded["password"],
                "Form encoding must preserve special characters and Unicode in the password."
            );
            Assert.AreEqual("1", decoded["rememberMe"], "Selecting Remember Me must enable it in the encoded form.");
            Assert.AreEqual(
                "0",
                UnityAuthenticationWebClient.LoginFields("u", "p", false)["rememberMe"],
                "Clearing Remember Me must disable it in the submitted form."
            );
        }

        [TestCase("http://example.test/login")]
        [TestCase("https://user:password@example.test/login")]
        [TestCase("https://example.test/login?password=secret")]
        [TestCase("https://example.test/login#fragment")]
        [TestCase("")]
        public void EndpointValidation_RejectsUnsafeEndpoints(string url) =>
            Assert.IsFalse(
                ClientSettings.IsTrustedHttpsUrl(url),
                "Unsafe authentication endpoints must be rejected before sending credentials."
            );

        [Test]
        public async Task UnconfiguredEndpoint_FailsWithoutSendingCredentials()
        {
            var web = new UnityAuthenticationWebClient(new ClientSettings());
            Assert.AreEqual(
                AuthenticationResult.ErrorCodes.NotConfigured,
                (await web.LoginAsync("user", "password", false, CancellationToken.None)).Error,
                "Login without an endpoint must report missing configuration."
            );
        }

        [TestCase(401, AuthenticationResult.ErrorCodes.InvalidCredentials)]
        [TestCase(302, AuthenticationResult.ErrorCodes.InvalidResponse)]
        [TestCase(401, AuthenticationResult.ErrorCodes.InvalidSession)]
        [TestCase(429, AuthenticationResult.ErrorCodes.RateLimited)]
        [TestCase(503, AuthenticationResult.ErrorCodes.ServiceUnavailable)]
        public void ResponseParsing_MapsKnownHttpErrors(int status, string error)
        {
            var result = AuthenticationResult.Parse(
                status,
                "application/json",
                "{\"error\":\"" + error + "\"}",
                false,
                Now
            );
            Assert.IsFalse(result.Succeeded, "An HTTP error response must not authenticate the user.");
            Assert.AreEqual(error, result.Error, "Known HTTP errors must retain their authentication error code.");
        }

        [TestCase("text/html", "<html>Login</html>")]
        [TestCase("application/json", "[]")]
        [TestCase("application/json", "not json")]
        [TestCase("application/json", "{}")]
        public void ResponseParsing_RejectsInvalidBodies(string contentType, string body) =>
            Assert.AreEqual(
                AuthenticationResult.ErrorCodes.InvalidResponse,
                AuthenticationResult.Parse(200, contentType, body, false, Now).Error,
                "Malformed or non-JSON response bodies must report an invalid response."
            );

        // [TestCase("useEncryption", "false")]
        [TestCase("expiresAtUtc", "{}")]
        [TestCase("host", "\"https://bad.test\"")]
        [TestCase("port", "70000")]
        [TestCase("token", "\"\"")]
        public void ResponseParsing_RejectsInvalidConnectionFields(string field, string value)
        {
            JObject data = JObject.Parse(Response());
            data[field] = JToken.Parse(value);
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", data.ToString(), false, Now).Succeeded,
                "Invalid connection fields must prevent authentication success."
            );
        }

        [Test]
        public void ResponseParsing_EnforcesTokenAndSessionExpiryAndRememberContract()
        {
            Assert.IsTrue(
                Success().Succeeded,
                "A valid response without remembered-session data must authenticate successfully."
            );
            Assert.IsTrue(
                Success(remember: true).Succeeded,
                "A valid response with a supported remembered-session lifetime must authenticate successfully."
            );
            Assert.IsFalse(Success(remember: true, days: 91).Succeeded, "Remembered session lifetime exceeds 90 days");
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", Response(remember: true), false, Now).Succeeded,
                "Unexpected session data"
            );
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", Response(), true, Now).Succeeded,
                "Invalid refreshToken type"
            );
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", Response(), false, Now.AddSeconds(51)).Succeeded,
                "Already expired"
            );
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", Response(), false, Now.AddSeconds(-12)).Succeeded,
                $"Token expiration exceeded 61 seconds"
            );
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(999, true)]
        [TestCase(1000, true)]
        [TestCase(1001, false)]
        public void ResponseParsing_RememberedLifetimeAllowsAtMostOneSecondClockDifference(
            int millisecondsBeyondMaximum,
            bool expectedSuccess
        )
        {
            // Arrange
            var returnedExpiry = Now.AddDays(90).AddMilliseconds(millisecondsBeyondMaximum);
            var data = JObject.Parse(Response(remember: true));
            data["refreshExpiresAtUtc"] = returnedExpiry.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

            // Act
            var result = AuthenticationResult.Parse(200, "application/json", data.ToString(), true, Now);

            // Assert
            Assert.AreEqual(
                expectedSuccess,
                result.Succeeded,
                $"A {millisecondsBeyondMaximum} ms clock difference must respect the one-second tolerance."
            );
            if (expectedSuccess)
                Assert.AreEqual(
                    (string)data["refreshExpiresAtUtc"],
                    result.Session.refreshExpiresAtUtc,
                    "Clock tolerance must not modify the expiry returned by the website."
                );
            else
                Assert.AreEqual(
                    AuthenticationResult.ErrorCodes.InvalidResponse,
                    result.Error,
                    "A remembered lifetime exceeding clock tolerance must report an invalid response."
                );
        }

        [Test]
        public void ResponseParsing_AlreadyExpiredRememberedSessionIsRejectedDespiteClockTolerance()
        {
            // Arrange
            var data = JObject.Parse(Response(remember: true));
            data["refreshExpiresAtUtc"] = Now.AddMilliseconds(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

            // Act
            var result = AuthenticationResult.Parse(200, "application/json", data.ToString(), true, Now);

            // Assert
            Assert.AreEqual(
                AuthenticationResult.ErrorCodes.InvalidResponse,
                result.Error,
                "Tolerance for the maximum lifetime must not allow an expired remembered session."
            );
        }

        [Test]
        public void Request_DisablesRedirectsUsesTimeoutAndClearsFormReferences()
        {
            var fields = UnityAuthenticationWebClient.LoginFields("user", "password", false);
            using var request = UnityAuthenticationWebClient.CreateRequest("https://example.test/login", fields);
            Assert.AreEqual(15, request.timeout, "Authentication requests must time out after fifteen seconds.");
            Assert.AreEqual(
                0,
                request.redirectLimit,
                "Authentication requests must not follow redirects that could expose credentials."
            );
            Assert.IsNull(
                request.certificateHandler,
                "Authentication requests must use the default certificate validation."
            );
            Assert.That(request.GetRequestHeader("Content-Type"), Does.StartWith("application/x-www-form-urlencoded"));
            Assert.IsEmpty(fields, "Request creation must release the caller's form references to credentials.");
            Assert.IsFalse(
                AuthenticationResult.Parse(200, "application/json", Response() + "{}", false, Now).Succeeded,
                "Trailing JSON objects must prevent authentication success."
            );
        }

        [Test]
        public void NetworkStartupFailure_ErasesTokenAndPayload()
        {
            var obj = new GameObject("authentication startup failure test");
            ClientNetCtrl controller = null;
            try
            {
                var manager = obj.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                controller = new ClientNetCtrl(manager);
                var config = Success().Connection;
                config.expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(50);
                controller.SetClientConfig(config);
                Assert.Throws<NullReferenceException>(() => controller.StartClient());
                Assert.IsNull(config.token, "Failed network startup must erase the one-time game token.");
                Assert.IsEmpty(
                    manager.NetworkConfig.ConnectionData,
                    "Failed network startup must clear the connection payload."
                );
                Assert.IsFalse(controller.IsConnecting, "Failed network startup must release the connecting state.");
            }
            finally
            {
                controller?.UnregisterNetworkEvents();
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public async Task Login_RememberedSessionIsSavedBeforeConnection()
        {
            var store = new FakeStore();
            var web = new FakeWeb { Result = Success(true) };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            coordinator.ConnectionReady += config =>
            {
                Assert.AreEqual(
                    "new-refresh",
                    store.Saved.refreshToken,
                    "The remembered session must be persisted before announcing connection readiness."
                );
                Assert.IsTrue(config.HasUsableToken(Now), "Connection readiness must provide an unexpired game token.");
            };
            await coordinator.LoginAsync("footstar", "password", true);
            Assert.IsTrue(
                coordinator.HasRememberedSession,
                "Successful remembered login must expose the saved session for future refresh."
            );
            Assert.IsTrue(
                web.RememberRequested,
                "Supported platforms must forward the Remember Me selection to authentication."
            );
            Assert.IsNull(
                web.Result.Session.refreshToken,
                "Persisting the remembered session must erase the response's refresh-token reference."
            );
        }

        [Test]
        public async Task UnsupportedPlatform_NeverRequestsRememberMe()
        {
            var web = new FakeWeb { Result = Success() };
            using var coordinator = new ClientAuthenticationCoordinator(
                web,
                new FakeStore { IsSupported = false },
                () => Now
            );
            await coordinator.LoginAsync("u", "p", true);
            Assert.IsFalse(
                web.RememberRequested,
                "Unsupported storage must suppress Remember Me in the authentication request."
            );
            Assert.IsFalse(
                coordinator.HasRememberedSession,
                "Unsupported storage must not expose a remembered session."
            );
        }

        [Test]
        public async Task Refresh_RotatesSecretWithoutExtendingExpiry()
        {
            var store = RememberedStore();
            var web = new FakeWeb { Result = Success(true, days: 45) };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            await coordinator.RefreshAsync();
            Assert.AreEqual(
                1,
                web.RefreshCalls,
                "Refreshing a remembered session must issue exactly one refresh request."
            );
            Assert.AreEqual(
                "old-refresh",
                web.RefreshRequested,
                "Refreshing must submit the existing remembered-session secret."
            );
            Assert.AreEqual(0, web.LoginCalls, "Refreshing must not submit password credentials.");
            Assert.IsNull(web.Revoked, "Token rotation must not revoke the previous refresh token separately.");
            Assert.AreEqual(
                "new-refresh",
                store.Saved.refreshToken,
                "Successful refresh must persist the rotated secret."
            );
            Assert.AreEqual(
                Now.AddDays(45).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                store.Saved.refreshExpiresAtUtc,
                "Token rotation must preserve the original session expiry."
            );
        }

        [TestCase("old-refresh", 45)]
        [TestCase("new-refresh", 46)]
        public async Task Refresh_RejectsReusedSecretOrExtendedExpiry(string token, int days)
        {
            var store = RememberedStore();
            var web = new FakeWeb { Result = Success(true, token, days) };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            int connections = 0;
            coordinator.ConnectionReady += _ => connections++;
            await coordinator.RefreshAsync();
            Assert.AreEqual(
                0,
                connections,
                "Reused refresh secrets or extended expiry must prevent a game connection."
            );
            Assert.IsFalse(
                coordinator.HasRememberedSession,
                "Rejected token rotation must remove the remembered session."
            );
        }

        [TestCase(AuthenticationResult.ErrorCodes.NetworkError, true)]
        [TestCase(AuthenticationResult.ErrorCodes.ServiceUnavailable, true)]
        [TestCase(AuthenticationResult.ErrorCodes.RateLimited, true)]
        [TestCase(AuthenticationResult.ErrorCodes.InvalidSession, false)]
        public async Task Refresh_TransientFailuresRetainSessionAndInvalidSessionDeletesIt(string error, bool retained)
        {
            var store = RememberedStore();
            var web = new FakeWeb { Result = AuthenticationResult.Failure(error, "") };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            await coordinator.RefreshAsync();
            Assert.AreEqual(
                retained,
                coordinator.HasRememberedSession,
                "Transient refresh errors must retain the session; an invalid session must remove it."
            );
            Assert.AreEqual(
                retained,
                store.Saved != null,
                "Stored session retention must follow the refresh error's recovery policy."
            );
            Assert.AreEqual(1, web.RefreshCalls, "Refresh failure must not automatically retry the request.");
            Assert.IsFalse(coordinator.IsAuthenticating, "Refresh failure must release the authentication state.");
        }

        [Test]
        public void Load_ExpiredOrUnreadableStorageIsDeleted()
        {
            var store = RememberedStore();
            store.Saved.refreshExpiresAtUtc = Now.AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
            using (var coordinator = new ClientAuthenticationCoordinator(new FakeWeb(), store, () => Now))
                Assert.IsFalse(
                    coordinator.HasRememberedSession,
                    "An expired stored session must not be offered for remembered login."
                );
            Assert.IsNull(store.Saved, "Loading an expired remembered session must delete it from storage.");
            store.ThrowLoad = true;
            using var unreadable = new ClientAuthenticationCoordinator(new FakeWeb(), store, () => Now);
            Assert.IsFalse(unreadable.HasRememberedSession, "Unreadable storage must not expose a remembered session.");
            StringAssert.Contains("could not be read", unreadable.Message);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task SaveFailure_ClearsSessionRevokesIssuedSecretAndStillConnects(bool refreshing)
        {
            // Arrange
            var store = RememberedStore();
            store.ThrowSave = true;
            var web = new FakeWeb { Result = Success(true, days: 45) };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            int connections = 0;
            coordinator.ConnectionReady += _ => connections++;
            // Act
            if (refreshing)
                await coordinator.RefreshAsync();
            else
                await coordinator.LoginAsync("u", "p", true);

            // Assert
            Assert.AreEqual(
                1,
                connections,
                "Session storage failure must still allow the authenticated game connection."
            );
            Assert.IsNull(store.Saved, "Session storage failure must clear the local remembered session.");
            Assert.IsFalse(coordinator.HasRememberedSession, "Session storage failure must disable remembered login.");
            Assert.AreEqual(
                "new-refresh",
                web.Revoked,
                "Session storage failure must revoke the newly issued refresh secret."
            );
            StringAssert.Contains("unavailable", coordinator.Message);
        }

        [Test]
        public async Task PendingLogin_RejectsDuplicatesAndExternalTokenThenAllowsExternalAfterFailure()
        {
            var web = new FakeWeb { Pending = new TaskCompletionSource<AuthenticationResult>() };
            using var coordinator = new ClientAuthenticationCoordinator(web, new FakeStore(), () => Now);
            Task first = coordinator.LoginAsync("u", "p", false);
            await coordinator.LoginAsync("u", "p", false);
            var rejected = Success().Connection;
            Assert.IsFalse(
                coordinator.AcceptExternalConfig(rejected),
                "A pending password login must reject externally supplied connection settings."
            );
            Assert.IsNull(rejected.token, "Rejected external settings must have their game token erased.");
            Assert.AreEqual(
                1,
                web.LoginCalls,
                "Duplicate submissions while login is pending must not issue another request."
            );
            web.Pending.SetResult(AuthenticationResult.Failure(AuthenticationResult.ErrorCodes.InvalidCredentials, ""));
            await first;
            Assert.IsTrue(
                coordinator.AcceptExternalConfig(Success().Connection),
                "After password login fails, valid external settings must be accepted."
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Logout_CancelsPendingAuthenticationAndLateSuccessCannotReconnectOrSave(bool refreshing)
        {
            // Arrange
            var store = RememberedStore();
            var web = new FakeWeb { Pending = new TaskCompletionSource<AuthenticationResult>() };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            int connections = 0;
            coordinator.ConnectionReady += _ => connections++;
            // Act
            Task authentication = refreshing ? coordinator.RefreshAsync() : coordinator.LoginAsync("u", "p", true);
            await coordinator.LogoutAsync();
            Assert.IsTrue(
                web.Cancellation.IsCancellationRequested,
                "Logout must request cancellation of pending authentication."
            );
            var late = Success(true, days: 45);
            web.Pending.SetResult(late);
            await authentication;

            // Assert
            Assert.AreEqual(0, connections, "Authentication completing after logout must not reconnect.");
            Assert.IsNull(store.Saved, "Authentication completing after logout must not persist a session.");
            Assert.IsNull(late.Connection.token, "A late authentication response must have its game token erased.");
            Assert.IsNull(
                late.Session.refreshToken,
                "A late authentication response must have its refresh-token reference erased."
            );
            Assert.AreEqual(
                "new-refresh",
                web.Revoked,
                "Logout must revoke a refresh secret issued by a late response."
            );
        }

        [Test]
        public async Task Logout_ClearsLocalSessionEvenIfRevocationFails()
        {
            var store = RememberedStore();
            var web = new FakeWeb { RevokeSuccess = false };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            await coordinator.LogoutAsync();
            Assert.IsNull(store.Saved, "Logout must clear local storage even when remote revocation fails.");
            Assert.AreEqual(
                "old-refresh",
                web.Revoked,
                "Logout must attempt to revoke the existing remembered-session secret."
            );
            StringAssert.Contains("could not be confirmed", coordinator.Message);
        }

        [Test]
        public async Task PasswordLogin_WithRememberMeRevokesPreviousSessionAfterSavingReplacement()
        {
            var store = RememberedStore();
            var web = new FakeWeb { Result = Success(true) };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            await coordinator.LoginAsync("u", "p", true);
            Assert.AreEqual(
                "new-refresh",
                store.Saved.refreshToken,
                "Remembered password login must persist the replacement session."
            );
            Assert.AreEqual(
                "old-refresh",
                web.Revoked,
                "Remembered password login must revoke the previous session after replacement."
            );
        }

        [Test]
        public async Task PasswordLogin_WithoutRememberMeClearsAndRevokesPreviousSession()
        {
            // Arrange
            var store = RememberedStore();
            var web = new FakeWeb { Result = Success() };
            using var coordinator = new ClientAuthenticationCoordinator(web, store, () => Now);
            ClientConnectionConfig connection = null;
            coordinator.ConnectionReady += config => connection = config;

            // Act
            await coordinator.LoginAsync("u", "p", false);

            // Assert
            Assert.IsNull(store.Saved, "Password login without Remember Me must delete the previous stored session.");
            Assert.IsFalse(
                coordinator.HasRememberedSession,
                "Password login without Remember Me must disable remembered login."
            );
            Assert.AreEqual(
                "old-refresh",
                web.Revoked,
                "Password login without Remember Me must revoke the previous session."
            );
            Assert.AreEqual(0, web.RefreshCalls, "Password login must not use the remembered-session endpoint.");
            Assert.IsNotNull(connection, "Removing Remember Me must still allow the game connection.");
            Assert.IsTrue(
                connection.HasUsableToken(Now),
                "Password login without Remember Me must still supply an unexpired game token."
            );
        }

        [TestCase("OnClientConnected")]
        [TestCase("OnClientDisconnect")]
        [TestCase("OnTransportFailure")]
        [TestCase("OnPreShutdown")]
        [TestCase("OnClientStopped")]
        public void NetworkLifecycle_ClearsTokenAndPayloadButKeepsEndpoint(string callback)
        {
            var previousClient = AppCtx.Client;
            var obj = new GameObject("authentication lifecycle test");
            ClientNetCtrl controller = null;
            try
            {
                AppCtx.Client = new FakeClient();
                var manager = obj.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                controller = new ClientNetCtrl(manager);
                var config = Success().Connection;
                controller.SetClientConfig(config);
                byte[] oldPayload = manager.NetworkConfig.ConnectionData;
                object[] args =
                    callback == "OnClientConnected" || callback == "OnClientDisconnect" ? new object[] { 0UL }
                    : callback == "OnClientStopped" ? new object[] { false }
                    : null;
                typeof(ClientNetCtrl)
                    .GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, args);
                Assert.IsNull(config.token, "Network lifecycle cleanup must erase the one-time game token.");
                Assert.AreEqual(
                    "game.example.test",
                    config.host,
                    "Network lifecycle cleanup must preserve the configured game endpoint."
                );
                Assert.IsEmpty(
                    manager.NetworkConfig.ConnectionData,
                    "Network lifecycle cleanup must discard the connection payload."
                );
                Assert.That(oldPayload, Is.All.EqualTo(0));
            }
            finally
            {
                controller?.UnregisterNetworkEvents();
                UnityEngine.Object.DestroyImmediate(obj);
                AppCtx.Client = previousClient;
            }
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        public void NetworkDisconnect_WithoutServerReasonDoesNotSpamToasts(bool logout, int expectedToasts)
        {
            var previousClient = AppCtx.Client;
            var obj = new GameObject("disconnect notification test");
            ClientNetCtrl controller = null;
            try
            {
                var client = new FakeClient();
                AppCtx.Client = client;
                var manager = obj.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                controller = new ClientNetCtrl(manager);
                controller.SetClientConfig(Success().Connection);

                if (logout)
                    controller.Logout();
                typeof(ClientNetCtrl)
                    .GetMethod("OnClientDisconnect", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { 0UL });

                Assert.AreEqual(
                    expectedToasts,
                    client.ToastCalls,
                    $"A disconnect with explicit logout={logout} must show {expectedToasts} connection problem toasts."
                );
                Assert.IsFalse(
                    controller.HasUsableToken,
                    "Disconnect must clear the single-use game token in both cases."
                );
            }
            finally
            {
                controller?.UnregisterNetworkEvents();
                UnityEngine.Object.DestroyImmediate(obj);
                AppCtx.Client = previousClient;
            }
        }

        [Test]
        public void LoginVM_NotifiesOnlyChangedPropertiesAndTracksClientState()
        {
            var client = new FakeClient();
            using var vm = new LoginVM(client);
            var changed = new List<string>();
            vm.propertyChanged += (_, evt) => changed.Add(evt.propertyName.ToString());
            vm.Login = "player";
            vm.Password = "secret";
            vm.Remember = true;
            CollectionAssert.AreEqual(new[] { "Login", "Password", "Remember" }, changed);
            changed.Clear();
            vm.Login = "player";
            vm.Password = "secret";
            vm.Remember = true;
            client.Notify();
            Assert.IsEmpty(changed, "Assigning unchanged login values must not publish property changes.");

            client.CanRememberLogin = true;
            client.HasRememberedLogin = true;
            client.IsAuthenticating = true;
            client.AuthenticationMessage = "Authenticating...";
            client.Notify();
            Assert.IsFalse(vm.InputsEnabled, "Authentication in progress must disable login inputs.");
            Assert.IsEmpty(vm.Password, "Authentication in progress must erase the view model's password.");
            Assert.IsTrue(
                vm.CanRememberLogin && vm.HasRememberedLogin,
                "Authentication in progress must preserve availability of the remembered login."
            );
            Assert.AreEqual("Authenticating...", vm.StatusText, "The login form must display authentication progress.");
            CollectionAssert.AreEquivalent(
                new[] { "InputsEnabled", "CanRememberLogin", "HasRememberedLogin", "StatusText", "Password" },
                changed
            );
            changed.Clear();
            client.Notify();
            Assert.IsEmpty(changed, "Repeating an unchanged client state must not publish property changes.");

            client.IsAuthenticating = false;
            client.IsClientConnecting = true;
            client.AuthenticationMessage = "";
            client.NotifyConnection();
            Assert.AreEqual("Connecting...", vm.StatusText, "The login form must display connection progress.");
            client.AuthenticationMessage = "Connection failed";
            client.IsClientConnecting = false;
            client.NotifyConnection();
            Assert.AreEqual(
                "Connection failed",
                vm.StatusText,
                "The login form must display the connection failure reason."
            );
            Assert.IsTrue(vm.InputsEnabled, "Connection failure must enable login inputs for recovery.");
            vm.Password = "another secret";
            client.IsClientConnected = true;
            client.NotifyConnection();
            Assert.IsEmpty(vm.Password, "A successful connection must erase the view model's password.");
            Assert.IsFalse(vm.InputsEnabled, "A connected client must have login inputs disabled.");
            client.IsClientConnected = false;
            client.NotifyConnection();
            Assert.IsTrue(vm.InputsEnabled, "Disconnecting must enable login inputs again.");
        }

        [Test]
        public async Task LoginVM_SubmitsCredentialsClearsPasswordAndRejectsConcurrentActions()
        {
            var client = new FakeClient
            {
                CanRememberLogin = true,
                HasRememberedLogin = true,
                Pending = new TaskCompletionSource<bool>(),
            };
            using var vm = new LoginVM(client)
            {
                Login = "player",
                Password = "secret",
                Remember = true,
            };
            client.OnLogin = () =>
                Assert.IsEmpty(
                    vm.Password,
                    "Submitting login must clear the view model's password before calling authentication."
                );
            Task submit = vm.SubmitAsync();
            Assert.AreEqual(1, client.LoginCalls, "Submitting valid login must issue exactly one password request.");
            Assert.AreEqual("player", client.SubmittedLogin, "Submitting login must forward the entered player name.");
            Assert.AreEqual(
                "secret",
                client.SubmittedPassword,
                "Submitting login must forward the entered password before discarding it."
            );
            Assert.IsTrue(client.SubmittedRemember, "Submitting login must forward the Remember Me selection.");
            Assert.IsFalse(vm.InputsEnabled, "A pending login submission must disable further input.");
            await vm.SubmitAsync();
            await vm.RetryAsync();
            Assert.AreEqual(
                1,
                client.LoginCalls,
                "Repeated submission while authenticating must not issue another password request."
            );
            Assert.AreEqual(0, client.RetryCalls, "Remembered login retry must be blocked while authenticating.");
            client.Pending.SetResult(true);
            await submit;
            Assert.IsTrue(
                vm.InputsEnabled,
                "Completing authentication must enable inputs when the client remains disconnected."
            );
            client.Pending = null;
            await vm.RetryAsync();
            Assert.AreEqual(
                1,
                client.RetryCalls,
                "Retrying a remembered login must issue exactly one refresh request."
            );
            client.HasRememberedLogin = false;
            await vm.RetryAsync();
            Assert.AreEqual(
                1,
                client.RetryCalls,
                "Retry without a remembered session must not issue another refresh request."
            );
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public async Task LoginVM_RejectsActionsWhenClientIsBusyOrConnected(
            bool authenticating,
            bool connecting,
            bool connected
        )
        {
            var client = new FakeClient
            {
                IsAuthenticating = authenticating,
                IsClientConnecting = connecting,
                IsClientConnected = connected,
                HasRememberedLogin = true,
            };
            using var vm = new LoginVM(client);
            await vm.SubmitAsync();
            await vm.RetryAsync();
            Assert.AreEqual(
                0,
                client.LoginCalls + client.RetryCalls,
                "Busy or connected clients must not submit password login or remembered-login retry."
            );
        }

        [Test]
        public async Task LoginVM_DisposeUnsubscribesClearsPasswordAndPreventsActions()
        {
            var client = new FakeClient { HasRememberedLogin = true };
            var vm = new LoginVM(client) { Password = "secret" };
            vm.Dispose();
            Assert.IsEmpty(vm.Password, "Disposing the login view model must erase its password.");
            var changed = new List<string>();
            vm.propertyChanged += (_, evt) => changed.Add(evt.propertyName.ToString());
            client.IsAuthenticating = true;
            client.Notify();
            client.NotifyConnection();
            Assert.IsEmpty(changed, "A disposed login view model must not respond to client notifications.");
            await vm.SubmitAsync();
            await vm.RetryAsync();
            Assert.AreEqual(
                0,
                client.LoginCalls + client.RetryCalls,
                "A disposed login view model must not submit authentication requests."
            );
            vm.Dispose();
        }

        [Test]
        public void LobbyVM_LoginVisibilityFollowsSessionInsteadOfTransport()
        {
            var vm = new LobbyVM();
            var changed = new List<string>();
            vm.propertyChanged += (_, evt) => changed.Add(evt.propertyName.ToString());
            Assert.IsTrue(vm.IsLoadingVisible, "Startup must show loading until authentication is resolved.");
            vm.UpdateConnectionState(false, true);
            Assert.IsFalse(vm.IsLoginVisible, "Connecting must not display the login form.");
            vm.UpdateSessionState(ClientSessionView.Session, "", true);
            vm.UpdateConnectionState(true, false);
            Assert.IsFalse(vm.IsLoginVisible, "The lobby must hide login after connection succeeds.");
            Assert.IsTrue(vm.IsClientConnected, "The lobby must reflect the successful client connection.");
            Assert.That(changed, Does.Contain(nameof(LobbyVM.IsLoginVisible)));
            vm.UpdateConnectionState(false, false);
            Assert.IsFalse(vm.IsLoginVisible, "Transport disconnection must preserve the authenticated screen.");
            Assert.IsTrue(vm.IsSessionVisible, "The lobby must remain visible while recovering.");
            changed.Clear();
            vm.UpdateConnectionState(false, false);
            Assert.IsEmpty(changed, "Repeating the disconnected state must not publish redundant property changes.");
        }

        [UnityTest]
        public IEnumerator LoginTemplate_BindsValuesStateVisibilityAndHandlesEnterAndReload()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var client = new FakeClient();
            using var vm = new LoginVM(client) { Login = "initial binding" };
            var lobbyVM = new LobbyVM();
            lobbyVM.UpdateSessionState(ClientSessionView.Login, "", true);
            LoginUiController ui = null;
            try
            {
                window.Show();
                var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml"
                );
                var root = CloneAuthenticationLobby(template);
                var mainView__login = root.Q("main-view__login");
                window.rootVisualElement.Add(root);
                root.Q("main-view").dataSource = lobbyVM;
                ui = new LoginUiController(root, vm);

                yield return TestUtils.WaitForBindings(() =>
                    root.Q<TextField>("footstar-login").value == "initial binding"
                );

                var password = root.Q<TextField>("footstar-password");
                var login = root.Q<TextField>("footstar-login");
                Assert.IsTrue(password.isPasswordField);
                Assert.IsFalse(root.Q<Toggle>("remember-login").value);
                Assert.AreEqual(DisplayStyle.None, root.Q("remember-login").resolvedStyle.display);
                Assert.AreEqual(DisplayStyle.None, root.Q("login-retry").resolvedStyle.display);
                Assert.AreEqual(DisplayStyle.Flex, mainView__login.resolvedStyle.display);
                Assert.AreEqual(DisplayStyle.None, root.Q("main-view__lobby").resolvedStyle.display);

                login.value = "player";
                password.value = "secret";
                client.CanRememberLogin = true;
                client.HasRememberedLogin = true;
                client.Notify();

                yield return TestUtils.WaitForBindings(() => vm.Login == "player" && vm.Password == "secret");
                Assert.AreEqual("player", vm.Login);
                Assert.AreEqual("secret", vm.Password);
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q("remember-login").resolvedStyle.display,
                    "Remember Me must remain hidden when automatic remembering is supported."
                );
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q("login-retry").resolvedStyle.display,
                    "Connection retry belongs on the startup problem screen."
                );
                using (
                    var evt = KeyDownEvent.GetPooled(
                        new Event
                        {
                            type = EventType.KeyDown,
                            keyCode = KeyCode.Return,
                            character = '\n',
                        }
                    )
                )
                {
                    evt.target = password;
                    password.SendEvent(evt);
                }

                yield return TestUtils.WaitForBindings(() => password.value == "");
                Assert.AreEqual(
                    1,
                    client.LoginCalls,
                    "Submitting the bound login form must issue exactly one password request."
                );
                Assert.AreEqual(
                    "secret",
                    client.SubmittedPassword,
                    "The bound login form must forward the entered password."
                );
                Assert.IsTrue(client.SubmittedRemember, "Supported platforms must automatically request remembering.");

                //  TODO password.value is secret

                Assert.IsEmpty(password.value, "Submitting the bound login form must clear the password field.");

                vm.Password = "clear on authentication";
                client.IsAuthenticating = true;
                client.AuthenticationMessage = "Authenticating...";
                client.Notify();

                var loginStatus = root.Q<Label>("login-status");

                yield return TestUtils.WaitForBindings(() => loginStatus.text == "Authenticating...");
                // for (int frame = 0; frame < 8; frame++)
                //     yield return null;
                Assert.IsEmpty(password.value, "Authentication progress must clear the bound password field.");
                Assert.IsFalse(
                    root.Q<Button>("login-submit").enabledSelf,
                    "Authentication progress must disable the bound submit button."
                );
                Assert.AreEqual(
                    "Authenticating...",
                    root.Q<Label>("login-status").text,
                    "Authentication progress must update the bound status label."
                );
                using (
                    var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return })
                )
                {
                    evt.target = root.Q("login-panel");
                    root.Q("login-panel").SendEvent(evt);
                }
                Assert.AreEqual(1, client.LoginCalls);

                client.IsAuthenticating = false;
                client.IsClientConnected = true;
                client.NotifyConnection();
                lobbyVM.UpdateConnectionState(true, false);
                lobbyVM.UpdateSessionState(ClientSessionView.Session, "", true);

                yield return TestUtils.WaitForUi(
                    () =>
                        mainView__login.resolvedStyle.display == DisplayStyle.None
                        && root.Q("main-view__lobby").resolvedStyle.display == DisplayStyle.Flex,
                    "the login form to hide and the authenticated lobby to become visible"
                );

                Assert.AreEqual(DisplayStyle.None, mainView__login.resolvedStyle.display);
                Assert.AreEqual(DisplayStyle.Flex, root.Q("main-view__lobby").resolvedStyle.display);
                client.IsClientConnected = false;
                client.NotifyConnection();
                lobbyVM.UpdateConnectionState(false, false);

                vm.Password = "clear on reload";
                yield return TestUtils.WaitForUi(
                    () => password.value == "clear on reload",
                    "password binding before rebuilding the UI"
                );

                password.RemoveFromHierarchy();
                ui.Dispose();
                ui = null;
                Assert.IsEmpty(vm.Password);

                using (
                    var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return })
                )
                {
                    evt.target = root.Q("login-panel");
                    root.Q("login-panel").SendEvent(evt);
                }
                Assert.AreEqual(
                    1,
                    client.LoginCalls,
                    "Pressing Enter while connected must not issue another login request."
                );
                root.RemoveFromHierarchy();
                root = CloneAuthenticationLobby(template);
                window.rootVisualElement.Add(root);
                root.Q("main-view").dataSource = lobbyVM;
                ui = new LoginUiController(root, vm);
                yield return TestUtils.WaitForUi(
                    () => root.Q<TextField>("footstar-login").value == "player",
                    "login binding after rebuilding the UI"
                );
                Assert.AreEqual(
                    "player",
                    root.Q<TextField>("footstar-login").value,
                    "Rebuilding the login UI must restore the retained player name through binding."
                );
                Assert.IsEmpty(
                    root.Q<TextField>("footstar-password").value,
                    "Rebuilding the login UI must leave the password field cleared."
                );
                Assert.IsTrue(
                    root.Q<Button>("login-submit").enabledSelf,
                    "Rebuilding the disconnected login UI must enable submission."
                );
            }
            finally
            {
                ui?.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator LobbyController_ReloadKeepsLoginVMAndLogoutCallbacksDisposeCleanly()
        {
            var previousClient = AppCtx.Client;
            var client = new FakeClient { IsClientConnected = true, SessionView = ClientSessionView.Session };
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var obj = new GameObject("lobby controller lifecycle test");
            obj.SetActive(false);
            LobbyUiController controller = null;
            try
            {
                AppCtx.Client = client;
                var panel = obj.AddComponent<PanelRenderer>();
                controller = new LobbyUiController(panel, new FakeLobbyApi());
                var reload = typeof(LobbyUiController).GetMethod(
                    "OnUIReload",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml"
                );
                window.Show();
                var root = CloneAuthenticationLobby(template);
                window.rootVisualElement.Add(root);
                reload.Invoke(controller, new object[] { panel, root, 1 });
                var vm = (LoginVM)root.Q("login-panel").dataSource;
                vm.Login = "retained player";
                vm.Password = "clear on reload";
                yield return TestUtils.WaitForUi(
                    () => root.Q<TextField>("footstar-password").value == "clear on reload",
                    "password binding before replacing the login view"
                );
                var oldPassword = root.Q<TextField>("footstar-password");
                var oldLogout = root.Q<Button>("login-logout");
                var nextRoot = CloneAuthenticationLobby(template);
                window.rootVisualElement.Add(nextRoot);
                reload.Invoke(controller, new object[] { panel, nextRoot, 2 });
                Assert.AreSame(
                    vm,
                    nextRoot.Q("login-panel").dataSource,
                    "The replacement login panel must bind to the existing view model."
                );
                Assert.AreEqual(
                    "retained player",
                    vm.Login,
                    "Replacing the UI tree must retain the entered player name."
                );
                Assert.IsEmpty(vm.Password, "Replacing the UI tree must clear the view model's password.");
                Assert.IsEmpty(oldPassword.value, "Replacing the UI tree must clear the previous password field.");
                Assert.IsNull(
                    root.Q("login-panel").dataSource,
                    "Replacing the UI tree must detach the previous login panel's data source."
                );
                Click(oldLogout);
                Assert.AreEqual(0, client.LogoutCalls, "The detached logout button must not call the client.");
                root.RemoveFromHierarchy();
                Click(nextRoot.Q<Button>("profile-menu-toggle"));
                yield return TestUtils.WaitForUi(
                    () => nextRoot.Q("profile-menu").resolvedStyle.display == DisplayStyle.Flex,
                    "the username button to open the profile menu"
                );
                Click(nextRoot.Q<Button>("login-logout"));
                Assert.AreEqual(
                    1,
                    client.LogoutCalls,
                    "The replacement logout button must call the client exactly once."
                );
                client.IsClientConnected = false;
                client.SessionView = ClientSessionView.Login;
                client.NotifyConnection();
                // for (int frame = 0; frame < 8; frame++)
                //     yield return null;
                yield return TestUtils.WaitForUi(
                    () =>
                        nextRoot.Q("main-view__login").resolvedStyle.display == DisplayStyle.Flex
                        && nextRoot.Q("main-view__lobby").resolvedStyle.display == DisplayStyle.None,
                    "requiring login to show the form and hide the replacement lobby"
                );
                Assert.AreEqual(
                    DisplayStyle.Flex,
                    nextRoot.Q("main-view__login").resolvedStyle.display,
                    "Disconnecting must show the replacement login view."
                );
                Assert.AreEqual(
                    DisplayStyle.None,
                    nextRoot.Q("main-view__lobby").resolvedStyle.display,
                    "Disconnecting must hide the replacement lobby view."
                );
                controller.Dispose();
                Assert.IsNull(
                    nextRoot.Q("login-panel").dataSource,
                    "Disposing the UI controller must detach the replacement login panel's data source."
                );
                Assert.AreEqual(
                    0,
                    client.AuthenticationSubscribers,
                    "Disposing the UI controller must remove authentication subscriptions."
                );
                Assert.AreEqual(
                    0,
                    client.ConnectionSubscribers,
                    "Disposing the UI controller must remove connection subscriptions."
                );
                controller.Dispose();
                Assert.Throws<ObjectDisposedException>(() => controller.Init(panel, new FakeLobbyApi()));
            }
            finally
            {
                controller?.Dispose();
                AppCtx.Client = previousClient;
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [UnityTest]
        public IEnumerator LobbyTemplate_SwitchesBetweenLoadingProblemLoginAndSessionWithoutLogoutOnWeb()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var client = new FakeClient();
            using var login = new LoginVM(client);
            LoginUiController ui = null;
            try
            {
                window.Show();
                var root = CloneAuthenticationLobby(
                    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                );
                var vm = new LobbyVM();
                root.Q("main-view").dataSource = vm;
                ui = new LoginUiController(root, login);
                window.rootVisualElement.Add(root);
                foreach (
                    var view in new[]
                    {
                        ClientSessionView.Loading,
                        ClientSessionView.Problem,
                        ClientSessionView.Login,
                        ClientSessionView.Session,
                    }
                )
                {
                    vm.UpdateSessionState(view, "The service is unavailable.", false);
                    string visibleName = view switch
                    {
                        ClientSessionView.Loading => "session-loading",
                        ClientSessionView.Problem => "connection-problem",
                        ClientSessionView.Login => "main-view__login",
                        _ => "main-view__lobby",
                    };
                    string[] screens =
                    {
                        "session-loading",
                        "connection-problem",
                        "main-view__login",
                        "main-view__lobby",
                    };
                    yield return TestUtils.WaitForUi(
                        () =>
                            Array.TrueForAll(
                                screens,
                                name =>
                                    root.Q(name).resolvedStyle.display
                                    == (name == visibleName ? DisplayStyle.Flex : DisplayStyle.None)
                            ),
                        $"only the {view} screen to become visible"
                    );
                    foreach (
                        string name in new[]
                        {
                            "session-loading",
                            "connection-problem",
                            "main-view__login",
                            "main-view__lobby",
                        }
                    )
                        Assert.AreEqual(
                            name == visibleName ? DisplayStyle.Flex : DisplayStyle.None,
                            root.Q(name).resolvedStyle.display,
                            $"Screen {name}: only the {view} screen must be visible."
                        );
                    Assert.AreEqual(
                        DisplayStyle.None,
                        root.Q("login-logout").resolvedStyle.display,
                        "WebGL must not display a Logout button in any session state."
                    );
                }
            }
            finally
            {
                ui?.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator ConnectionStatus_PreservesSessionScreenAndCleansUpSubscriptions()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var client = new FakeClient { IsClientConnected = true, SessionView = ClientSessionView.Session };
            ConnectionStatusUiController status = null;
            try
            {
                window.Show();
                status = new ConnectionStatusUiController(client, window.rootVisualElement);
                var indicator = window.rootVisualElement.Q("connection-status");
                yield return TestUtils.WaitForUi(
                    () => indicator.resolvedStyle.display == DisplayStyle.None,
                    "the connected session to hide disconnected status"
                );
                client.IsClientConnected = false;
                client.NotifyConnection();
                yield return TestUtils.WaitForUi(
                    () => indicator.resolvedStyle.display == DisplayStyle.Flex,
                    "a disconnected session to display its persistent status"
                );
                client.SessionView = ClientSessionView.Login;
                client.Notify();
                yield return TestUtils.WaitForUi(
                    () => indicator.resolvedStyle.display == DisplayStyle.None,
                    "the login screen to hide session connection status"
                );
                status.Dispose();
                status = null;
                Assert.AreEqual(
                    0,
                    client.AuthenticationSubscribers,
                    "Closing the status view must remove its authentication subscription."
                );
                Assert.AreEqual(
                    0,
                    client.ConnectionSubscribers,
                    "Closing the status view must remove its connection subscription."
                );
            }
            finally
            {
                status?.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static VisualElement CloneAuthenticationLobby(VisualTreeAsset template)
        {
            var root = template.CloneTree();
            var loginView = root.Q("main-view__login");
            var lobbyView = root.Q("main-view__lobby");
            var logout = root.Q("login-logout");
            var loading = root.Q("session-loading");
            var problem = root.Q("connection-problem");
            var profile = root.Q("profile-menu");
            var profileToggle = root.Q("profile-menu-toggle");
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (
                    element != loginView
                    && !loginView.Contains(element)
                    && element != lobbyView
                    && element != logout
                    && element != loading
                    && !loading.Contains(element)
                    && element != problem
                    && !problem.Contains(element)
                    && element != profile
                    && !profile.Contains(element)
                    && element != profileToggle
                )
                    element.ClearBindings();
            }
            return root;
        }

        private static void Click(Button button)
        {
            typeof(Clickable)
                .GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });
        }

        private sealed class FakeLobbyApi : ILobbyApiClient
        {
            public event Action<ServerInfoContract> ServerInfoUpdated
            {
                add { }
                remove { }
            }
            public event Action<string> AlertReceived
            {
                add { }
                remove { }
            }

            public void SendGameJoinRequest(int gameId) { }

            public void RequestServerInfo() { }

            public void DeleteGame(int gameId) { }

            public event Action<Imma.Foobbuteo.Shared.Lobby.Contract.NewGameResult> NewGameCompleted
            {
                add { }
                remove { }
            }

            public void NewGame(Imma.Foobbuteo.Shared.Lobby.NewGameOptions options) { }

            public void Dispose() { }
        }

        [TestCase(null)]
        [TestCase("prod")]
        public async Task EnvironmentStore_LegacyOrOtherEnvironmentIsClearedBeforeRefresh(string savedEnvironment)
        {
            var store = RememberedStore();
            store.Saved.environment = savedEnvironment;
            var web = new FakeWeb();
            using var coordinator = new ClientAuthenticationCoordinator(
                web,
                new EnvironmentRememberedSessionStore(store, ClientEnvironment.Development),
                () => Now
            );

            await coordinator.RefreshAsync();

            Assert.IsFalse(
                coordinator.HasRememberedSession,
                "Legacy and production sessions must not be loaded by a development client."
            );
            Assert.IsNull(store.Saved, "An incompatible remembered session must be deleted from storage.");
            Assert.IsNull(
                web.RefreshRequested,
                "A token from another environment must never reach the refresh endpoint."
            );
            Assert.IsNull(web.Revoked, "An incompatible token must not be revoked against the current environment.");
        }

        [Test]
        public void EnvironmentStore_MatchingSessionIsRetained()
        {
            var store = RememberedStore();
            store.Saved.environment = ClientEnvironment.Development;
            using var coordinator = new ClientAuthenticationCoordinator(
                new FakeWeb(),
                new EnvironmentRememberedSessionStore(store, ClientEnvironment.Development),
                () => Now
            );
            Assert.IsTrue(
                coordinator.HasRememberedSession,
                "A valid remembered session for the selected environment must remain available."
            );
            Assert.IsNotNull(store.Saved, "Loading a matching session must preserve device storage.");
        }

        [Test]
        public async Task EnvironmentStore_LoginAndRefreshPersistSelectedEnvironment()
        {
            var store = new FakeStore();
            var web = new FakeWeb { Result = Success(true) };
            using var coordinator = new ClientAuthenticationCoordinator(
                web,
                new EnvironmentRememberedSessionStore(store, ClientEnvironment.Development),
                () => Now
            );

            await coordinator.LoginAsync("player", "password", true);

            Assert.AreEqual(
                ClientEnvironment.Development,
                store.Saved.environment,
                "Remembered password login must persist the selected environment alongside its token."
            );
            coordinator.UpdateConnectionState(false, false);
            web.Result = Success(true, "rotated-refresh");
            await coordinator.RefreshAsync();
            Assert.AreEqual(
                ClientEnvironment.Development,
                store.Saved.environment,
                "Rotating a refresh token must retain its environment binding."
            );
            Assert.AreEqual(
                "rotated-refresh",
                store.Saved.refreshToken,
                "Environment binding must preserve normal token rotation."
            );
        }

        [Test]
        public async Task LoginVM_WaitsForConfigurationBeforeAllowingActions()
        {
            var client = new FakeClient
            {
                IsAuthenticationReady = false,
                HasRememberedLogin = true,
                AuthenticationMessage = "Waiting for client environment configuration.",
            };
            using var vm = new LoginVM(client) { Login = "player", Password = "secret" };
            await vm.SubmitAsync();
            await vm.RetryAsync();
            Assert.IsFalse(vm.InputsEnabled, "Login controls must be disabled while waiting for configuration.");
            Assert.AreEqual(0, client.LoginCalls, "Submitting before environment selection must not send credentials.");
            Assert.AreEqual(0, client.RetryCalls, "Remembered login must not run before configuration is ready.");
            client.IsAuthenticationReady = true;
            client.Notify();
            Assert.IsTrue(vm.InputsEnabled, "Configuration completion must enable login through state notification.");
        }

        [UnityTest]
        public IEnumerator LoginTemplate_ConfigurationReadinessDisablesLoginAndSignup()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var client = new FakeClient { IsAuthenticationReady = false };
            using var vm = new LoginVM(client);
            LoginUiController ui = null;
            try
            {
                window.Show();
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Auth/Login.tmpl.uxml")
                    .CloneTree();
                window.rootVisualElement.Add(root);
                ui = new LoginUiController(root, vm);
                var login = root.Q<Button>("login-submit");
                var signup = root.Q<Button>("signup-link");
                yield return TestUtils.WaitForUi(
                    () => !login.enabledSelf && !signup.enabledSelf,
                    "login and signup disabled while configuration is pending"
                );
                Assert.IsFalse(login.enabledSelf, "Pending configuration must disable the login binding.");
                Assert.IsFalse(signup.enabledSelf, "Pending configuration must disable the signup binding.");
                client.IsAuthenticationReady = true;
                client.Notify();
                yield return TestUtils.WaitForUi(
                    () => login.enabledSelf && signup.enabledSelf,
                    "login and signup enabled after configuration"
                );
                Assert.IsTrue(signup.enabledSelf, "Ready configuration must enable signup through the VM binding.");
            }
            finally
            {
                ui?.Dispose();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static FakeStore RememberedStore() =>
            new FakeStore
            {
                Saved = new RememberedSession
                {
                    refreshToken = "old-refresh",
                    refreshExpiresAtUtc = Now.AddDays(45).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                },
            };

        private sealed class FakeStore : IRememberedSessionStore
        {
            public bool IsSupported { get; set; } = true;
            public RememberedSession Saved;
            public bool ThrowSave;
            public bool ThrowLoad;

            public RememberedSession Load()
            {
                if (ThrowLoad)
                    throw new InvalidOperationException();
                return Saved == null ? null : Copy(Saved);
            }

            public void Save(RememberedSession session)
            {
                if (ThrowSave)
                    throw new InvalidOperationException();
                Saved = Copy(session);
            }

            public void Clear() => Saved = null;

            private static RememberedSession Copy(RememberedSession source) =>
                new RememberedSession
                {
                    refreshToken = source.refreshToken,
                    environment = source.environment,
                    refreshExpiresAtUtc = source.refreshExpiresAtUtc,
                };
        }

        private sealed class FakeWeb : IAuthenticationWebClient
        {
            public AuthenticationResult Result = AuthenticationResult.Failure(
                AuthenticationResult.ErrorCodes.ServiceUnavailable,
                ""
            );
            public TaskCompletionSource<AuthenticationResult> Pending;
            public int LoginCalls;
            public int RefreshCalls;
            public bool RememberRequested;
            public string RefreshRequested;
            public string Revoked;
            public bool RevokeSuccess = true;
            public CancellationToken Cancellation;

            public Task<AuthenticationResult> LoginAsync(
                string login,
                string password,
                bool remember,
                CancellationToken cancellation
            )
            {
                LoginCalls++;
                RememberRequested = remember;
                Cancellation = cancellation;
                return Pending?.Task ?? Task.FromResult(Result);
            }

            public Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellation)
            {
                RefreshCalls++;
                RefreshRequested = refreshToken;
                Cancellation = cancellation;
                return Pending?.Task ?? Task.FromResult(Result);
            }

            public Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellation)
            {
                Revoked = refreshToken;
                return Task.FromResult(RevokeSuccess);
            }
        }

        private sealed class FakeClient : IClientAppCtrl
        {
            public ClientSessionView SessionView { get; set; } = ClientSessionView.Login;
            public bool CanLogout => true;
            public int OpenGameId => 0;

            public void OnLobbySpawned() { }

            public bool IsAuthenticationReady { get; set; } = true;
            public GameUser User { get; set; }
            public bool IsClientConnected { get; set; }
            public bool IsClientConnecting { get; set; }
            public bool IsAuthenticating { get; set; }
            public bool CanRememberLogin { get; set; }
            public bool HasRememberedLogin { get; set; }
            public string AuthenticationMessage { get; set; } = "";
            public event Action AuthenticationStateChanged;
            public event Action<GameUser> UserInfoUpdated
            {
                add { }
                remove { }
            }
            public event Action ClientConnectionStateChanged;

            public void Notify() => AuthenticationStateChanged?.Invoke();

            public int LoginCalls;
            public int RetryCalls;
            public int LogoutCalls;
            public int ToastCalls;
            public int AuthenticationSubscribers => AuthenticationStateChanged?.GetInvocationList().Length ?? 0;
            public int ConnectionSubscribers => ClientConnectionStateChanged?.GetInvocationList().Length ?? 0;
            public string SubmittedLogin;
            public string SubmittedPassword;
            public bool SubmittedRemember;
            public Action OnLogin;
            public TaskCompletionSource<bool> Pending;

            public void NotifyConnection() => ClientConnectionStateChanged?.Invoke();

            public Task LoginAsync(string login, string password, bool remember)
            {
                LoginCalls++;
                SubmittedLogin = login;
                SubmittedPassword = password;
                SubmittedRemember = remember;
                OnLogin?.Invoke();
                return Pending?.Task ?? Task.CompletedTask;
            }

            public Task RetryRememberedLoginAsync()
            {
                RetryCalls++;
                return Pending?.Task ?? Task.CompletedTask;
            }

            public Task LogoutAsync()
            {
                LogoutCalls++;
                return Task.CompletedTask;
            }

            public bool OpenLobbyScene() => true;

            public void OpenGameScene(int gameId) { }

            public void StartClient() { }

            public void DevDisconnect() { }

            public void SetRootElement(VisualElement rootElement) { }

            public void ShowToast(string message, float durationInSeconds = 3) => ToastCalls++;

            public void ShowBanner(string message, float durationInSeconds = 3) { }
        }
    }
}
