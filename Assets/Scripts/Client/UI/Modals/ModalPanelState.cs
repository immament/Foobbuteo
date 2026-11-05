// SPDX-License-Identifier: AGPL-3.0-only
using Unity.Properties;

namespace Imma.Foobbuteo.Client.UI.Modals
{
    internal sealed class ModalPanelState : BindableViewModel
    {
        private readonly ModalHostState m_host;

        internal ModalPanelState(ModalHostState host) => m_host = host;

        [CreateProperty]
        public bool IsVisible => ReferenceEquals(m_host.ActivePanel, this);

        internal void Refresh() => Notify(nameof(IsVisible));
    }
}
