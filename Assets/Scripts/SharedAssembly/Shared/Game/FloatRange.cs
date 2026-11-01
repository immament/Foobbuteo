// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Shared.Game
{
    public struct FloatRange
    {
        public float Min { get; }
        public float Max { get; }

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(float value) => Min <= value && value <= Max;

        public bool ContainsExclusive(float value) => Min < value && value < Max;
    }
}
