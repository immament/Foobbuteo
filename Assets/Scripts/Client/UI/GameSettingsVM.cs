// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.Preferences;
using Imma.Foobbuteo.Common.dev;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class GameSettingsVM : BindableViewModel, System.IDisposable
    {
        internal const string VolumeKey = "Foobbuteo.SoundVolumePercent";
        internal const int DefaultVolume = 50;
        private readonly ClientPreferences m_preferences;
        private bool m_isAdmin;

        internal GameSettingsVM()
            : this(new ClientPreferences(new PlayerPrefsClientPreferencesStore())) { }

        internal GameSettingsVM(ClientPreferences preferences)
        {
            m_preferences = preferences;
            preferences.propertyChanged += OnPreferencesChanged;
        }

        private void OnPreferencesChanged(object sender, BindablePropertyChangedEventArgs change)
        {
            if (change.propertyName != nameof(ClientPreferences.Volume))
                return;
            Notify(nameof(Volume));
            Notify(nameof(VolumeText));
        }

        public void Dispose() => m_preferences.propertyChanged -= OnPreferencesChanged;

        [CreateProperty]
        public int Volume
        {
            get => m_preferences.Volume;
            set => m_preferences.Volume = value;
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

        internal void Refresh() => Notify(nameof(DevNetworkGuiEnabled));
    }
}
