// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.dev
{
    public static class Formation442
    {
        public static readonly Vector2 LogicSize = new(610f, 420f);

        // index 0 = GK
        public static readonly Vector2[] Positions =
        {
            new(10f, 210f),
            new(98f, 420f - 58f),
            new(98f, 420f - 158f),
            new(98f, 158f),
            new(98f, 58f),
            new(158f, 158f),
            new(208f, 420f - 58f),
            new(158f, 420f - 158f),
            new(258f, 420f - 158f),
            new(258f, 158f),
            new(208f, 58f),
            // new(0f, 0f), // GK
            // new(-2.1f, 1.431f), // LB
            // new(-2.1f, 0.54f), // CB
            // new(-2.1f, -0.54f), // CB
            // new(-2.1f, -1.431f), // RB
            // new(-1f, 1.431f), // LM
            // new(-1.45f, 0.54f), // CM
            // new(-1.45f, -0.54f), // CM
            // new(-0.954f, -1.431f), // RM
            // new(-0.41f, 0.54f), // ST
            // new(-0.41f, -0.54f), // ST
        };

        public static Vector2 GetPosition(TeamIndex teamIdx, byte playerIdx)
        {
            if (teamIdx == TeamIndex.Home)
            {
                return Positions[playerIdx - 1];
            }
            return LogicSize - Positions[playerIdx - 1];
        }
    }
}
/*
new (0f, 200f),
new (90f, 350f),
new (90f, 250f),
new (90f, 150f),
new (90f, 50f),
new (150f, 150f),
new (200f, 350f),
new (150f, 250f),
new (250f, 250f),
new (250f, 150f),
new (200f, 50f),
*/
