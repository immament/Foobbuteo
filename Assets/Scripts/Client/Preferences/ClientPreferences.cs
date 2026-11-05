// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.UI;
using Unity.Properties;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Preferences
{
    internal sealed class ClientPreferences : BindableViewModel
    {
        internal const string VolumeKey = "Foobbuteo.SoundVolumePercent";
        internal const int DefaultVolume = 50;

        private readonly IClientPreferencesStore m_store;
        private int m_volume;

        public ClientPreferences(IClientPreferencesStore store)
        {
            m_store = store ?? throw new System.ArgumentNullException(nameof(store));
            m_volume = Mathf.Clamp(store.LoadVolume(), 0, 100);
        }

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
                m_store.SaveVolume(value);
                Notify();
            }
        }
    }
}
