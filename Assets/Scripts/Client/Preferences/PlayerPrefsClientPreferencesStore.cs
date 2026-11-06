// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;

namespace Imma.Foobbuteo.Client.Preferences
{
    internal sealed class PlayerPrefsClientPreferencesStore : IClientPreferencesStore
    {
        public int LoadVolume() => PlayerPrefs.GetInt(ClientPreferences.VolumeKey, ClientPreferences.DefaultVolume);

        public void SaveVolume(int percent)
        {
            PlayerPrefs.SetInt(ClientPreferences.VolumeKey, percent);
            PlayerPrefs.Save();
        }
    }
}
