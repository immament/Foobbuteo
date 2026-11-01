// SPDX-License-Identifier: AGPL-3.0-only
using System;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class LineupDragManipulator : PointerManipulator
    {
        private readonly LineupDragSession m_session;
        private readonly Func<bool> m_canDrag;
        private readonly Func<VisualElement> m_createPreview;
        private readonly Action<int, int> m_drop;
        private readonly int m_playerId;

        internal bool CanDrag => target.panel != null && target.enabledInHierarchy && m_canDrag();

        internal LineupDragManipulator(
            LineupDragSession session,
            int playerId,
            Func<VisualElement> createPreview,
            Func<bool> canDrag,
            Action<int, int> drop
        )
        {
            m_session = session;
            m_playerId = playerId;
            m_createPreview = createPreview;
            m_canDrag = canDrag;
            m_drop = drop;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            m_session.Cancel(this);
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnDown(PointerDownEvent evt) => m_session.TryBegin(this, evt);

        private void OnDetach(DetachFromPanelEvent evt) => m_session.Cancel(this);

        internal VisualElement CreatePreview() => m_createPreview();

        internal void Drop(int destination) => m_drop(m_playerId, destination);
    }
}
