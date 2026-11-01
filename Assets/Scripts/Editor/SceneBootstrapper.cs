// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEditor;
using UnityEditor.SceneManagement;

namespace Imma.Foobbuteo.Editor
{
    /// <summary>
    /// From "Unity - Boss Room" (com.unity.multiplayer.samples.coop)
    /// Class that permits auto-loading a bootstrap scene when the editor switches play state. This class is
    /// initialized when Unity is opened and when scripts are recompiled. This is to be able to subscribe to
    /// EditorApplication's playModeStateChanged event, which is when we wish to open a new scene.
    /// </summary>
    /// <remarks>
    /// A critical edge case scenario regarding NetworkManager is accounted for here.
    /// A NetworkObject's GlobalObjectIdHash value is currently generated in OnValidate() which is invoked during a
    /// build and when the asset is loaded/viewed in the editor.
    /// If we were to manually open Bootstrap scene via EditorSceneManager.OpenScene(...) as the editor is exiting play
    /// mode, Bootstrap scene would be entering play mode within the editor prior to having loaded any assets, meaning
    /// NetworkManager itself has no entry within the AssetDatabase cache. As a result of this, any referenced Network
    /// Prefabs wouldn't have any entry either.
    /// To account for this necessary AssetDatabase step, whenever we're redirecting from a new scene, or a scene
    /// existing in our EditorBuildSettings, we forcefully stop the editor, open Bootstrap scene, and re-enter play
    /// mode. This provides the editor the chance to create AssetDatabase cache entries for the Network Prefabs assigned
    /// to the NetworkManager.
    /// If we are entering play mode directly from Bootstrap scene, no additional steps need to be taken and the scene
    /// is loaded normally.
    /// </remarks>
    ///
    [InitializeOnLoad]
    public static class SceneBootstrapper
    {
        const string k_PreviousSceneKey = "PreviousScene";
        const string k_ShouldLoadBootstrapSceneKey = "fbt_LoadBootstrapScene";

        const string k_LoadBootstrapSceneOnPlay = "Foobbuteo/Load Bootstrap Scene On Play/Enable";
        const string k_DoNotLoadBootstrapSceneOnPlay = "Foobbuteo/Load Bootstrap Scene On Play/Disable";

        static bool s_RestartingToSwitchScene;

        static string BootstrapScene => EditorBuildSettings.scenes[0].path;

        // to track where to go back to
        static string PreviousScene
        {
            get => EditorPrefs.GetString(k_PreviousSceneKey);
            set => EditorPrefs.SetString(k_PreviousSceneKey, value);
        }

        static bool ShouldLoadBootstrapScene
        {
            get
            {
                if (!EditorPrefs.HasKey(k_ShouldLoadBootstrapSceneKey))
                {
                    EditorPrefs.SetBool(k_ShouldLoadBootstrapSceneKey, true);
                }

                return EditorPrefs.GetBool(k_ShouldLoadBootstrapSceneKey, true);
            }
            set => EditorPrefs.SetBool(k_ShouldLoadBootstrapSceneKey, value);
        }

        static SceneBootstrapper()
        {
            EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
        }

        [MenuItem(k_LoadBootstrapSceneOnPlay, true)]
        static bool ShowLoadBootstrapSceneOnPlay()
        {
            return !ShouldLoadBootstrapScene;
        }

        [MenuItem(k_LoadBootstrapSceneOnPlay)]
        static void EnableLoadBootstrapSceneOnPlay()
        {
            ShouldLoadBootstrapScene = true;
        }

        [MenuItem(k_DoNotLoadBootstrapSceneOnPlay, true)]
        static bool ShowDoNotLoadBootstrapSceneOnPlay()
        {
            return ShouldLoadBootstrapScene;
        }

        [MenuItem(k_DoNotLoadBootstrapSceneOnPlay)]
        static void DisableDoNotLoadBootstrapSceneOnPlay()
        {
            ShouldLoadBootstrapScene = false;
        }

        static void EditorApplicationOnplayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (!ShouldLoadBootstrapScene)
            {
                return;
            }

            if (s_RestartingToSwitchScene)
            {
                if (playModeStateChange == PlayModeStateChange.EnteredPlayMode)
                {
                    // for some reason there's multiple start and stops events happening while restarting the editor playmode. We're making sure to
                    // set stoppingAndStarting only when we're done and we've entered playmode. This way we won't corrupt "activeScene" with the multiple
                    // start and stop and will be able to return to the scene we were editing at first
                    s_RestartingToSwitchScene = false;
                }
                return;
            }

            if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
            {
                OnExitingEditMode();
                return;
            }
            if (
                playModeStateChange == PlayModeStateChange.EnteredEditMode
                && !string.IsNullOrEmpty(PreviousScene)
                && !PreviousScene.Equals(EditorSceneManager.GetActiveScene().path)
            )
            {
                EditorSceneManager.OpenScene(PreviousScene);
            }
        }

        private static void OnExitingEditMode()
        {
            // cache previous scene so we return to this scene after play session, if possible
            PreviousScene = EditorSceneManager.GetActiveScene().path;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // user either hit "Save" or "Don't Save"; open bootstrap scene

                if (
                    !string.IsNullOrEmpty(BootstrapScene)
                    && System.Array.Exists(EditorBuildSettings.scenes, scene => scene.path == BootstrapScene)
                )
                {
                    var activeScene = EditorSceneManager.GetActiveScene();

                    s_RestartingToSwitchScene =
                        activeScene.path == string.Empty || !BootstrapScene.Contains(activeScene.path);

                    // we only manually inject Bootstrap scene if we are in a blank empty scene,
                    // or if the active scene is not already BootstrapScene
                    if (s_RestartingToSwitchScene)
                    {
                        if (EditorApplication.isPlaying)
                            EditorApplication.isPlaying = false;
                        // scene is included in build settings; open it
                        EditorSceneManager.OpenScene(BootstrapScene);
                    }
                }
            }
            else
            {
                // user either hit "Cancel" or exited window; don't open bootstrap scene & return to editor
                EditorApplication.isPlaying = false;
            }
        }
    }
}
