// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class LoginUiController : IDisposable
    {
        private readonly LoginVM m_viewModel;
        private readonly VisualElement m_loginPanel;
        private readonly TextField m_password;
        private readonly Button m_submit;
        private readonly Button m_retry;
        private readonly Button m_signup;

        public LoginUiController(VisualElement root, LoginVM viewModel)
        {
            m_viewModel = viewModel;
            m_loginPanel = root.Q("login-panel");
            m_loginPanel.dataSource = viewModel;
            m_loginPanel.dataSourcePath = new PropertyPath();
            m_password = m_loginPanel.Q<TextField>("footstar-password");
            m_submit = m_loginPanel.Q<Button>("login-submit");
            m_retry = m_loginPanel.Q<Button>("login-retry");
            m_signup = m_loginPanel.Q<Button>("signup-link");
            m_submit.clicked += Submit;
            m_retry.clicked += Retry;
            m_signup.clicked += Signup;
            m_loginPanel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        private void Submit() => _ = m_viewModel.SubmitAsync();

        private void Retry() => _ = m_viewModel.RetryAsync();

        private static void Signup()
        {
            if (ClientAppCtrl.Instance.IsAuthenticationReady)
                Application.OpenURL(ClientAppCtrl.Instance.ClientSettings.signupUrl);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            this.LTrace(() => $"{evt.keyCode}, {m_submit.enabledInHierarchy}");
            if ((evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter) && m_submit.enabledInHierarchy)
            {
                evt.StopPropagation();
                Submit();
            }
        }

        public void Dispose()
        {
            m_submit.clicked -= Submit;
            m_retry.clicked -= Retry;
            m_signup.clicked -= Signup;
            m_loginPanel.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            m_viewModel.ClearPassword();
            m_loginPanel.dataSource = null;
            if (m_password.panel != null)
                m_password.SetValueWithoutNotify("");
        }
    }
}
