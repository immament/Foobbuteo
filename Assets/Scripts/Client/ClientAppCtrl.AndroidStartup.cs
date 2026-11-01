// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections;
using Imma.Foobbuteo.Client.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client
{
    public partial class ClientAppCtrl
    {
        [SerializeField]
        private StartupLoadingView m_startupLoading;
        private Coroutine m_androidStartupRoutine;
        private VisualElement m_startupLobbyRoot;
        private bool m_androidStartupInitialized;

        public void StartAndroidClient()
        {
            if (m_isAndroidStartup)
                return;
            m_isAndroidStartup = true;
            if (m_startupLoading == null)
                throw new InvalidOperationException("Android startup loading view is not assigned.");
            m_startupLoading.Show();
            m_androidStartupRoutine = StartCoroutine(StartAndroidClientRoutine());
        }

        private IEnumerator StartAndroidClientRoutine()
        {
            // Present the bootstrap UI before initialization or scene loading can block the main thread.
            yield return new WaitForEndOfFrame();
            if (!TryLoadAndroidLobby(out var loading))
                yield break;
            if (loading != null)
                yield return loading;
            try
            {
                StartClient();
                m_androidStartupInitialized = true;
                var root = m_startupLobbyRoot;
                m_startupLobbyRoot = null;
                if (root != null)
                    NotifyLobbyUiReady(root);
            }
            catch (Exception exception)
            {
                FailAndroidStartup(exception);
            }
            m_androidStartupRoutine = null;
        }

        private bool TryLoadAndroidLobby(out AsyncOperation loading)
        {
            loading = null;
            try
            {
                Init();
                m_openGameId = 0;
                if (SceneManager.GetActiveScene().name != k_LobbySceneName)
                    loading = SceneManager.LoadSceneAsync(k_LobbySceneName);
                return true;
            }
            catch (Exception exception)
            {
                FailAndroidStartup(exception);
                return false;
            }
        }

        private void FailAndroidStartup(Exception exception)
        {
            Debug.LogException(exception, this);
            if (m_startupLoading != null)
                m_startupLoading.ShowError();
        }

        internal void NotifyLobbyUiReady(VisualElement root)
        {
            if (!m_isAndroidStartup || m_startupLoading == null)
                return;
            if (m_androidStartupInitialized)
                m_startupLoading.ObserveLobby(root, this);
            else
                m_startupLobbyRoot = root;
        }

        private void StopAndroidStartup()
        {
            if (m_androidStartupRoutine != null)
                StopCoroutine(m_androidStartupRoutine);
            m_androidStartupRoutine = null;
            m_startupLobbyRoot = null;
            if (m_startupLoading != null && m_isAndroidStartup)
                Destroy(m_startupLoading.gameObject);
        }
    }
}
