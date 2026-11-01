// SPDX-License-Identifier: AGPL-3.0-only
using System;
using UnityEngine.SceneManagement;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class UiLayoutDiagnosticsController : IDisposable
    {
        private bool m_initialized;
        private bool m_configured;
        public bool Enabled { get; private set; }

        public void Initialize()
        {
            if (m_initialized)
                return;
            m_initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToLoadedScenes();
        }

        public bool ValidateStartupConfiguration(bool? enabled, out string error)
        {
            error = null;
            if (m_configured && enabled.HasValue && enabled.Value != Enabled)
            {
                error = "UI layout diagnostics cannot change until the application is restarted.";
                return false;
            }
            return true;
        }

        public void Configure(bool enabled)
        {
            if (!ValidateStartupConfiguration(enabled, out var error))
                throw new InvalidOperationException(error);
            Enabled = enabled;
            m_configured = true;
            ApplyToLoadedScenes();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene(scene);

        private void ApplyToLoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                ApplyToScene(SceneManager.GetSceneAt(index));
        }

        internal void ApplyToScene(Scene scene)
        {
            if (!scene.isLoaded)
                return;
            string targetName;
            if (scene.name == "LobbyScene")
                targetName = "PanelRenderer";
            else if (scene.name == "GameScene")
                targetName = "MainUI";
            else
                return;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var diagnostics in root.GetComponentsInChildren<UiLayoutDiagnostics>(true))
                if (diagnostics.gameObject.name == targetName)
                    diagnostics.enabled = Enabled;
        }

        public void Dispose()
        {
            if (!m_initialized)
                return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            m_initialized = false;
        }
    }
}
