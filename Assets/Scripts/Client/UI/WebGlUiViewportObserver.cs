// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class WebGlUiViewportObserver : IDisposable
    {
        private delegate void ViewportCallback(int id, float width, float height, float cssWidth, float cssHeight);

        private static readonly Dictionary<int, Action<Vector2, Vector2>> s_callbacks = new();
        private static readonly ViewportCallback s_callback = OnViewportChanged;
        private static int s_nextId;
        private int m_id;

        [DllImport("__Internal")]
        private static extern void ObserveUiCanvas(int id, ViewportCallback callback);

        [DllImport("__Internal")]
        private static extern void UnobserveUiCanvas(int id);

        internal WebGlUiViewportObserver(Action<Vector2, Vector2> changed)
        {
            m_id = ++s_nextId;
            s_callbacks.Add(m_id, changed);
            ObserveUiCanvas(m_id, s_callback);
        }

        [MonoPInvokeCallback(typeof(ViewportCallback))]
        private static void OnViewportChanged(int id, float width, float height, float cssWidth, float cssHeight)
        {
            if (s_callbacks.TryGetValue(id, out var callback))
                callback(new Vector2(width, height), new Vector2(cssWidth, cssHeight));
        }

        public void Dispose()
        {
            if (m_id == 0)
                return;
            s_callbacks.Remove(m_id);
            UnobserveUiCanvas(m_id);
            m_id = 0;
        }
    }
}
#endif
