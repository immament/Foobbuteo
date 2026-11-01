// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEditor;
using UnityEngine;

public static class DevSaveGameRoomPrefab
{
    [MenuItem("Foobbuteo/Save Game Room Prefab")]
    private static void Save()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode first.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Net/GameRoomPrefab.prefab");

        if (prefab == null)
        {
            Debug.LogError("GameRoomPrefab was not found.");
            return;
        }

        if (!prefab.TryGetComponent<Unity.Netcode.NetworkObject>(out var networkObject))
        {
            Debug.LogError("GameRoomPrefab has no NetworkObject.");
            return;
        }

        EditorUtility.SetDirty(networkObject);
        PrefabUtility.SavePrefabAsset(prefab, out bool success);
        Debug.Log(success ? "GameRoomPrefab saved." : "Prefab save failed.");
    }
}
