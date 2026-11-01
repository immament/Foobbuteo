// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_ANDROID && !UNITY_EDITOR

using UnityEngine;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class AndroidRememberedSessionStore : IRememberedSessionStore
    {
        public bool IsSupported => true;

        public RememberedSession Load()
        {
            string json = Call<string>("load");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RememberedSession>(json);
        }

        public void Save(RememberedSession session)
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass("com.imma.foobbuteo.RememberedSessionStorage");
            bridge.CallStatic("save", activity, JsonUtility.ToJson(session));
        }

        public void Clear()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass("com.imma.foobbuteo.RememberedSessionStorage");
            bridge.CallStatic("clear", activity);
        }

        private static T Call<T>(string method)
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass("com.imma.foobbuteo.RememberedSessionStorage");
            return bridge.CallStatic<T>(method, activity);
        }
    }
}
#endif
