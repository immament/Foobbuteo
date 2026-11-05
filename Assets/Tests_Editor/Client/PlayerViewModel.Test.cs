// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Client
{
    public class PlayerViewModelTests
    {
        [Test]
        public void BallOwnershipChanged_NotifiesBothDerivedActionGroups()
        {
            var player = CreatePlayer();
            var changes = new List<string>();
            player.propertyChanged += (_, change) => changes.Add(change.propertyName);

            player.HasBall = true;

            CollectionAssert.Contains(
                changes,
                nameof(PlayerVM.ShowGkActions),
                "Ball ownership must refresh goalkeeper actions."
            );
            CollectionAssert.Contains(
                changes,
                nameof(PlayerVM.ShowGkFieldPlayerActions),
                "Ball ownership must refresh field-player actions."
            );
        }

        [Test]
        public void SelectionWithoutUser_DoesNotThrowAndNotifiesClaimPermission()
        {
            var hud = ScriptableObject.CreateInstance<GameHudVM>();
            try
            {
                var changes = new List<string>();
                hud.propertyChanged += (_, change) => changes.Add(change.propertyName);

                Assert.DoesNotThrow(
                    () => hud.SwitchPlayer(CreatePlayer()),
                    "Selection must remain safe before the user is initialized."
                );
                CollectionAssert.Contains(
                    changes,
                    nameof(GameHudVM.CanClaimPlayer),
                    "Selection must refresh claim availability."
                );
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        [Test]
        public void SelectionWithSlottedUser_NotifiesClaimPermissionOnce()
        {
            var hud = ScriptableObject.CreateInstance<GameHudVM>();
            try
            {
                var user = new GameUser();
                user.RestoreGameSlot(new GameSlot(701, TeamIndex.Home, 1));
                hud.User = user;
                var changes = new List<string>();
                hud.propertyChanged += (_, change) => changes.Add(change.propertyName);

                hud.SwitchPlayer(CreatePlayer());

                Assert.AreEqual(
                    1,
                    changes.FindAll(name => name == nameof(GameHudVM.CanClaimPlayer)).Count,
                    "A slotted user's new selection must refresh claim permission once."
                );
                Assert.AreEqual(
                    1,
                    changes.FindAll(name => name == nameof(GameHudVM.SelectedPlayer)).Count,
                    "A selection transition must not publish duplicate selection notifications."
                );
            }
            finally
            {
                Object.DestroyImmediate(hud);
            }
        }

        private static PlayerVM CreatePlayer() =>
            new(
                2,
                new TeamVM(TeamIndex.Home, 801, "Home", null, null, ShirtColorEnum.Blue),
                new ClientPlayerState(new GameSlot(701, TeamIndex.Home, 2))
            );
    }
}
