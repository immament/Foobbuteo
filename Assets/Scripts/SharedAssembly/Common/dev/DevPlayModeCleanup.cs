// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_EDITOR

using System.Diagnostics;
using Unity.Netcode;
using UnityEditor;

namespace Imma.Foobbuteo.Common.dev
{
    // [InitializeOnLoad]
    // public static class DevPlayModeCleanup
    // {
    //     static DevPlayModeCleanup()
    //     {
    //         Init();
    //     }

    //     [Conditional("UNITY_EDITOR")]
    //     static void Init()
    //     {
    //         UnityEngine.Debug.Log("[DevPlayModeCleanup] ::Init");
    //         EditorApplication.playModeStateChanged += OnStateChanged;
    //     }

    //     static void OnStateChanged(PlayModeStateChange state)
    //     {
    //         if (state == PlayModeStateChange.ExitingPlayMode)
    //         {
    //             UnityEngine.Debug.Log("[DevPlayModeCleanup] :: ExitingPlayMode");
    //             var nm = NetworkManager.Singleton;

    //             if (nm != null && nm.IsListening)
    //             {
    //                 UnityEngine.Debug.Log("[DevPlayModeCleanup] :: ExitingPlayMode Shutting down NetworkManager");
    //                 nm.Shutdown();
    //             }
    //         }
    //     }
    // }
}
#endif // UNITY_EDITOR
