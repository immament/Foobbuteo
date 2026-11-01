// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using Imma.Common;
using Imma.Foobbuteo.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Imma.Foobbuteo.Client.UI.Tooltip
{
    public class TooltipTrigger
        : BaseMonoBehaviour,
            ITooltipProvider,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IPointerUpHandler
    {
        [SerializeField]
        string m_title;

        [SerializeField]
        private MonoBehaviour m_tooltipDataProvider;

        private ITooltipProvider TooltipProvider => m_tooltipDataProvider as ITooltipProvider;

        [SerializeField]
        string m_description;

        [SerializeField]
        private float m_tooltipDelay = 0.5f;

        [SerializeField]
        private Collider2D m_collider;

        private Coroutine m_tooltipCoroutine;
        private bool m_tooltipVisible;

        // private bool IsTouchDevice => Touchscreen.current != null;

        public TooltipData GetTooltip()
        {
            var provider = TooltipProvider;
            if (provider != null)
            {
                this.LogTrace("++");
                return provider.GetTooltip();
            }
            return new TooltipData(m_title, m_description);
        }

        new void OnEnable()
        {
            if (m_collider == null)
            {
                TryGetComponent(out m_collider);
            }
        }

        #region ++ Pointer Events ++

        public void OnPointerEnter(PointerEventData eventData)
        {
            this.LogTrace(() => $"+++, {eventData.pointerId}");
            // bool isMouse = eventData.pointerId == PointerInputModule.kMouseLeftId;

            // if (isMouse)
            StartTooltipTimer();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            this.LogTrace(() => $"+++, {eventData.pointerId}");
            StopTooltipTimer();
            HideTooltip();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            this.LogTrace(() =>
                $"+++, {eventData.pointerId}, Touchscreen deviceId={Touchscreen.current?.deviceId}, {Touchscreen.current}"
            );
            // bool isTouch = eventData.pointerId >= 0;

            // if (isTouch)
            StartTooltipTimer();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            this.LogTrace(() => $"+++, {eventData.pointerId}");
            StopTooltipTimer();
            HideTooltip();
        }

        // public void OnPointerEnter(PointerEventData eventData)
        // {
        //     this.LogTrace($"+++, {IsTouchDevice}");
        //     // Mouse hover
        //     if (!IsTouchDevice)
        //     {
        //         StartTooltipTimer();
        //     }
        // }

        // public void OnPointerExit(PointerEventData eventData)
        // {
        //     this.LogTrace($"+++ {IsTouchDevice}");
        //     StopTooltipTimer();
        //     HideTooltip();
        // }

        // public void OnPointerDown(PointerEventData eventData)
        // {
        //     this.LogTrace("+++");
        //     // Touch long press
        //     if (IsTouchDevice)
        //     {
        //         StartTooltipTimer();
        //     }
        // }

        // public void OnPointerUp(PointerEventData eventData)
        // {
        //     this.LogTrace("+++");

        //     if (IsTouchDevice)
        //     {
        //         StopTooltipTimer();
        //         HideTooltip();
        //     }
        // }

        #endregion -- Pointer Events --

        private void StartTooltipTimer()
        {
            StopTooltipTimer();
            m_tooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay());
        }

        private void StopTooltipTimer()
        {
            if (m_tooltipCoroutine != null)
            {
                StopCoroutine(m_tooltipCoroutine);
                m_tooltipCoroutine = null;
            }
        }

        private IEnumerator ShowTooltipAfterDelay()
        {
            this.LogTrace("++");
            yield return new WaitForSeconds(m_tooltipDelay);
            TooltipService.Instance.Show(GetTooltip(), GetPosition());
            m_tooltipVisible = true;
            m_tooltipCoroutine = null;
        }

        private Vector2 GetPosition()
        {
            if (m_collider == null)
            {
                return Camera.main.WorldToScreenPoint(transform.position);
            }
            Bounds bounds = m_collider.bounds;
            return Camera.main.WorldToScreenPoint(new(bounds.max.x, bounds.center.y));
        }

        private void HideTooltip()
        {
            if (!m_tooltipVisible)
                return;

            // TooltipManagerCanvas.Instance.Hide();
            TooltipService.Instance.Hide();
            m_tooltipVisible = false;
        }
    }
}
