// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections;
using System.Collections.Generic;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using Tests.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.Client
{
    public class AndroidUiLayoutTests
    {
        [UnityTest]
        public IEnumerator Lobby_PhoneSizesKeepToolbarAndScrollableDetailsInsideViewport()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(30, 30, 1000, 950);
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                    .CloneTree();
                TestUtils.ClearBindingsExcept(root);
                root.Q("main-view__login").style.display = DisplayStyle.None;
                root.Q("main-view__lobby").style.display = DisplayStyle.Flex;
                var main = root.Q("main-view");
                main.style.position = Position.Absolute;
                window.rootVisualElement.Add(root);
                window.Show();
                var tools = root.Q("info__tools");
                var details = root.Q<ScrollView>("details-container__game-data");
                foreach (var size in new[] { new Vector2(393, 839), new Vector2(839, 393) })
                {
                    UiScreenLayout.TryCreate(size, true, true, out var layout);
                    LobbyUiController.ApplyLayout(main, layout);
                    main.style.width = size.x;
                    main.style.height = size.y;
                    yield return TestUtils.WaitForUi(
                        () => Mathf.Abs(main.layout.height - size.y) < 0.1f && tools.worldBound.height > 0,
                        "The connected lobby must settle at " + size
                    );
                    foreach (var button in tools.Query<Button>().ToList())
                    {
                        Assert.LessOrEqual(
                            button.worldBound.xMax,
                            main.worldBound.xMax + 1,
                            "Lobby action " + button.name + " must fit horizontally at " + size
                        );
                        Assert.LessOrEqual(
                            button.worldBound.yMax,
                            main.worldBound.yMax + 1,
                            "Lobby action " + button.name + " must fit vertically at " + size
                        );
                    }
                    Assert.Greater(
                        details.contentViewport.layout.height,
                        0,
                        "Selected game details must retain a scrollable viewport at " + size
                    );
                    Assert.LessOrEqual(
                        root.Q("details-container__join-btn").worldBound.yMax,
                        tools.worldBound.yMin,
                        "Joining the selected game must not overlap lobby tools at " + size
                    );
                }
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator Login_RotationKeepsFieldsAndActionsReachableByScrolling()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var warnings = new List<string>();
            void OnLog(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning)
                    warnings.Add(message);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                window.position = new Rect(30, 30, 1000, 950);
                var root = AssetDatabase
                    .LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Lobby/LobbyView.tmpl.uxml")
                    .CloneTree();
                TestUtils.ClearBindingsExcept(root);
                var main = root.Q("main-view");
                main.style.position = Position.Absolute;
                root.Q("main-view__lobby").style.display = DisplayStyle.None;
                root.Q<Label>("login-status").text = "Unable to log in. Check your connection and try again.";
                window.rootVisualElement.Add(root);
                window.Show();
                var scroll = root.Q<ScrollView>("main-view__login");
                scroll.style.display = DisplayStyle.Flex;
                Assert.IsNotNull(scroll, "The login form must provide vertical scrolling on short Android screens.");

                foreach (var size in new[] { new Vector2(393, 839), new Vector2(839, 393), new Vector2(393, 839) })
                {
                    main.style.width = size.x;
                    main.style.height = size.y;
                    scroll.scrollOffset = Vector2.zero;
                    var login = root.Q<TextField>("footstar-login");
                    var submit = root.Q<Button>("login-submit");
                    var lastAction = root.Q<Button>("signup-link");
                    yield return TestUtils.WaitForUi(
                        () => Mathf.Abs(main.layout.height - size.y) < 0.1f && submit.worldBound.height > 0,
                        $"The login viewport must settle after rotation to {size} but is {main.layout.width}x{main.layout.height}"
                    );
                    Assert.GreaterOrEqual(
                        login.worldBound.yMin,
                        scroll.contentViewport.worldBound.yMin - 1,
                        "Scrolling to the top must expose the login field at " + size
                    );
                    Assert.LessOrEqual(
                        submit.worldBound.xMax,
                        scroll.contentViewport.worldBound.xMax + 1,
                        "Login controls must fit the available width at " + size
                    );
                    Assert.IsNotNull(lastAction, "The sign-up action must remain available below the login buttons.");
                    scroll.ScrollTo(lastAction);
                    yield return TestUtils.WaitForUi(
                        () => lastAction.worldBound.yMax <= scroll.contentViewport.worldBound.yMax + 1,
                        "Scrolling must expose the final login action at " + size
                    );
                    Assert.GreaterOrEqual(
                        lastAction.worldBound.yMin,
                        scroll.contentViewport.worldBound.yMin - 1,
                        "The final login action must fit fully inside the scrolled viewport at " + size
                    );
                }
                Assert.IsEmpty(warnings, "The resized login layout must not produce UI binding or style warnings.");
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
                Application.logMessageReceived -= OnLog;
            }
        }
    }
}
