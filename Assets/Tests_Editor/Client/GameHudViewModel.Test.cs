// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.Preferences;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Client
{
    public class GameHudViewModelTests
    {
        [Test]
        public void RuntimeHud_StartsFreshWithoutMutatingTemplate()
        {
            var template = ScriptableObject.CreateInstance<GameHudVM>();
            template.ScoreA = 9;
            template.TimerText = "Previous match";
            var runtime = GameHudVM.CreateRuntime(template);
            try
            {
                Assert.AreEqual(
                    0,
                    runtime.ScoreA,
                    "A runtime HUD must not inherit a previous score from authoring data."
                );
                Assert.IsFalse(runtime.IsSessionReady, "A new HUD must wait for fresh session details.");
                Assert.AreEqual(9, template.ScoreA, "Initializing the runtime HUD must preserve the template.");
                Assert.AreNotEqual(
                    "Previous match",
                    runtime.TimerText,
                    "Previous timer text must not leak into a new match."
                );
            }
            finally
            {
                Object.DestroyImmediate(runtime);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void SharedPreferences_RefreshBothSettingsAdapters()
        {
            var preferences = new ClientPreferences(new Store());
            using var first = new GameSettingsVM(preferences);
            using var second = new GameSettingsVM(preferences);
            int changes = 0;
            second.propertyChanged += (_, change) =>
            {
                if (change.propertyName == nameof(GameSettingsVM.VolumeText))
                    changes++;
            };

            first.Volume = 72;

            Assert.AreEqual(
                "72%",
                second.VolumeText,
                "Every settings adapter must reflect the same preference source."
            );
            Assert.AreEqual(1, changes, "Another adapter's write must notify the displayed percentage once.");
        }

        private sealed class Store : IClientPreferencesStore
        {
            public int LoadVolume() => 50;

            public void SaveVolume(int percent) { }
        }
    }
}
