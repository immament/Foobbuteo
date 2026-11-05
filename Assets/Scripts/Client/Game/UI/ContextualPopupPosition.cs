// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    internal static class ContextualPopupPosition
    {
        internal static void Place(VisualElement popup, Vector2 panelAnchor, Rect safeAreaPanel)
        {
            Vector2 size = popup.worldBound.size;
            var position = new Vector2(
                Mathf.Clamp(
                    panelAnchor.x - size.x / 2,
                    safeAreaPanel.xMin,
                    Mathf.Max(safeAreaPanel.xMin, safeAreaPanel.xMax - size.x)
                ),
                Mathf.Clamp(
                    panelAnchor.y - size.y,
                    safeAreaPanel.yMin,
                    Mathf.Max(safeAreaPanel.yMin, safeAreaPanel.yMax - size.y)
                )
            );
            Vector2 local = popup.parent.WorldToLocal(position);
            popup.style.left = local.x;
            popup.style.top = local.y;
        }
    }
}
