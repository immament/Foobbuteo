// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed partial class GamePanelSettingsRuntime : MonoBehaviour
    {
        [SerializeField]
        private PanelRenderer m_mainRenderer;

        [SerializeField]
        private PanelRenderer m_tooltipRenderer;

        internal void Configure(PanelRenderer main, PanelRenderer tooltip)
        {
            m_mainRenderer = main;
            m_tooltipRenderer = tooltip;
        }
    }
}
