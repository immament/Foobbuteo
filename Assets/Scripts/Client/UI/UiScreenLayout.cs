// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;

namespace Imma.Foobbuteo.Client.UI
{
    internal readonly struct UiScreenLayout
    {
        internal const float NarrowWidth = 650f;
        internal const float ShortHeight = 520f;

        internal Vector2 LogicalSize { get; }
        internal float AspectRatio => LogicalSize.x / LogicalSize.y;
        internal bool IsPortrait => LogicalSize.x < LogicalSize.y;
        internal bool IsLandscape => !IsPortrait;
        internal bool IsNarrow => LogicalSize.x < NarrowWidth;
        internal bool IsShort => LogicalSize.y < ShortHeight;
        internal bool IsSmall => IsNarrow || IsShort;
        internal bool IsMobilePlatform { get; }
        internal bool SupportsTouch { get; }

        private UiScreenLayout(Vector2 logicalSize, bool isMobilePlatform, bool supportsTouch)
        {
            LogicalSize = logicalSize;
            IsMobilePlatform = isMobilePlatform;
            SupportsTouch = supportsTouch;
        }

        internal static bool TryCreate(
            Vector2 logicalSize,
            bool isMobilePlatform,
            bool supportsTouch,
            out UiScreenLayout layout
        )
        {
            layout = default;
            if (!IsValidSize(logicalSize))
                return false;
            layout = new UiScreenLayout(logicalSize, isMobilePlatform, supportsTouch);
            return true;
        }

        internal static bool IsValidSize(Vector2 size) =>
            size.x > 0f && size.y > 0f && !float.IsInfinity(size.x) && !float.IsInfinity(size.y);
    }
}
