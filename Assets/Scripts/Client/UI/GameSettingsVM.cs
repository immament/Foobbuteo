// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

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
        public DisplayStyle Display => m_open ? DisplayStyle.Flex : DisplayStyle.None;

        [CreateProperty]
        public bool BackgroundEnabled => !m_open;

        public void SetOpen(bool open)
        {
            m_open = open;
            Notify(nameof(Display));
            Notify(nameof(BackgroundEnabled));
        }

        private void Notify(string name) => propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(name));
    }
}
