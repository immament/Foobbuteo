// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Common;
using NUnit.Framework;

namespace Tests.Editor
{
    public class NetworkObjectRegistryTests
    {
        [Test]
        public void RoomReplacement_LateDespawnDoesNotRemoveNewRoom()
        {
            using var registry = new NetworkObjectRegistry();
            var previous = new TestRoom();
            var next = new TestRoom();
            int entries = 0;
            int exits = 0;
            TestRoom active = null;
            using var binding = registry.BindLifecycle<TestRoom>(
                room =>
                {
                    entries++;
                    active = room;
                },
                () => exits++
            );
            registry.SetActive(previous);

            registry.SetActive(next);
            bool removedPrevious = registry.Unregister(previous);
            registry.SetActive(next);

            Assert.IsFalse(removedPrevious, "An old room's delayed despawn must not remove the replacement.");
            Assert.AreSame(next, active, "Replacement must remain the active room.");
            Assert.AreEqual(2, entries, "Repeated replacement with the same room must not reinitialize it.");
            Assert.AreEqual(1, exits, "Only the actual old room replacement must notify departure.");
            Assert.IsTrue(registry.Unregister(next), "The active room's own departure must succeed.");
        }

        private sealed class TestRoom : INetworkBehaviour
        {
            public bool IsSpawned => true;
        }
    }
}
