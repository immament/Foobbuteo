// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Imma.Foobbuteo.Client.Game
{
    public class ActiionZoneMB : MonoBehaviour, IPointerClickHandler
    {
        private PlayerMB m_player;

        private float m_lastClickTime;

        [SerializeField]
        private float DoubleClickDelay = 0.3f;

        private void Awake()
        {
            m_player = GetComponentInParent<PlayerMB>();
        }

        private void OnDoubleClick(Vector2 worldPosition)
        {
            var targetPosition = m_player.transform.parent.InverseTransformPoint(worldPosition).WorldToLogic();
            m_player.ActionCtx.ExecuteAction(targetPosition);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            float timeSinceLastClick = Time.time - m_lastClickTime;
            if (timeSinceLastClick <= DoubleClickDelay)
            {
                OnDoubleClick(eventData.pointerPressRaycast.worldPosition);
            }

            m_lastClickTime = Time.time;
        }
    }
}
