// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;

namespace Imma.Foobbuteo.Client
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TargetMarkerMB : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer m_spriteRenderer;

        [SerializeField]
        private Sprite m_passTargetSprite;

        [SerializeField]
        private Sprite m_moveTargetSprite;

        [SerializeField]
        private Sprite m_shotTargetSprite;

        private Sprite GetSprite(PlayerActionEnum actionType)
        {
            switch (actionType)
            {
                case PlayerActionEnum.Move:
                    return m_moveTargetSprite;
                case PlayerActionEnum.LowPass:
                case PlayerActionEnum.HighPass:
                    return m_passTargetSprite;
                case PlayerActionEnum.Shot:
                    return m_shotTargetSprite;
            }
            return null;
        }

        public void ShowTargetMarker(Vector3? localPosition, PlayerActionEnum actionType)
        {
            if (localPosition.HasValue)
            {
                m_spriteRenderer.sprite = GetSprite(actionType);
                transform.localPosition = localPosition.Value;
                gameObject.SetActive(true);
            }
            else
            {
                HideTargetMarker();
            }
        }

        public void HideTargetMarker()
        {
            gameObject.SetActive(false);
        }
    }
}
