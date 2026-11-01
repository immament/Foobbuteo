// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game
{
    public class PlayerSpriteDatabase : BaseMonoBehaviour
    {
        private readonly Dictionary<ShirtColorEnum, Sprite> m_shirtSprites = new();

        private readonly Dictionary<
            (
                ShirtColorEnum shirtColor,
                bool isUser,
                bool inactive,
                bool isCaptain,
                bool isSelected,
                bool hasBall,
                bool disconnected
            ),
            Sprite
        > m_smoothPlayerSprites = new();

        private const float k_playerTokenSize = 0.16f;
        private static readonly Color s_captainStarColor = new Color(1f, 0.83f, 0.12f);
        private static readonly Vector2 s_captainStarPosition = new Vector2(0.53f, 0.53f);
        private static readonly Color s_rimColorDefault = new Color(0.08f, 0.1f, 0.14f);
        private static readonly Color s_rimColorHasBall = new Color(0.2f, 1f, 0.28f);
        private static readonly Color s_rimColorSelected = Color.white;
        private static readonly Color s_rimColorInactive = new Color(0.55f, 0.55f, 0.55f, 0.9f);
        private static readonly Color s_rimColorDisconnected = new Color(0.65f, 0.65f, 0.65f);
        internal const float InactiveOpacity = 0.78f;

        internal void Init(ClientImageCatalog images)
        {
            if (images == null)
                throw new ArgumentNullException(nameof(images));
            m_shirtSprites.Clear();
            foreach (ShirtColorEnum color in Enum.GetValues(typeof(ShirtColorEnum)))
                m_shirtSprites[color] = images.GetShirt(color);
        }

        public Sprite GetPlayerSpriteDefault(ShirtColorEnum shirtColor)
        {
            return GetPlayerSprite(shirtColor, true, false, false, false, false);
        }

        public Sprite GetPlayerSprite(
            ShirtColorEnum shirtColor,
            bool isUser,
            bool inactive,
            bool isCaptain,
            bool isSelected,
            bool hasBall,
            bool disconnected = false
        )
        {
            if (
                !m_smoothPlayerSprites.TryGetValue(
                    (shirtColor, isUser, inactive, isCaptain, isSelected, hasBall, disconnected),
                    out var smooth
                )
            )
            {
                smooth = CreateToken(
                    shirtColor,
                    isUser,
                    inactive,
                    isCaptain,
                    isSelected,
                    hasBall,
                    k_playerTokenSize,
                    disconnected
                );
                m_smoothPlayerSprites.Add(
                    (shirtColor, isUser, inactive, isCaptain, isSelected, hasBall, disconnected),
                    smooth
                );
            }
            return smooth;
        }

        public Sprite GetShirtSprite(ShirtColorEnum color)
        {
            if (m_shirtSprites.TryGetValue(color, out var sprite))
            {
                return sprite;
            }

            this.LogWarn($"Missing shirt sprite: [{color}]");
            return null;
        }

        // Draw at a higher resolution instead of magnifying the original 16px artwork.
        // Keep the original world size so prefabs, numbers and gameplay coordinates agree.
        private static Sprite CreateToken(
            ShirtColorEnum shirtColor,
            bool isUser,
            bool inactive,
            bool isCaptain,
            bool isSelected,
            bool hasBall,
            float worldSize,
            bool disconnected
        )
        {
            const int size = 128;
            const float edge = 2f / size;
            Color teamColor = ShirtColors.GetColor(shirtColor);

            Color rimColor =
                isSelected ? s_rimColorSelected
                : hasBall ? s_rimColorHasBall
                : isUser && inactive ? s_rimColorInactive
                : s_rimColorDefault;

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size) * 2f - Vector2.one;
                float radius = p.magnitude;
                float light = Mathf.Clamp01((p.y - p.x) * 0.3f + 0.5f);
                Color fill = Color.Lerp(teamColor * 0.7f, teamColor, light);
                fill = Color.Lerp(fill, Color.white, Mathf.Pow(light, 5f) * 0.22f);
                if (!isUser)
                    fill = Color.Lerp(fill, Color.white, 0.35f);

                Color pixel = Color.Lerp(rimColor, fill, Mathf.Clamp01((0.78f - radius) / edge + 0.5f));
                pixel.a = Mathf.Clamp01((0.92f - radius) / edge + 0.5f);

                // Keep an amber inner band visible underneath selection and possession highlights.
                if (isUser && inactive && radius > 0.7f && radius < 0.85f)
                    pixel = s_rimColorInactive;

                if (isUser && disconnected && radius > 0.85f && radius < 0.99f)
                {
                    float angle = Mathf.Atan2(p.y, p.x);
                    if (Mathf.Repeat(angle * 8f, Mathf.PI * 2f) < Mathf.PI)
                        pixel = s_rimColorDisconnected;
                }
                if (isCaptain)
                {
                    // Five-point gold captain star, clear of the central shirt number.
                    Vector2 badge = p - s_captainStarPosition;
                    float angle = Mathf.Atan2(badge.x, badge.y);
                    float sector = Mathf.Abs(Mathf.Repeat(angle + Mathf.PI / 5f, 2f * Mathf.PI / 5f) - Mathf.PI / 5f);
                    float starRadius =
                        0.046f
                        * Mathf.Sin(Mathf.PI / 5f)
                        / (0.10f * Mathf.Sin(Mathf.PI / 5f - sector) + 0.23f * Mathf.Sin(sector));
                    float coverage = Mathf.Clamp01((starRadius - badge.magnitude) / edge + 0.5f);
                    if (coverage > 0f)
                        pixel = Color.Lerp(pixel, s_captainStarColor, coverage);
                }
                // Activity and connection markers are independent of user ownership.
                if (isUser && inactive)
                    pixel.a *= InactiveOpacity;
                pixels[y * size + x] = pixel;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = $"token-{shirtColor}-{isUser}-{inactive}-{isCaptain}-{isSelected}-{hasBall}-{disconnected}",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size / worldSize,
                0,
                SpriteMeshType.FullRect
            );
            sprite.name = texture.name;
            return sprite;
        }

        new void OnDestroy()
        {
            foreach (Sprite sprite in m_smoothPlayerSprites.Values)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
            m_smoothPlayerSprites.Clear();
            m_shirtSprites.Clear();
            base.OnDestroy();
        }
    }
}
