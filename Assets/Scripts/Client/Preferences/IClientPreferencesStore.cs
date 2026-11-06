// SPDX-License-Identifier: AGPL-3.0-only
namespace Imma.Foobbuteo.Client.Preferences
{
    internal interface IClientPreferencesStore
    {
        int LoadVolume();
        void SaveVolume(int percent);
    }
}
