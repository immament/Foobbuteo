// SPDX-License-Identifier: AGPL-3.0-only
using Unity.Properties;

namespace Imma.Foobbuteo.Client.UI.Modals
{
    internal sealed class ModalHostState : BindableViewModel
    {
        internal ModalPanelState ActivePanel { get; private set; }

        [CreateProperty]
        public bool HasActivePanel => ActivePanel != null;

        [CreateProperty]
        public bool BackgroundEnabled => !HasActivePanel;

        internal void SetActive(ModalPanelState panel)
        {
            if (ReferenceEquals(ActivePanel, panel))
                return;
            var previous = ActivePanel;
            ActivePanel = panel;
            previous?.Refresh();
            panel?.Refresh();
            Notify(nameof(HasActivePanel));
            Notify(nameof(BackgroundEnabled));
        }
    }
}
