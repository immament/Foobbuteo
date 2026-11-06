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

        private VisualElement m_tooltip;
        private Label m_titleLabel;
        private Label m_descriptionLabel;

        private Vector2 m_offset;

        private Vector2 m_lastPanelPos;
        private VisualElement m_modalScope;

        public void SetModalScope(VisualElement activePanel)
        {
            m_modalScope = activePanel;
            Hide();
        }

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
            Unbind();
            m_modalScope = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
            if (rootElement.Q("TooltipRoot") == null)
                return;
            Bind(rootElement);
        }

        internal void Bind(VisualElement rootElement)
        {
            Unbind();
            m_RootElement = rootElement;

            m_tooltip = m_RootElement.Q<VisualElement>("TooltipRoot");
            m_titleLabel = m_RootElement.Q<Label>("TitleLabel");
            m_descriptionLabel = m_RootElement.Q<Label>("DescriptionLabel");

            m_tooltip.style.display = DisplayStyle.None;
            m_tooltip.style.visibility = Visibility.Hidden;
        }

        internal void Unbind()
        {
            if (m_tooltip != null)
            {
                m_tooltip.UnregisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
                m_tooltip.style.display = DisplayStyle.None;
            }
            m_RootElement = m_tooltip = null;
            m_titleLabel = m_descriptionLabel = null;
        }

        public void Show(TooltipData data, Vector2 position, bool isScreenPosition = true, VisualElement source = null)
        {
            if (
                m_tooltip?.panel == null
                || (
                    m_modalScope != null
                    && (
                        m_modalScope.panel == null
                        || source?.panel != m_modalScope.panel
                        || (source != m_modalScope && !m_modalScope.Contains(source))
                    )
                )
            )
                return;
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

            m_lastPanelPos = isScreenPosition
                ? RuntimePanelUtils.ScreenToPanel(
                    m_tooltip.panel,
                    new Vector2(position.x, UnityEngine.Device.Screen.height - position.y)
                )
                : position;
            m_tooltip.style.top = m_lastPanelPos.y + m_offset.y;

            m_tooltip.style.left = m_lastPanelPos.x + m_offset.x;

            RegisterTooltipGeometryChanged();

            // 4. Turn on display. This forces Unity to compute the size behind the scenes
            m_tooltip.style.display = DisplayStyle.Flex;
            m_tooltip.style.visibility = Visibility.Visible;
        }

        public void Hide()
        {
            // m_isGeometryValid = false;
            if (m_tooltip != null)
            {
                m_tooltip.UnregisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
                m_tooltip.style.display = DisplayStyle.None;
            }
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
            float uiY = m_lastPanelPos.y + m_offset.y;
            Rect safeArea = new Rect(0, 0, panelWidth, panelHeight);
            if (UiViewportMetrics.TryRead(m_RootElement, out var metrics))
                safeArea = m_RootElement.LocalToWorld(metrics.SafeAreaPanelUnits);
            Vector2 local = m_tooltip.parent.WorldToLocal(
                new Vector2(
                    Mathf.Clamp(uiX, safeArea.xMin, Mathf.Max(safeArea.xMin, safeArea.xMax - tooltipWidth)),
                    Mathf.Clamp(uiY, safeArea.yMin, Mathf.Max(safeArea.yMin, safeArea.yMax - tooltipHeight))
                )
            );
            m_tooltip.style.left = local.x;
            m_tooltip.style.top = local.y;
        }
    }
}
