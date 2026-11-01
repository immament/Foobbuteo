// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Shared.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Imma.Foobbuteo.Client.Game
{
    public class GoalZoneMB : MonoBehaviour, IPointerClickHandler
    {
        private float m_lastClickTime;

        [SerializeField]
        private float DoubleClickDelay = 0.3f;

        private void OnDoubleClick(Vector2 worldPosition)
        {
            if (GameManager.Instance.SelectedPlayer == null)
            {
                this.LogWarn("Player not selected");
                return;
            }
            // Pitch transform
            var targetPosition = transform.parent.InverseTransformPoint(worldPosition).WorldToLogic();
            GameManager.Instance.SelectedPlayer.ActionCtx.ExecuteAction(targetPosition);
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
