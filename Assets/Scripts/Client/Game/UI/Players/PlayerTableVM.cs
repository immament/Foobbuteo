// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerTableVM : INotifyBindablePropertyChanged
    {
        public IReadOnlyList<PlayerListRowVM> Rows { get; private set; } = Array.Empty<PlayerListRowVM>();
        public PlayerTableViewDefinition View { get; private set; } = PlayerTableViewDefinition.AllSkills;
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public void SetRows(IEnumerable<PlayerListRowVM> rows)
        {
            Rows = rows.OrderBy(row => row.ShirtNumber ?? byte.MaxValue).ToArray();
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Rows)));
        }

        public void SetView(PlayerTableViewDefinition view)
        {
            if (View == view)
                return;
            View = view;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(View)));
        }
    }
}
