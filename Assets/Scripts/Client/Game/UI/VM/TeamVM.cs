// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Runtime.CompilerServices;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.VM
{
    [Serializable]
    public class TeamVM : INotifyBindablePropertyChanged
    {
        public TeamVM(TeamIndex teamIdx, int teamId, string name, Sprite logo, Sprite shirt, ShirtColorEnum shirtColor)
        {
            m_teamIdx = teamIdx;
            m_teamId = teamId;
            Name = name;
            Logo = logo;
            Shirt = shirt;
            ShirtColor = shirtColor;
        }

        [SerializeField]
        private TeamIndex m_teamIdx;
        public TeamIndex TeamIdx => m_teamIdx;

        [SerializeField]
        private int m_teamId;
        public int TeamId => m_teamId;

        internal void UpdateInfo(NetFoobbuteoTeam teamInfo)
        {
            m_teamId = teamInfo.Id;
            Name = teamInfo.Name;
        }

        [SerializeField]
        private string m_name;

        [CreateProperty]
        public string Name
        {
            get => m_name;
            private set
            {
                if (m_name == value)
                    return;
                m_name = value;
                Notify();
            }
        }

        [SerializeField]
        private ShirtColorEnum m_shirtColor;

        [CreateProperty]
        public Color ShirtBackgroundColor => ShirtColors.GetColor(m_shirtColor, ShirtColors.k_TeamColorAlpha);

        public ShirtColorEnum ShirtColor
        {
            get => m_shirtColor;
            private set
            {
                m_shirtColor = value;
                ShirtTextColor = m_shirtColor switch
                {
                    ShirtColorEnum.White => Color.black,
                    _ => Color.white,
                };
            }
        }

        private Sprite m_shirt;

        [CreateProperty]
        public Sprite Shirt
        {
            get => m_shirt;
            set
            {
                if (m_shirt == value)
                    return;
                m_shirt = value;
                Notify();
            }
        }
        private Color m_shirtTextColor;

        [CreateProperty]
        public Color ShirtTextColor
        {
            get => m_shirtTextColor;
            private set
            {
                if (m_shirtTextColor == value)
                    return;
                m_shirtTextColor = value;
                Notify();
            }
        }

        private Sprite m_logo;

        [CreateProperty]
        public Sprite Logo
        {
            get => m_logo;
            set
            {
                if (m_logo == value)
                    return;
                m_logo = value;
                Notify();
            }
        }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify([CallerMemberName] string propertyName = "")
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }
    }
}
