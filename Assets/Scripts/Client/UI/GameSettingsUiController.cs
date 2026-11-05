// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Imma.Foobbuteo.Client.UI.Modals;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class GameSettingsUiController : IDisposable
    {
        private readonly GameSettingsVM m_vm;
        private readonly ModalHostController m_host;
        private readonly bool m_ownsHost;
        private readonly ModalPanelController m_modal;
        private VisualElement m_panel;
        private Button m_open;
        private Button m_close;

        internal GameSettingsUiController(GameSettingsVM vm, ModalHostController host = null)
        {
            m_vm = vm;
            m_ownsHost = host == null;
            m_host = host ?? new ModalHostController(new ModalHostState());
            m_modal = new ModalPanelController(m_host, "game-settings-overlay", () => true, _ => { });
        }

        public void Bind(VisualElement root)
        {
            Unbind();
            if (m_ownsHost)
                m_host.Bind(root);
            m_panel = root.Q("game-settings-panel");
            m_panel.dataSource = m_vm;
            m_panel.dataSourcePath = new PropertyPath();
            m_open = root.Q<Button>("open-settings-btn");
            m_close = m_panel.Q<Button>("close-game-settings-btn");
            m_open.clicked += Open;
            m_close.clicked += Close;
            m_modal.Bind(root.Q("game-settings-overlay"), m_panel.Q<SliderInt>("sound-volume-slider"));
        }

        private void Open()
        {
            if (m_modal.TryOpen(m_open))
                m_vm.Refresh();
        }

        private void Close() => m_modal.TryClose(ModalCloseReason.CloseButton);

        internal void Unbind()
        {
            m_modal.Unbind();
            if (m_panel == null)
                return;
            m_open.clicked -= Open;
            m_close.clicked -= Close;
            m_panel.dataSource = null;
            m_panel = null;
            m_open = m_close = null;
            if (m_ownsHost)
                m_host.Unbind();
        }

        public void Dispose()
        {
            Unbind();
            m_modal.Dispose();
            if (m_ownsHost)
                m_host.Dispose();
        }
    }
}
