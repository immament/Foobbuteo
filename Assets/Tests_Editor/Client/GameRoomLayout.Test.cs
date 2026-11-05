// SPDX-License-Identifier: AGPL-3.0-only
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class GameRoomLayoutTests
    {
        [Test]
        public void ScreenShell_ProvidesOneSharedModalBoundaryOutsideHud()
        {
            var root = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/GameRoomLayout.tmpl.uxml")
                .CloneTree();

            Assert.IsNotNull(
                root.Q("game-room-root"),
                "The game screen must have a neutral root independent of lineup editing."
            );
            Assert.AreEqual(
                1,
                root.Query<VisualElement>("modal-backdrop").ToList().Count,
                "Every game dialog must share exactly one backdrop."
            );
            Assert.IsFalse(
                root.Q("MainLayout").Contains(root.Q("modal-layer")),
                "Disabling the HUD must never disable its active modal."
            );
            Assert.IsFalse(
                root.Q("screen-content").Contains(root.Q("toast-container")),
                "Blocking game content must not disable notifications."
            );
        }
    }
}
