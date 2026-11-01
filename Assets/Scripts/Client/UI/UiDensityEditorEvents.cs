// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Imma.Foobbuteo.Client.UI
{
    internal static class UiDensityEditorEvents
    {
        private static Action s_changed;
        private static bool s_pending;

        internal static event Action Changed
        {
            add
            {
                if (s_changed == null)
                    EditorWindow.windowFocusChanged += RequestRefresh;
                s_changed += value;
            }
            remove
            {
                s_changed -= value;
                if (s_changed != null)
                    return;
                EditorWindow.windowFocusChanged -= RequestRefresh;
                EditorApplication.delayCall -= DeferRefresh;
                EditorApplication.delayCall -= PublishRefresh;
                s_pending = false;
            }
        }

        internal static void RequestRefresh()
        {
            if (s_changed == null || s_pending)
                return;
            s_pending = true;
            EditorApplication.delayCall += DeferRefresh;
        }

        private static void DeferRefresh()
        {
            // SimulatorWindow switches its simulation shims in Update, after the focus event.
            // Cross an editor tick before reading Device.Application and Device.Screen.
            EditorApplication.delayCall += PublishRefresh;
        }

        private static void PublishRefresh()
        {
            s_pending = false;
            s_changed?.Invoke();
        }
    }
}
#endif
