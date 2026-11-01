// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Runtime.CompilerServices;

namespace Imma.Foobbuteo.Client.UI
{
    [System.Serializable]
    public struct Margins
    {
        public float Top;
        public float Right;
        public float Bottom;
        public float Left;

        public readonly float Horizontal => Left + Right;
        public readonly float Vertical => Top + Bottom;

        private static readonly Margins kZero = new(0f, 0f, 0f, 0f);

        public static Margins Zero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return kZero; }
        }

        public Margins(float top, float right, float bottom, float left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }
    }
}
