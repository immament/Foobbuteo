// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using Imma.Foobbuteo.Client.Game.UI;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class ContextualPopupTests
    {
        [UnityTest]
        public IEnumerator WorldAnchor_AccountsForParentTransformAndClampsToSafeEdges()
        {
            const float pixelAlignmentTolerance = 0.75f;
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(20, 20, 700, 500);
                window.Show();
                var parent = new VisualElement();
                parent.style.position = Position.Absolute;
                parent.style.left = 100;
                parent.style.top = 40;
                parent.style.width = 500;
                parent.style.height = 400;
                parent.style.scale = new Scale(new Vector3(1.2f, 1.2f, 1));
                var popup = new VisualElement();
                popup.style.position = Position.Absolute;
                popup.style.width = 100;
                popup.style.height = 40;
                parent.Add(popup);
                window.rootVisualElement.Add(parent);
                yield return TestUtils.WaitForUi(
                    () => popup.worldBound.width > 100,
                    "Popup geometry must include its transformed parent's scale."
                );
                var safe = new Rect(0, 0, 700, 500);
                var anchor = new Vector2(320, 220);

                ContextualPopupPosition.Place(popup, anchor, safe);
                yield return TestUtils.WaitForUi(
                    () =>
                        Mathf.Abs(popup.worldBound.center.x - anchor.x) < pixelAlignmentTolerance
                        && Mathf.Abs(popup.worldBound.yMax - anchor.y) < pixelAlignmentTolerance,
                    () =>
                        "The popup must remain centered above the panel-space world anchor under a transformed parent. Actual popup="
                        + popup.worldBound
                        + ", parent="
                        + parent.worldBound
                        + ", popup layout="
                        + popup.layout
                        + ", local anchor="
                        + parent.WorldToLocal(anchor)
                );
                ContextualPopupPosition.Place(popup, Vector2.zero, safe);
                yield return TestUtils.WaitForUi(
                    () =>
                        popup.worldBound.xMin >= -pixelAlignmentTolerance
                        && popup.worldBound.yMin >= -pixelAlignmentTolerance,
                    "A popup near the top-left anchor must stay within safe bounds."
                );
                Assert.LessOrEqual(
                    popup.worldBound.xMax,
                    safe.xMax + pixelAlignmentTolerance,
                    "Clamping must preserve the right safe edge."
                );
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }
    }
}
