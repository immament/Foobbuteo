// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
using UnityEditor;

namespace Imma.Foobbuteo.Client
{
    public partial class ClientAppCtrl
    {
        private new void OnEnable()
        {
            base.OnEnable();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                OnApplicationQuit();
        }

        partial void UnsubscribeFromPlayModeChanges() =>
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }
}
#endif
