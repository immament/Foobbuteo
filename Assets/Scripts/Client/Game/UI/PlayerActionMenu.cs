// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Game.Contract;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    [UxmlElement]
    public partial class PlayerActionMenu : VisualElement
    {
        private readonly KeyButton<PlayerActionEnum>[] m_buttons;
        private PlayerActionEnum m_selectedKey = PlayerActionEnum.Move;

        public PlayerActionMenu()
        {
            this.LTrace("++");
            AddToClassList("toggle-button-group");

            var isPlayerSelectedBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(PlayerVM.PlayerId)),
            };
            var hasBallBinding = new DataBinding { dataSourcePath = new PropertyPath(nameof(PlayerVM.HasBall)) };

            m_buttons = new[]
            {
                CreateButton("MoveButton", PlayerActionEnum.Move, isPlayerSelectedBinding, "Move"),
                CreateButton("PassButton", PlayerActionEnum.LowPass, hasBallBinding, "Low Pass"),
                CreateButton("LongPassButton", PlayerActionEnum.HighPass, hasBallBinding, "High Pass / Cross"),
                CreateButton("ShotButton", PlayerActionEnum.Shot, hasBallBinding, "Shoot"),
            };

            foreach (var button in m_buttons)
                Add(button);

            UpdateClasses();
        }

        [UxmlAttribute]
        [CreateProperty]
        public PlayerActionEnum SelectedKey
        {
            get => m_selectedKey;
            set
            {
                this.LTrace(() => $"{m_selectedKey} => {value}");
                if (m_selectedKey == value)
                    return;

                m_selectedKey = value;
                NotifyPropertyChanged(nameof(SelectedKey));
                UpdateClasses();
            }
        }

        private KeyButton<PlayerActionEnum> CreateButton(
            string name,
            PlayerActionEnum key,
            DataBinding binding,
            string tooltip
        )
        {
            var btn = new KeyButton<PlayerActionEnum>(() => SelectedKey = key, key)
            {
                name = name,
                tooltip = tooltip,
                text = key switch
                {
                    PlayerActionEnum.LowPass => "Low Pass",
                    PlayerActionEnum.HighPass => "High Pass",
                    _ => tooltip,
                },
            };
            btn.AddToClassList("toggle-button", "fbt-btn", "fbt-btn-solid", "fbt-theme-primary", "btn-icon-rect");
            btn.SetBinding("style.display", binding);
            return btn;
        }

        private void UpdateClasses()
        {
            foreach (var btn in m_buttons)
            {
                btn.EnableInClassList("selected", btn.Key == m_selectedKey);
            }
        }
    }
}
