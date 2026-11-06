// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Client.UI;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerListVM : BindableViewModel
    {
        public PlayerTableVM Table { get; } = new();
        public TeamIndex? Team { get; private set; }

        [CreateProperty]
        public string Title { get; private set; }

        public void Open(TeamIndex team)
        {
            Team = team;
        }

        public void SetTitle(string title)
        {
            Title = title;
            Notify(nameof(Title));
        }

        public void Close()
        {
            Team = null;
        }
    }
}
