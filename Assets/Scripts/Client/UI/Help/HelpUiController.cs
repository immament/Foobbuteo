// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Imma.Foobbuteo.Client.UI.Modals;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Help
{
    internal sealed class HelpUiController : IDisposable
    {
        private readonly ModalPanelController m_modal;
        private Button m_open;
        private Button m_mobileOpen;
        private Button m_close;

        internal HelpUiController(ModalHostController host) =>
            m_modal = new ModalPanelController(host, "help-modal", () => true, _ => { });

        internal void Bind(VisualElement root)
        {
            Unbind();
            m_open = root.Q<Button>("HelpButton");
            m_mobileOpen = root.Q<Button>("pitch-help-button");
            m_close = root.Q<Button>("HelpCloseButton");
            m_open.clicked += Open;
            m_mobileOpen.clicked += OpenMobile;
            m_close.clicked += Close;
            m_modal.Bind(root.Q("help-modal"), m_close);
        }

        private void Open() => m_modal.TryOpen(m_open);

        private void OpenMobile() => m_modal.TryOpen(m_mobileOpen);

        private void Close() => m_modal.TryClose(ModalCloseReason.CloseButton);

        internal void Unbind()
        {
            m_modal.Unbind();
            if (m_open == null)
                return;
            m_open.clicked -= Open;
            m_mobileOpen.clicked -= OpenMobile;
            m_close.clicked -= Close;
            m_open = m_mobileOpen = m_close = null;
        }

        public void Dispose()
        {
            Unbind();
            m_modal.Dispose();
        }
    }
}
