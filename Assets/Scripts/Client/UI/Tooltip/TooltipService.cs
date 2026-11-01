// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Tooltip
{
    // Server builds strip PanelRenderer while processing the scene.
#if !UNITY_SERVER
    [RequireComponent(typeof(PanelRenderer))]
#endif
    public class TooltipService : MonoBehaviour
    {
        public static TooltipService Instance { get; private set; }

        private VisualElement m_RootElement;

        //DEV: serialized only for test
        [SerializeField]
        private int m_UiVersion = 0;

        private VisualElement m_tooltip;
        private Label m_titleLabel;
        private Label m_descriptionLabel;

        private Vector2 m_offset;

        private Vector2 m_lastPanelPos;
        private bool m_lastPanelPosOnScreen;

        // private bool m_isGeometryValid = false; // <-- Prevents frame-1 math errors

        private void Awake()
        {
            // this.LogWarn("++");
            Instance = this;
        }

        void OnEnable()
        {
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            if (version == m_UiVersion)
                return;
            m_UiVersion = version;
            m_RootElement = rootElement;

            m_tooltip = m_RootElement.Q<VisualElement>("TooltipRoot");
            m_titleLabel = m_RootElement.Q<Label>("TitleLabel");
            m_descriptionLabel = m_RootElement.Q<Label>("DescriptionLabel");

            m_tooltip.style.display = DisplayStyle.None;
            m_tooltip.style.visibility = Visibility.Hidden;
        }

        public void Show(TooltipData data, Vector2 position, bool isScreenPosition = true)
        {
            this.LTrace(() => $"screenPosition: {position}");

            if (string.IsNullOrEmpty(data.Title))
            {
                m_titleLabel.style.display = DisplayStyle.None;
            }
            else
            {
                m_titleLabel.style.display = DisplayStyle.Flex;
                m_titleLabel.text = data.Title;
            }

            m_descriptionLabel.text = data.Description;
            // m_isGeometryValid = false; // Reset because text/size changed!

            // 1. Keep it invisible to the player's eyes for now
            m_tooltip.style.visibility = Visibility.Hidden;

            m_lastPanelPosOnScreen = isScreenPosition;
            if (m_lastPanelPosOnScreen)
            {
                m_lastPanelPos = RuntimePanelUtils.ScreenToPanel(m_tooltip.panel, position);
                m_tooltip.style.top = m_tooltip.panel.visualTree.localBound.height - m_lastPanelPos.y - m_offset.y;
            }
            else
            {
                m_lastPanelPos = position;
                m_tooltip.style.top = m_lastPanelPos.y + m_offset.y;
            }

            m_tooltip.style.left = m_lastPanelPos.x + m_offset.x;

            RegisterTooltipGeometryChanged();

            // 4. Turn on display. This forces Unity to compute the size behind the scenes
            m_tooltip.style.display = DisplayStyle.Flex;
            m_tooltip.style.visibility = Visibility.Visible;
        }

        public void Hide()
        {
            // m_isGeometryValid = false;
            m_tooltip.style.display = DisplayStyle.None;
        }

        private void UnregisterTooltipGeometryChanged()
        {
            m_tooltip.UnregisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
        }

        private void RegisterTooltipGeometryChanged()
        {
            // unregister old
            UnregisterTooltipGeometryChanged();
            m_tooltip.RegisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
        }

        private void OnTooltipGeometryChanged(GeometryChangedEvent evt)
        {
            UnregisterTooltipGeometryChanged();
            // m_isGeometryValid = true;
            AdjustAndApplyPosition();
            m_tooltip.style.visibility = Visibility.Visible;
        }

        private void AdjustAndApplyPosition()
        {
            float tooltipWidth = m_tooltip.resolvedStyle.width;
            float tooltipHeight = m_tooltip.resolvedStyle.height;
            float panelWidth = m_tooltip.panel.visualTree.resolvedStyle.width;
            float panelHeight = m_tooltip.panel.visualTree.resolvedStyle.height;

            float uiX = m_lastPanelPos.x + m_offset.x;
            float uiY = m_lastPanelPosOnScreen
                ? panelHeight - m_lastPanelPos.y - m_offset.y
                : m_lastPanelPos.y + m_offset.y;
            m_tooltip.style.left = Mathf.Clamp(uiX, 0, panelWidth - tooltipWidth);
            m_tooltip.style.top = Mathf.Clamp(uiY, 0, panelHeight - tooltipHeight);
        }
    }
}
