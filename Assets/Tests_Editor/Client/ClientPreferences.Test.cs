// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.Preferences;
using NUnit.Framework;

namespace Tests.Editor.Client
{
    public class ClientPreferencesTests
    {
        [TestCase(-10, 0)]
        [TestCase(87, 87)]
        [TestCase(120, 100)]
        public void Initialization_ClampsPersistedVolume(int stored, int expected)
        {
            var preferences = new ClientPreferences(new Store { Stored = stored });
            Assert.AreEqual(
                expected,
                preferences.Volume,
                "Initial volume must use the persisted percentage within the allowed range."
            );
        }

        [Test]
        public void ChangedVolume_PersistsClampedValueAndNotifiesOnlyOnce()
        {
            var store = new Store { Stored = 50 };
            var preferences = new ClientPreferences(store);
            int notifications = 0;
            preferences.propertyChanged += (_, _) => notifications++;

            preferences.Volume = 150;
            preferences.Volume = 100;

            Assert.AreEqual(100, store.Stored, "Out-of-range writes must persist the clamped percentage.");
            Assert.AreEqual(1, store.Writes, "An equivalent volume must not repeat persistence.");
            Assert.AreEqual(1, notifications, "An equivalent volume must not repeat change notifications.");
        }

        private sealed class Store : IClientPreferencesStore
        {
            internal int Stored;
            internal int Writes;

            public int LoadVolume() => Stored;

            public void SaveVolume(int percent)
            {
                Stored = percent;
                Writes++;
            }
        }
    }
}
