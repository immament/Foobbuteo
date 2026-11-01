// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Imma.Foobbuteo.Core;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class LoginVM : INotifyBindablePropertyChanged, IDisposable
    {
        private readonly IClientAppCtrl m_client;
        private string m_login = "";
        private string m_password = "";
        private bool m_remember;
        private string m_statusText = "";
        private bool m_inputsEnabled;
        private bool m_canRememberLogin;
        private bool m_hasRememberedLogin;
        private bool m_actionPending;
        private bool m_disposed;

        public LoginVM(IClientAppCtrl client)
        {
            m_client = client;
            m_client.AuthenticationStateChanged += RefreshState;
            m_client.ClientConnectionStateChanged += RefreshState;
            RefreshState();
        }

        [CreateProperty]
        public string Login
        {
            get => m_login;
            set => Set(ref m_login, value ?? "");
        }

        [CreateProperty]
        public string Password
        {
            get => m_password;
            set => Set(ref m_password, value ?? "");
        }

        [CreateProperty]
        public bool Remember
        {
            get => m_remember;
            set => Set(ref m_remember, value);
        }

        [CreateProperty]
        public string StatusText => m_statusText;

        [CreateProperty]
        public bool InputsEnabled => m_inputsEnabled;

        [CreateProperty]
        public bool CanRememberLogin => m_canRememberLogin;

        [CreateProperty]
        public bool HasRememberedLogin => m_hasRememberedLogin;

        public Task SubmitAsync()
        {
            if (!CanAct())
                return Task.CompletedTask;
            string password = Password;
            ClearPassword();
            return RunActionAsync(() => m_client.LoginAsync(Login, password, m_client.CanRememberLogin));
        }

        public Task RetryAsync()
        {
            if (!CanAct() || !m_client.HasRememberedLogin)
                return Task.CompletedTask;
            ClearPassword();
            return RunActionAsync(m_client.RetryRememberedLoginAsync);
        }

        public void ClearPassword() => Password = "";

        private bool CanAct() =>
            !m_disposed
            && m_client.IsAuthenticationReady
            && !m_actionPending
            && !m_client.IsClientConnected
            && !m_client.IsAuthenticating
            && !m_client.IsClientConnecting;

        private async Task RunActionAsync(Func<Task> action)
        {
            m_actionPending = true;
            RefreshState();
            try
            {
                await action();
            }
            finally
            {
                m_actionPending = false;
                if (!m_disposed)
                    RefreshState();
            }
        }

        private void RefreshState()
        {
            bool busy = m_actionPending || m_client.IsAuthenticating || m_client.IsClientConnecting;
            Set(
                ref m_inputsEnabled,
                m_client.IsAuthenticationReady && !busy && !m_client.IsClientConnected,
                nameof(InputsEnabled)
            );
            Set(ref m_canRememberLogin, m_client.CanRememberLogin, nameof(CanRememberLogin));
            Set(ref m_hasRememberedLogin, m_client.HasRememberedLogin, nameof(HasRememberedLogin));
            Set(
                ref m_statusText,
                m_client.IsClientConnecting && string.IsNullOrEmpty(m_client.AuthenticationMessage)
                    ? "Connecting..."
                    : m_client.AuthenticationMessage ?? "",
                nameof(StatusText)
            );
            if (busy || m_client.IsClientConnected)
                ClearPassword();
        }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            m_disposed = true;
            m_client.AuthenticationStateChanged -= RefreshState;
            m_client.ClientConnectionStateChanged -= RefreshState;
            ClearPassword();
        }
    }
}
