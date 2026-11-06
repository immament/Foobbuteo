// SPDX-License-Identifier: AGPL-3.0-only
using Imma.Foobbuteo.Client.Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class GameSceneUiTests
    {
        [Test]
        public void DensityOwner_SharesRuntimeSettingsAndRestoresBothSources()
        {
            var mainObject = new GameObject("Main");
            var tooltipObject = new GameObject("Tooltip");
            var ownerObject = new GameObject("Settings owner");
            ownerObject.SetActive(false);
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var main = mainObject.AddComponent<PanelRenderer>();
                var tooltip = tooltipObject.AddComponent<PanelRenderer>();
                main.panelSettings = tooltip.panelSettings = original;
                main.sortingOrder = 0;
                tooltip.sortingOrder = 1;
                var owner = ownerObject.AddComponent<GamePanelSettingsRuntime>();
                owner.Configure(main, tooltip);

                ownerObject.SetActive(true);
                owner.Acquire();

                Assert.AreNotSame(
                    original,
                    main.panelSettings,
                    "Game density changes must use transient settings, preserving the authoring asset."
                );
                Assert.AreSame(
                    main.panelSettings,
                    tooltip.panelSettings,
                    "Both Game renderers must use the same runtime panel and density policy."
                );
                Assert.AreEqual(1, tooltip.sortingOrder, "Sharing settings must preserve tooltip renderer order.");
                var runtime = main.panelSettings;
                ownerObject.SetActive(false);
                owner.Release();
                Assert.AreSame(original, main.panelSettings, "Disabling the owner must restore MainUI's source asset.");
                Assert.AreSame(
                    original,
                    tooltip.panelSettings,
                    "Disabling the owner must restore TooltipUI's source asset."
                );
                Assert.IsTrue(
                    runtime == null,
                    "The runtime clone must be released after both renderers restore their sources."
                );
                ownerObject.SetActive(true);
                owner.Acquire();
                Assert.AreNotSame(
                    original,
                    main.panelSettings,
                    "Re-enabling must acquire a fresh shared runtime instance."
                );
                owner.Release();
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(mainObject);
                Object.DestroyImmediate(tooltipObject);
                Object.DestroyImmediate(original);
            }
        }
    }
}
