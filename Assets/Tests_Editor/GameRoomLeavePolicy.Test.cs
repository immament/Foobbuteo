// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public class GameRoomLeavePolicyTests
    {
        private const int k_roomId = 74001;
        private const int k_userId = 75001;
        private const string k_desktopCloseName = "GameCloseButton";
        private const string k_mobileCloseName = "pitch-close-button";

        [TestCase(UserRoles.Player, FoobbuteoGameStatus.Pending, k_roomId, true)]
        [TestCase(UserRoles.Player, FoobbuteoGameStatus.Ongoing, k_roomId, false)]
        [TestCase(UserRoles.Player, FoobbuteoGameStatus.Ended, k_roomId, true)]
        [TestCase(UserRoles.Player, FoobbuteoGameStatus.Ongoing, 0, true)]
        [TestCase(UserRoles.Player, FoobbuteoGameStatus.Ongoing, k_roomId + 1, true)]
        [TestCase(UserRoles.Admin, FoobbuteoGameStatus.Pending, k_roomId, true)]
        [TestCase(UserRoles.Admin, FoobbuteoGameStatus.Ongoing, k_roomId, true)]
        [TestCase(UserRoles.Admin, FoobbuteoGameStatus.Ended, k_roomId, true)]
        public void LeavePolicy_OnlyLocksNonAdminPlayingTheOpenOngoingGame(
            UserRoles role,
            FoobbuteoGameStatus status,
            int assignedGameId,
            bool allowed
        )
        {
            var user = CreateUser(role);
            if (assignedGameId > 0)
                user.RestoreGameSlot(new GameSlot(assignedGameId, TeamIndex.Home, 1));

            bool actual = GameRoomLeavePolicy.CanLeave(user, k_roomId, status);

            Assert.AreEqual(
                allowed,
                actual,
                $"Leave permission for {role}, {status}, assigned game {assignedGameId} must match the room rule."
            );
        }

        [UnityTest]
        public IEnumerator CloseButtons_FirstSpectatorEntry_RemainVisibleBeforeAndAfterInitialization()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var vm = ScriptableObject.CreateInstance<GameHudVM>();
            window.Show();
            var root = CreateButtons(window, vm);
            try
            {
                yield return WaitForButtons(
                    root,
                    DisplayStyle.Flex,
                    "Uninitialized room state must not hide a spectator's close buttons"
                );

                vm.User = CreateUser(UserRoles.Player);
                yield return WaitForButtons(
                    root,
                    DisplayStyle.Flex,
                    "A spectator must retain close buttons before game state arrives"
                );

                var state = new ClientGameState(k_roomId, FoobbuteoGameType.PvP);
                SetStatus(state, FoobbuteoGameStatus.Ongoing);
                vm.GameState = state;
                yield return WaitForButtons(
                    root,
                    DisplayStyle.Flex,
                    "First entry into an ongoing game as a spectator must show both close buttons"
                );

                vm.User.RestoreGameSlot(new GameSlot(k_roomId, TeamIndex.Home, 1));
                vm.User = vm.User;
                yield return WaitForButtons(
                    root,
                    DisplayStyle.None,
                    "Only claiming a player in this ongoing game must hide the spectator's close buttons"
                );
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(vm);
            }
        }

        [UnityTest]
        public IEnumerator CloseButtons_FollowClaimStartEndAndVisualTreeReload()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var vm = ScriptableObject.CreateInstance<GameHudVM>();
            var user = CreateUser(UserRoles.Player);
            var state = new ClientGameState(k_roomId, FoobbuteoGameType.PvP);
            SetStatus(state, FoobbuteoGameStatus.Pending);
            vm.GameState = state;
            vm.User = user;
            window.Show();
            var root = CreateButtons(window, vm);
            try
            {
                yield return WaitForButtons(root, DisplayStyle.Flex, "pending spectator may close both layouts");
                user.RestoreGameSlot(new GameSlot(k_roomId, TeamIndex.Home, 1));
                vm.User = user;
                yield return WaitForButtons(root, DisplayStyle.Flex, "pending assigned player may leave");

                SetStatus(state, FoobbuteoGameStatus.Ongoing);
                vm.RefreshRoomPermissions();
                yield return WaitForButtons(root, DisplayStyle.None, "ongoing assigned player has no close buttons");

                window.rootVisualElement.Clear();
                root = CreateButtons(window, vm);
                yield return WaitForButtons(root, DisplayStyle.None, "reloaded visual tree retains the lock");

                vm.User = CreateUser(UserRoles.Player);
                yield return WaitForButtons(root, DisplayStyle.Flex, "ongoing spectator may close both layouts");
                vm.User = user;
                yield return WaitForButtons(
                    root,
                    DisplayStyle.None,
                    "claiming during an ongoing game hides close buttons"
                );

                var admin = CreateUser(UserRoles.Admin);
                admin.RestoreGameSlot(new GameSlot(k_roomId, TeamIndex.Home, 1));
                vm.User = admin;
                yield return WaitForButtons(root, DisplayStyle.Flex, "assigned admin may always close");
                vm.User = user;
                SetStatus(state, FoobbuteoGameStatus.Ended);
                vm.RefreshRoomPermissions();
                yield return WaitForButtons(root, DisplayStyle.Flex, "game ending releases the room lock");
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(vm);
            }
        }

        private static VisualElement CreateButtons(EditorWindow window, GameHudVM vm)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml"
            );
            var root = tree.CloneTree();
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (element.name != k_desktopCloseName && element.name != k_mobileCloseName)
                    element.ClearBindings();
            }
            root.Q("MainLayout").dataSource = vm;
            window.rootVisualElement.Add(root);
            return root;
        }

        private static IEnumerator WaitForButtons(VisualElement root, DisplayStyle expected, string scenario)
        {
            yield return TestUtils.WaitForUi(
                () =>
                    root.Q<Button>(k_desktopCloseName).style.display.value == expected
                    && root.Q<Button>(k_mobileCloseName).style.display.value == expected,
                scenario
            );
            Assert.AreEqual(expected, root.Q<Button>(k_desktopCloseName).style.display.value, scenario);
            Assert.AreEqual(expected, root.Q<Button>(k_mobileCloseName).style.display.value, scenario);
        }

        private static void SetStatus(ClientGameState state, FoobbuteoGameStatus status) =>
            state.UpdateTurn(new TurnStateChangedMessage(status, TurnStageEnum.PlayerMove, 1, 0));

        private static ServerUser CreateUser(UserRoles role) =>
            new(76001, k_userId, k_userId + 1, "Room player", role, k_roomId);
    }
}
