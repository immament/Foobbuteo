// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Common.dev;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class GameSettingsVM : INotifyBindablePropertyChanged
    {
        internal const string VolumeKey = "Foobbuteo.SoundVolumePercent";
        internal const int DefaultVolume = 50;
        private int m_volume = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, DefaultVolume), 0, 100);
        private bool m_open;
        private bool m_isAdmin;
        public event System.EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        [CreateProperty]
        public int Volume
        {
            get => m_volume;
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                if (m_volume == value)
                    return;
                m_volume = value;
                PlayerPrefs.SetInt(VolumeKey, value);
                PlayerPrefs.Save();
                Notify(nameof(Volume));
                Notify(nameof(VolumeText));
            }
        }

        [CreateProperty]
        public string VolumeText => $"{Volume}%";

        [CreateProperty]
        public DisplayStyle AdminOptionsDisplay => m_isAdmin ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool DevNetworkGuiEnabled
        {
            get => DevNetworkGUI.IsVisible;
            set
            {
                if (!m_isAdmin || DevNetworkGUI.IsVisible == value)
                    return;
                DevNetworkGUI.IsVisible = value;
                Notify(nameof(DevNetworkGuiEnabled));
            }
        }

        internal void SetAdmin(bool isAdmin)
        {
            m_isAdmin = isAdmin;
            Notify(nameof(AdminOptionsDisplay));
        }

        [CreateProperty]
        public DisplayStyle Display => m_open ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool BackgroundEnabled => !m_open;

        public void SetOpen(bool open)
        {
            m_open = open;
            Notify(nameof(Display));
            Notify(nameof(BackgroundEnabled));
            Notify(nameof(DevNetworkGuiEnabled));
        }

        private void Notify(string name) => propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(name));
    }
}
