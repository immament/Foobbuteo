// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Game.Contract;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI
{
    [UxmlElement]
    public partial class PlayerModeMenu : VisualElement
    {
        private readonly KeyButton<PlayerModeEnum>[] m_buttons;
        private PlayerModeEnum m_selectedKey = PlayerModeEnum.Confrontation;

        public PlayerModeMenu()
        {
            AddToClassList("toggle-button-group");

            var showFieldPlayerBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(PlayerVM.ShowGkFieldPlayerActions)),
            };
            var showGkBinding = new DataBinding { dataSourcePath = new PropertyPath(nameof(PlayerVM.ShowGkActions)) };

            m_buttons = new[]
            {
                CreateButton(
                    "ConfrontationButton",
                    PlayerModeEnum.Confrontation,
                    showFieldPlayerBinding,
                    "Confrontation"
                ),
                CreateButton(
                    "InterceptionButton",
                    PlayerModeEnum.Interception,
                    showFieldPlayerBinding,
                    "Intercept Passes"
                ),
                CreateButton("GkSaveButton", PlayerModeEnum.GkSave, showGkBinding, "Saves"),
                CreateButton("GkOutOfAreaButton", PlayerModeEnum.GkOutOfArea, showGkBinding, "Out of Area"),
                CreateButton("GkFieldPlayerButton", PlayerModeEnum.GkFieldPlayer, showGkBinding, "Field Player"),
            };

            foreach (var button in m_buttons)
                Add(button);

            UpdateClasses();
        }

        [UxmlAttribute]
        [CreateProperty]
        public PlayerModeEnum SelectedKey
        {
            get => m_selectedKey;
            set
            {
                if (m_selectedKey == value)
                    return;

                m_selectedKey = value;

                NotifyPropertyChanged(nameof(SelectedKey));
                UpdateClasses();
            }
        }

        private KeyButton<PlayerModeEnum> CreateButton(
            string name,
            PlayerModeEnum key,
            DataBinding binding,
            string tooltip
        )
        {
            var btn = new KeyButton<PlayerModeEnum>(() => SelectedKey = key, key) { name = name, tooltip = tooltip };
            btn.text = key switch
            {
                PlayerModeEnum.Confrontation => "Tackle",
                PlayerModeEnum.Interception => "Intercept",
                PlayerModeEnum.GkOutOfArea => "Rush",
                PlayerModeEnum.GkFieldPlayer => "Field",
                _ => tooltip,
            };
            btn.AddToClassList("toggle-button", "btn-icon-rect");
            btn.SetBinding("style.display", binding);
            return btn;
        }

        private void UpdateClasses()
        {
            for (int i = 0; i < m_buttons.Length; i++)
            {
                m_buttons[i].EnableInClassList("selected", m_buttons[i].Key == m_selectedKey);
            }
        }
    }
}
