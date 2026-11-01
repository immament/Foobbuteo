// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    [UxmlElement]
    public partial class KeyButton<K> : Button
        where K : Enum
    {
        public K Key { get; private set; }

        public KeyButton()
            : base() { }

        public KeyButton(Action clickEvent, K key)
            : base(clickEvent)
        {
            Key = key;
        }
    }
}
