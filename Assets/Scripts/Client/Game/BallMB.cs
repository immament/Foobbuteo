// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.UI.Tooltip;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game;
using UnityEngine;

namespace Imma.Foobbuteo.Client
{
    public class BallMB : BaseMonoBehaviour, ITooltipProvider
    {
        private const string k_tooltipTitle = "<b>Ball</b>";

        internal void MoveToLogicPosition(Vector2 logicPosition)
        {
            transform.localPosition = CoordinateConverter.LogicToWorld(logicPosition);
        }

        public TooltipData GetTooltip()
        {
            var logicPosition = transform.localPosition.WorldToLogic();
            var desc =
                $@"Zone: {PitchUtils.GetZone(logicPosition)}
                Logic: {logicPosition}
                World: {transform.localPosition}";

            return new TooltipData(k_tooltipTitle, desc);
        }
    }
}
