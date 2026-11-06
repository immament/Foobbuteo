// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.UI.Tooltip;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Imma.Foobbuteo.Client.Game
{
    internal class PlayerMB : BaseMonoBehaviour, IPointerClickHandler, ITooltipProvider
    {
        private PlayerVM m_playerVM;
        internal PlayerVM PlayerVM => m_playerVM;
        internal int PlayerId => m_playerState.PlayerId;
        internal byte PlayerIdx => m_playerState.Slot.PlayerIdx;
        internal TeamIndex TeamIdx => m_playerState.Slot.TeamIdx;

        internal bool CanMove => m_gameManager.CanMovePlayer(this);

        private PlayerSpriteDatabase m_spriteDatabase;
        private GameManager m_gameManager;

        [SerializeField]
        private SpriteRenderer m_spriteRenderer;

        [SerializeField]
        private TextMeshPro m_numberLabel;

        [SerializeField]
        private Transform m_actionZone;

        [SerializeField]
        private Transform m_highPassZone;

        // private Vector2? m_targetPosition;

        [SerializeReference]
        private ClientPlayerState m_playerState;
        internal ClientPlayerState PlayerState => m_playerState;

        private PlayerActionContext m_actionCtx;
        internal PlayerActionContext ActionCtx => m_actionCtx;

        [SerializeField, Min(1f)]
        private float m_minTokenPixels = 28f;

        [SerializeField, Min(1f)]
        private float m_minClickPixels = 44f;

        [SerializeField, Min(1f)]
        private float m_maxVisualScale = 2f;
        private bool m_visualInitialized;
        private CircleCollider2D m_clickCollider;
        private Vector3 m_originalVisualScale;
        private Vector3 m_originalNumberScale;
        private float m_originalClickRadius;

        new void Start()
        {
            m_clickCollider = m_spriteRenderer.GetComponent<CircleCollider2D>();
            m_originalVisualScale = m_spriteRenderer.transform.localScale;
            m_originalNumberScale = m_numberLabel.transform.localScale;
            if (m_clickCollider != null)
                m_originalClickRadius = m_clickCollider.radius;
            SetVisual();
            // SetVisualState(PlayerSpritresEnum.Active_Normal);
            UpdateVisualState();
            m_visualInitialized = true;
            m_gameManager.ApplyViewportScale(this);
        }

        internal void ApplyViewportScale(float pixelsPerUnit, float density)
        {
            if (!m_visualInitialized || m_spriteRenderer.sprite == null)
                return;

            float parentScale = Mathf.Abs(m_spriteRenderer.transform.parent.lossyScale.x);
            float originalWidth =
                m_spriteRenderer.sprite.bounds.size.x * Mathf.Abs(m_originalVisualScale.x) * parentScale;

            if (originalWidth <= 0f)
            {
                return;
            }

            float scale = Mathf.Clamp(
                m_minTokenPixels * density / (originalWidth * pixelsPerUnit),
                1f,
                m_maxVisualScale
            );

            m_spriteRenderer.transform.localScale = m_originalVisualScale * scale;
            m_numberLabel.transform.localScale = m_originalNumberScale * scale;

            if (m_clickCollider != null)
            {
                float visualWorldScale = Mathf.Abs(m_spriteRenderer.transform.lossyScale.x);
                m_clickCollider.radius = Mathf.Max(
                    m_originalClickRadius,
                    m_minClickPixels * density / (2f * pixelsPerUnit * visualWorldScale)
                );
            }
        }

        internal void Init(
            GameManager gameManager,
            PlayerSpriteDatabase spriteDatabase,
            PlayerVM playerVM,
            ClientPlayerState playerState
        )
        {
            m_gameManager = gameManager;
            m_spriteDatabase = spriteDatabase;
            m_playerVM = playerVM;
            m_playerState = playerState;

            m_actionCtx = new PlayerActionContext(this, m_actionZone, m_highPassZone);
        }

        new void OnDestroy()
        {
            if (m_actionCtx != null)
            {
                m_actionCtx.Dispose();
            }
            base.OnDestroy();
        }

        private void SetVisual()
        {
            this.LogAssert(m_numberLabel != null, $"m_numberLabel: {m_numberLabel}");
            this.LogAssert(m_playerVM != null, $"m_playerVM: {m_playerVM}");
            m_numberLabel.text = m_playerVM.ShirtNumber.ToString();
            m_numberLabel.color = m_playerVM.ShirtTextColor;
        }

        private void UpdateVisualState()
        {
            m_spriteRenderer.sprite = m_spriteDatabase.GetPlayerSprite(
                m_playerVM.Team.ShirtColor,
                m_playerState.IsUser,
                m_playerState.IsInactive,
                m_playerState.IsCaptain,
                IsSelected,
                m_playerState.HasBall,
                m_playerState.IsUser && !m_playerState.IsConnected
            );
        }

        // events
        internal void SetSelected(bool selected)
        {
            m_actionCtx.SetActive(selected);
            UpdateVisualState();
        }

        internal bool IsSelected => m_gameManager.SelectedPlayer == this;

        internal Vector3 PopupAnchorPosition
        {
            get
            {
                Bounds bounds = m_spriteRenderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            // Expanded targets can overlap. Pick the closest centre among targets containing the click.
            Camera camera = eventData.pressEventCamera;
            PlayerMB nearest = this;
            float nearestDistance = float.PositiveInfinity;
            if (camera != null && camera.orthographic)
            {
                Vector2 worldPoint = camera.ScreenToWorldPoint(eventData.position);
                foreach (PlayerMB player in FindObjectsByType<PlayerMB>())
                {
                    if (
                        player.m_gameManager != m_gameManager
                        || player.m_clickCollider == null
                        || !player.m_clickCollider.enabled
                        || !player.m_clickCollider.OverlapPoint(worldPoint)
                    )
                        continue;
                    float distance = (
                        (Vector2)camera.WorldToScreenPoint(player.transform.position) - eventData.position
                    ).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = player;
                        nearestDistance = distance;
                    }
                }
            }
            m_gameManager.SelectPlayer(nearest);
        }

        public TooltipData GetTooltip()
        {
            const float skillRatio = 5f;
            var title = $"{m_playerVM.Name} <size=-5>({m_playerState.UserId}/{m_playerState.PlayerId})</size>";
            var desc =
                $@"{(m_playerState.IsInactive ? "Inactive" : string.Empty)}{(m_playerState.IsUser && !m_playerState.IsConnected ? " Disconnected" : string.Empty)}
                Zone: {m_playerState.Zone}
                Logic: {m_playerState.Position}, {transform.localPosition.WorldToLogic()}
                World: {m_playerState.Position.LogicToWorld()}  {(Vector2)transform.localPosition}
                Action Points: {Mathf.Round(m_playerState.ActionPoints)}/100
                Ball Distance: {m_playerState.BallDistance}{(m_playerState.HasBall ? " <size=+2>⚽</size>" : string.Empty)}

                {BaseSkills()}
                {(m_playerState.IsGk ? GkSkills() : FieldPlayerSkills())}";
            return new TooltipData(title, desc);

            string BaseSkills()
            {
                return $@"Speed: {m_playerState.MoveSkill * skillRatio}%
                    Confrontation: {m_playerState.ConfrontationSkill * skillRatio}%
                    Low Pass: {(m_playerState.IsGk ? m_playerState.GkPassSkill : m_playerState.LowPassSkill) * skillRatio}%
                    High Pass: {(m_playerState.IsGk ? m_playerState.GkPassSkill : m_playerState.LongPassSkill) * skillRatio}%";
            }
            string FieldPlayerSkills()
            {
                return $@"Crosses: {m_playerState.CrossesSkill * skillRatio}%
                    Shoot: {m_playerState.ShootingSkill * skillRatio}%
                    Long Shots: {m_playerState.LongShotsSkill * skillRatio}%";
            }
            string GkSkills()
            {
                return $@"Saves: {m_playerState.GkSavesSkill * skillRatio}%
                    Out Of Area: {m_playerState.OutOfAreaSkill * skillRatio}%";
            }

            // Mahmood Abu-Turk(114172)
            // Zone: 18 (350,250)
            // Ball Distance: 70
            // Action Points: 100
            // Ball Position: 300,200
            // Speed: 5%
            // Confrontation: 4%
            // Low Pass: 2%
            // High Pass: 2%
            // Crosses: 2%
            // Shoot: 3%
            // Long Shots: 3%
        }

        internal void StartPlayerTurn()
        {
            m_playerState.StartTurn();
            m_actionCtx.ClearTarget();
        }

        internal void EndPlayerTurn()
        {
            m_actionCtx.HideZones();
        }

        internal void SetHasBall(bool hasBall)
        {
            m_playerState.HasBall = hasBall;
            m_playerVM.HasBall = hasBall;
            UpdateVisualState();
        }

        internal void OnPlayerControlGranted(GameDetails_PlayerContract contract)
        {
            m_playerState.OnPlayerControlClaimed(contract);
            m_playerVM.Update(m_playerState);
            SetVisual();
            UpdateVisualState();
        }

        // ++ IClickable +++++
    }
}
