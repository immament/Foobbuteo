// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine;

namespace Imma.Foobbuteo.Client.UI.Help
{
    [System.Serializable]
    public class HelpItemData
    {
        public Sprite image;
        public string title;

        [TextArea]
        public string description;
    }
}
