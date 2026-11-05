// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Core;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class ConnectionStatusUiController : INotifyBindablePropertyChanged, IDisposable
    {
        private readonly IClientAppCtrl m_client;
        private readonly VisualElement m_element;

        public ConnectionStatusUiController(IClientAppCtrl client, VisualElement root)
        {
            m_client = client;
            m_element = new VisualElement
            {
                name = "connection-status",
                dataSource = this,
                pickingMode = PickingMode.Ignore,
            };
            m_element.AddToClassList("connection-status");
            var dot = new Label("●") { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("connection-status__dot");
            m_element.Add(dot);
            m_element.Add(new Label("Disconnected") { pickingMode = PickingMode.Ignore });
            m_element.SetBinding(
                "style.display",
                new DataBinding
                {
                    dataSourcePath = new PropertyPath(nameof(Display)),
                    bindingMode = BindingMode.ToTarget,
                }
            );
            (root.Q("notifications-safe-area") ?? root.Q("SafeAreaRoot") ?? root.Q("main-view") ?? root).Add(m_element);
            m_client.ClientConnectionStateChanged += Refresh;
            m_client.AuthenticationStateChanged += Refresh;
        }

        [CreateProperty]
        public DisplayStyle Display =>
            !m_client.IsClientConnected && m_client.SessionView == ClientSessionView.Session
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public void Refresh() => propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Display)));

        public void Dispose()
        {
            m_client.ClientConnectionStateChanged -= Refresh;
            m_client.AuthenticationStateChanged -= Refresh;
            m_element.RemoveFromHierarchy();
        }
    }
}
