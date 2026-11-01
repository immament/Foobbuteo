// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading;
using System.Threading.Tasks;
using Imma.Common;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class ClientAuthenticationCoordinator : IDisposable
    {
        private readonly IAuthenticationWebClient m_web;
        private readonly IRememberedSessionStore m_store;
        private readonly Func<DateTimeOffset> m_now;
        private RememberedSession m_session;
        private CancellationTokenSource m_request;
        private int m_generation;
        private bool m_connected;
        private bool m_connecting;
        private bool m_disposed;
        private bool m_storageClearFailed;
        private readonly Func<ClientConnectionConfig, CancellationToken, Task<AuthenticationResult>> m_webSession;
        private ClientConnectionConfig m_externalConnection;

        public bool RequiresLogin { get; private set; }
        public bool CanRecover => HasRememberedSession || m_externalConnection != null && m_webSession != null;

        public bool IsAuthenticating => m_request != null;
        public bool CanRemember => m_store.IsSupported;
        public bool HasRememberedSession => m_session != null;
        public string Message { get; private set; } = "";
        public event Action StateChanged;
        public event Action<ClientConnectionConfig> ConnectionReady;

        public ClientAuthenticationCoordinator(
            IAuthenticationWebClient web,
            IRememberedSessionStore store,
            Func<DateTimeOffset> now = null,
            Func<ClientConnectionConfig, CancellationToken, Task<AuthenticationResult>> webSession = null
        )
        {
            m_web = web;
            m_store = store;
            m_now = now ?? (() => DateTimeOffset.UtcNow);
            m_webSession = webSession;
            RequiresLogin = true;
            if (!store.IsSupported)
                return;
            try
            {
                m_session = store.Load();
                RequiresLogin = m_session == null;
                if (m_session != null && !m_session.IsValid(m_now()))
                {
                    ForgetSession();
                    SetMessage("Remembered login expired. Please log in again.");
                }
            }
            catch
            {
                ForgetSession();
                SetMessage("Remembered login could not be read. Please log in again.");
            }
        }

        public Task LoginAsync(string login, string password, bool remember)
        {
            if (IsBusy)
                return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            {
                SetMessage("Enter your Footstar login and password.");
                return Task.CompletedTask;
            }
            m_externalConnection = null;
            return AuthenticateWithPasswordAsync(login, password, remember && CanRemember);
        }

        public Task RefreshAsync()
        {
            if (IsBusy || m_session == null)
                return Task.CompletedTask;
            if (!m_session.IsValid(m_now()))
            {
                ForgetSession();
                SetMessage("Remembered login expired. Please log in again.");
                return Task.CompletedTask;
            }
            return AuthenticateRememberedSessionAsync();
        }

        private bool IsBusy => m_disposed || IsAuthenticating || m_connected || m_connecting;

        public bool AcceptExternalConfig(ClientConnectionConfig config)
        {
            if (IsBusy || !config.HasUsableToken(m_now()))
            {
                config.token = null;
                return false;
            }
            m_externalConnection = config.CopyConnection();
            RequiresLogin = false;
            Connect(config);
            return true;
        }

        private Task AuthenticateWithPasswordAsync(string login, string password, bool remember)
        {
            return ExecuteAuthenticationAsync(
                "Signing in...",
                cancellation =>
                {
                    var pending = m_web.LoginAsync(login, password, remember, cancellation);
                    login = null;
                    password = null;
                    return pending;
                },
                result => PreparePasswordLogin(result, remember)
            );
        }

        private Task AuthenticateRememberedSessionAsync()
        {
            string previousToken = m_session.refreshToken;
            string previousExpiry = m_session.refreshExpiresAtUtc;
            return ExecuteAuthenticationAsync(
                "Signing in with remembered login...",
                cancellation => m_web.RefreshTokenAsync(previousToken, cancellation),
                result => PrepareRememberedLogin(result, previousToken, previousExpiry)
            );
        }

        private string PreparePasswordLogin(AuthenticationResult result, bool remember)
        {
            if (!result.Succeeded)
            {
                RequiresLogin = true;
                this.LWarn($"{result.Error}, {result.ErrorDetails}");
                SetMessage(ErrorMessage(result.Error));
                return null;
            }

            RequiresLogin = false;

            string oldToken = m_session?.refreshToken;
            if (remember && result.Session != null)
            {
                string warning = SaveRememberedSession(result.Session);
                if (warning.Length == 0 && oldToken != null)
                    _ = TryRevokeAsync(oldToken);
                return warning;
            }

            ForgetSession();
            if (oldToken != null)
                _ = TryRevokeAsync(oldToken);
            return "";
        }

        private string PrepareRememberedLogin(AuthenticationResult result, string previousToken, string previousExpiry)
        {
            if (!result.Succeeded)
            {
                if (result.IsInvalidSession)
                    ForgetSession();
                SetMessage(ErrorMessage(result.Error));
                return null;
            }

            if (result.Session == null)
            {
                Forget("PRL1");
                return null;
            }
            if (result.Session.refreshToken == previousToken)
            {
                Forget("PRL2");
                return null;
            }
            if (!AuthenticationResult.TryUtcDate(previousExpiry, out var oldExpiry))
            {
                Forget("PRL3");
                return null;
            }
            if (!AuthenticationResult.TryUtcDate(result.Session.refreshExpiresAtUtc, out var newExpiry))
            {
                Forget("PRL4");
                return null;
            }
            if (oldExpiry != newExpiry)
            {
                Forget("PRL5");
                return null;
            }

            return SaveRememberedSession(result.Session);

            void Forget(string code)
            {
                ForgetSession();
                SetMessage($"Invalid remembered login response. Please log in again. {code}");
            }
        }

        /// <summary>
        /// Save Session Token on Device
        /// </summary>
        /// <param name="session"></param>
        /// <returns>Error messege in case of problem, empty string if success</returns>
        private string SaveRememberedSession(RememberedSession session)
        {
            try
            {
                m_store.Save(session);
                m_session?.ClearSecret();
                m_session = new RememberedSession
                {
                    refreshToken = session.refreshToken,
                    refreshExpiresAtUtc = session.refreshExpiresAtUtc,
                };
                return "";
            }
            catch (Exception e)
            {
                this.LWarn($"Save session problem: {e}");
                ForgetSession();
                _ = TryRevokeAsync(session.refreshToken);
                return "Remember Me is unavailable on this device.";
            }
        }

        private async Task ExecuteAuthenticationAsync(
            string message,
            Func<CancellationToken, Task<AuthenticationResult>> sendRequest,
            Func<AuthenticationResult, string> prepareConnection
        )
        {
            int generation = ++m_generation;
            RequiresLogin = false;
            using var request = new CancellationTokenSource();
            m_request = request;
            SetMessage(message);
            AuthenticationResult result = null;
            try
            {
                result = await sendRequest(request.Token);
                if (generation != m_generation || m_disposed || request.IsCancellationRequested)
                {
                    if (result.Session != null)
                        _ = TryRevokeAsync(result.Session.refreshToken);
                    return;
                }
                // Null rejects the response; an empty string accepts it without a storage warning.
                string warning = prepareConnection(result);
                if (warning == null)
                    return;

                var connection = result.Connection;
                result.Session?.ClearSecret();
                // The transport owns the short-lived token from this point, not the HTTP result.
                result = null;
                m_request = null;
                Connect(connection, warning);
            }
            catch (OperationCanceledException) { }
            catch
            {
                if (generation == m_generation && !m_disposed)
                {
                    if (!CanRecover)
                        RequiresLogin = true;
                    SetMessage("Login service unavailable. Try again.");
                }
            }
            finally
            {
                result?.ClearSecrets();
                if (ReferenceEquals(m_request, request))
                    m_request = null;
                if (!m_disposed)
                    StateChanged?.Invoke();
            }
        }

        private void Connect(ClientConnectionConfig config, string warning = "")
        {
            if (!config.HasUsableToken(m_now()))
            {
                config.token = null;
                SetMessage("The game login token expired. Please sign in again.");
                return;
            }
            RequiresLogin = false;
            m_connecting = true;
            SetMessage(string.IsNullOrEmpty(warning) ? "Connecting..." : warning);
            try
            {
                ConnectionReady?.Invoke(config);
            }
            catch
            {
                config.token = null;
                m_connecting = false;
                SetMessage("Unable to start the game connection. Please try again.");
            }
        }

        public void UpdateConnectionState(bool connected, bool connecting, string reason = null)
        {
            bool wasConnecting = m_connecting;
            bool wasConnected = m_connected;
            m_connected = connected;
            m_connecting = connecting;
            if (connected)
                RequiresLogin = false;
            if (connected && Message == "Connecting...")
                Message = "";
            else if (!connected && !connecting && (wasConnecting || wasConnected))
                Message = string.IsNullOrEmpty(reason)
                    ? "Unable to connect to the game server. Check your connection and retry."
                    : Imma.Foobbuteo.Shared.ClientDisconnectReason.Message(reason);
            StateChanged?.Invoke();
        }

        public Task RecoverAsync()
        {
            if (IsBusy)
                return Task.CompletedTask;
            if (m_externalConnection == null)
                return RefreshAsync();
            if (m_webSession == null)
                return Task.CompletedTask;
            return ExecuteAuthenticationAsync(
                "Reconnecting...",
                cancellation => m_webSession(m_externalConnection, cancellation),
                result =>
                {
                    if (result.Succeeded)
                        return "";
                    if (result.IsInvalidSession)
                        RequireLogin();
                    SetMessage(ErrorMessage(result.Error));
                    return null;
                }
            );
        }

        public void RequireLogin()
        {
            m_externalConnection = null;
            ForgetSession();
            SetMessage("Please log in again.");
        }

        public async Task LogoutAsync()
        {
            if (m_disposed)
                return;
            ++m_generation;
            m_request?.Cancel();
            m_request = null;
            m_connected = false;
            m_connecting = false;
            m_externalConnection = null;
            string token = m_session?.refreshToken;
            ForgetSession();
            SetMessage("Logged out.");
            int generation = m_generation;
            if (token != null && !await TryRevokeAsync(token) && generation == m_generation && !m_disposed)
                SetMessage("Logged out locally. Server session revocation could not be confirmed.");
        }

        private async Task<bool> TryRevokeAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return true;
            try
            {
                return await m_web.RevokeAsync(token, CancellationToken.None);
            }
            catch
            {
                return false;
            }
        }

        private void ForgetSession()
        {
            RequiresLogin = true;
            m_session?.ClearSecret();
            m_session = null;
            try
            {
                m_store.Clear();
                m_storageClearFailed = false;
            }
            catch
            {
                m_storageClearFailed = true;
            }
        }

        private void SetMessage(string message)
        {
            Message = m_storageClearFailed
                ? message + " Remembered login storage could not be cleared on this device."
                : message;
            StateChanged?.Invoke();
        }

        private static string ErrorMessage(string error) =>
            error switch
            {
                AuthenticationResult.ErrorCodes.InvalidCredentials => "Invalid login or password.",
                AuthenticationResult.ErrorCodes.InvalidSession =>
                    "Remembered login expired or was revoked. Please log in again.",
                AuthenticationResult.ErrorCodes.RateLimited =>
                    "Too many login attempts. Please wait before trying again.",
                AuthenticationResult.ErrorCodes.NotConfigured => "Login service is not configured for this build.",
                AuthenticationResult.ErrorCodes.InvalidResponse =>
                    "The login service returned an invalid response. Please try again.",
                AuthenticationResult.ErrorCodes.NetworkError =>
                    "Cannot reach the login service. Check your connection and retry.",
                _ => "Login service unavailable. Please try again.",
            };

        public void Dispose()
        {
            m_disposed = true;
            ++m_generation;
            m_request?.Cancel();
            m_request = null;
            m_session?.ClearSecret();
            m_session = null;
        }
    }
}
