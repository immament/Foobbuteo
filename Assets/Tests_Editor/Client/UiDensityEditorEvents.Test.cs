// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Reflection;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Client.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tests.Editor.Client
{
    public class UiDensityEditorEventsTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Notifications_CoalesceAndCancelWhenLastSubscriberLeaves()
        {
            int notifications = 0;
            Action changed = () => notifications++;
            UiDensityEditorEvents.Changed += changed;
            try
            {
                UiDensityEditorEvents.RequestRefresh();
                UiDensityEditorEvents.RequestRefresh();
                Assert.AreEqual(0, notifications, "Focus changes must defer reads until Simulator has updated.");
                var defer = (EditorApplication.CallbackFunction)
                    Delegate.CreateDelegate(
                        typeof(EditorApplication.CallbackFunction),
                        typeof(UiDensityEditorEvents).GetMethod("DeferRefresh", PrivateStatic)
                    );
                Assert.AreEqual(
                    1,
                    Array
                        .FindAll(EditorApplication.delayCall.GetInvocationList(), callback => callback.Equals(defer))
                        .Length,
                    "A burst of events must schedule only one deferred refresh."
                );
                UiDensityEditorEvents.Changed -= changed;
                Assert.IsFalse(
                    Array.Exists(
                        EditorApplication.delayCall?.GetInvocationList() ?? Array.Empty<Delegate>(),
                        callback => callback.Equals(defer)
                    ),
                    "Removing the last subscriber must cancel its pending callback."
                );
            }
            finally
            {
                UiDensityEditorEvents.Changed -= changed;
            }
        }

        [TestCase(typeof(AndroidUiDensity))]
        [TestCase(typeof(LobbyPanelScaling))]
        public void PanelLifecycle_ReenableSubscribesOnceAndDisableRemovesListener(Type scalingType)
        {
            var owner = new GameObject("Density event ownership test");
            owner.SetActive(false);
            var original = ScriptableObject.CreateInstance<PanelSettings>();
            Component scaling = null;
            try
            {
                var renderer = owner.AddComponent<PanelRenderer>();
                renderer.panelSettings = original;
                scaling = owner.AddComponent(scalingType);
                var field = typeof(UiDensityEditorEvents).GetField("s_changed", PrivateStatic);
                int CountOwnedListeners() =>
                    Array
                        .FindAll(
                            ((Action)field.GetValue(null))?.GetInvocationList() ?? Array.Empty<Delegate>(),
                            callback => ReferenceEquals(callback.Target, scaling)
                        )
                        .Length;

                for (int cycle = 0; cycle < 2; cycle++)
                {
                    scalingType.GetMethod("OnEnable", PrivateInstance).Invoke(scaling, null);
                    Assert.AreEqual(1, CountOwnedListeners(), "Each enable must own exactly one density listener.");
                    UiDensityEditorEvents.RequestRefresh();
                    scalingType.GetMethod("OnDisable", PrivateInstance).Invoke(scaling, null);
                    Assert.AreEqual(
                        0,
                        CountOwnedListeners(),
                        "Disable must remove listeners before releasing settings."
                    );
                    Assert.AreSame(
                        original,
                        renderer.panelSettings,
                        "A queued event must not retain the runtime copy."
                    );
                }
            }
            finally
            {
                if (scaling != null)
                    scalingType.GetMethod("OnDisable", PrivateInstance).Invoke(scaling, null);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(original);
            }
        }
    }
}
