// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Foobbuteo.Client.UI.Tooltip;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public class TooltipModalScopeTests
    {
        [UnityTest]
        public IEnumerator ModalScope_HidesBackgroundRequestsAndRejectsOldOriginsAfterReplacement()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var serviceObject = new GameObject("Tooltip scope test");
            var service = serviceObject.AddComponent<TooltipService>();
            try
            {
                window.Show();
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Game/Tooltip.tmpl.uxml")
                    .CloneTree();
                root.style.flexGrow = 1;
                window.rootVisualElement.Add(root);
                var scope = new VisualElement();
                var source = new Button();
                scope.Add(source);
                root.Add(scope);
                service.Bind(root);
                service.SetModalScope(scope);

                service.Show(new TooltipData("World", "Blocked"), new Vector2(20, 20), false);
                Assert.AreEqual(
                    DisplayStyle.None,
                    root.Q("TooltipRoot").resolvedStyle.display,
                    "A delayed world tooltip must not appear above an active modal."
                );
                service.Show(new TooltipData("Modal", "Allowed"), new Vector2(20, 20), false, source);
                yield return TestUtils.WaitForUi(
                    () => root.Q("TooltipRoot").resolvedStyle.display == DisplayStyle.Flex,
                    "An active dialog's control must still be able to show its tooltip."
                );
                Assert.AreEqual(
                    "Allowed",
                    root.Q<Label>("DescriptionLabel").text,
                    "The displayed tooltip must belong to the active dialog."
                );

                var replacement = new VisualElement();
                root.Add(replacement);
                service.SetModalScope(replacement);
                service.Show(new TooltipData("Old", "Stale"), Vector2.zero, false, source);
                yield return TestUtils.WaitForUi(
                    () => root.Q("TooltipRoot").resolvedStyle.display == DisplayStyle.None,
                    "Replacing the modal scope must hide the old tooltip."
                );
                Assert.AreEqual(
                    "Allowed",
                    root.Q<Label>("DescriptionLabel").text,
                    "An old dialog's pending request must not overwrite live tooltip content."
                );

                service.SetModalScope(null);
                service.Show(new TooltipData("World", "Restored"), new Vector2(20, 20), false);
                yield return TestUtils.WaitForUi(
                    () =>
                        root.Q<Label>("DescriptionLabel").text == "Restored"
                        && root.Q("TooltipRoot").resolvedStyle.display == DisplayStyle.Flex,
                    "Closing modal scope must restore world tooltip requests."
                );
            }
            finally
            {
                service.Unbind();
                window.Close();
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(serviceObject);
            }
        }
    }
}
