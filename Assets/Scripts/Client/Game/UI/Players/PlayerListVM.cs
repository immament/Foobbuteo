// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerListVM : INotifyBindablePropertyChanged
    {
        public PlayerTableVM Table { get; } = new();
        public TeamIndex? Team { get; private set; }

        [CreateProperty]
        public string Title { get; private set; }

        [CreateProperty]
        public DisplayStyle Display => Team.HasValue ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool BackgroundEnabled => !Team.HasValue;
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public void Open(TeamIndex team)
        {
            Team = team;
            Notify(nameof(Display));
            Notify(nameof(BackgroundEnabled));
        }

        public void SetTitle(string title)
        {
            Title = title;
            Notify(nameof(Title));
        }

        public void Close()
        {
            Team = null;
            Notify(nameof(Display));
            Notify(nameof(BackgroundEnabled));
        }

        private void Notify(string property) =>
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
    }
}
