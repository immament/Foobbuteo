// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024�2026 Dariusz Ju�czyk

using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game
{
    internal static class ShirtColors
    {
        public static Color GetColor(ShirtColorEnum shirtColor) =>
            shirtColor switch
            {
                ShirtColorEnum.Blue => new Color(0.08f, 0.32f, 0.9f),
                ShirtColorEnum.Red => new Color(0.9f, 0.08f, 0.1f),
                ShirtColorEnum.Green => new Color(0.05f, 0.55f, 0.22f),
                ShirtColorEnum.Orange => new Color(0.95f, 0.36f, 0.04f),
                ShirtColorEnum.Black => new Color(0.12f, 0.14f, 0.18f),
                ShirtColorEnum.White => new Color(0.92f, 0.94f, 0.97f),
                ShirtColorEnum.Brown => new Color(0.43f, 0.22f, 0.1f),
                _ => new Color(0.52f, 0.17f, 0.75f),
            };
    }
}
