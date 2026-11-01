// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    public static class CoordinateConverter
    {
        public const float LogicWidth = 610f;
        public const float LogicHeight = 420f;

        public const float Left = -3.045f;
        public const float Right = 3.045f;
        public const float Bottom = -2.095f;
        public const float Top = 2.095f;

        private const float m_ratio = LogicWidth / (Right - Left);

        #region ++ Logic To World +++++++++++++
        public static float ToWorld(this float length)
        {
            return length / m_ratio;
        }

        public static Vector2 LogicToWorld(this Vector2 p)
        {
            return new Vector2(
                Mathf.LerpUnclamped(Left, Right, p.x / LogicWidth),
                -Mathf.LerpUnclamped(Bottom, Top, p.y / LogicHeight)
            );
        }

        public static Vector2 SizeToWorld(this Vector2 size)
        {
            return new Vector2(ToWorld(size.x), ToWorld(size.y));
        }

#if UNITY_EDITOR
        public static Vector2 LogicToGizmo(this Vector2 p)
        {
            return LogicToWorld(p) * 2f + new Vector2(0.27f, 0.07f) + new Vector2(-0.025f, -0.02f);
        }

        public static Vector2 SizeToGizmo(this Vector2 size)
        {
            return new Vector2(ToWorld(size.x) * 2f, ToWorld(size.y) * 2f);
        }

#endif

        // public static Vector2 LogicToWorld(Vector2 p, bool mirror)
        // {
        //     return LogicToWorld(p) * (mirror ? -1f : 1f);
        // }

        // public static Vector2 LogicToWorld(float x, float y)
        // {
        //     return new Vector2(
        //         Mathf.LerpUnclamped(Left, Right, x / LogicWidth),
        //         Mathf.LerpUnclamped(Bottom, Top, y / LogicHeight)
        //     );
        // }

        // public static Vector2 LogicToWorldClamp(float x, float y)
        // {
        //     return new Vector2(
        //         Mathf.Lerp(Left, Right, x / LogicWidth),
        //         Mathf.Lerp(Bottom, Top, y / LogicHeight)
        //     );
        // }

        #endregion -- Logic To World -------

        #region ++ World To Logic +++++++++++++

        // public static float ToLogic(float length)
        // {
        //     return length * m_ratio;
        // }

        public static Vector2 WorldToLogic(this Vector3 p)
        {
            return WorldToLogic(p.x, p.y);
        }

        public static Vector2 WorldToLogic(this Vector2 p)
        {
            return WorldToLogic(p.x, p.y);
        }

        public static Vector2 WorldToLogic(float x, float y)
        {
            return new Vector2(
                ((x - Left) / (Right - Left)) * LogicWidth,
                ((-y - Bottom) / (Top - Bottom)) * LogicHeight
            );
        }

        // public static Vector2 WorldToLogicClamp(float x, float y)
        // {
        //     var result = new Vector2(
        //         Mathf.InverseLerp(Left, Right, x) * LogicWidth,
        //         Mathf.InverseLerp(Bottom, Top, y) * LogicHeight
        //     );

        //     // ImmLogger.LTrace<CoordinateConverter>(
        //     //     $"WorldToLogic - x: {x}, y: {y}, result: {result.x}, {result.y} - lerp: {Mathf.InverseLerp(Left, Right, x)}, {Mathf.InverseLerp(Bottom, Top, y)}"
        //     // );
        //     return result;
        // }

        #endregion -- World To Logic -------
    }
}
