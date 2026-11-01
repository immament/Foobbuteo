// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class ServerMatchAudioBuildProcessor : IProcessSceneWithReport
{
    // The client assembly is excluded from server players, so identify its audio object by name.
    internal const string AudioObjectName = "ClientMatchAudio";
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (
            report == null
            || report.summary.platformGroup != BuildTargetGroup.Standalone
            || report.summary.GetSubtarget<StandaloneBuildSubtarget>() != StandaloneBuildSubtarget.Server
        )
            return;
        StripClientAssets(scene);
    }

    internal static void StripClientAssets(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != null && transform.name == AudioObjectName)
                    Object.DestroyImmediate(transform.gameObject);
            }
            if (root == null)
                continue;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                    continue;
                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    if (
                        AssetDatabase.GetAssetPath(property.objectReferenceValue)
                        == "Assets/ClientAssets/ClientImageCatalog.asset"
                    )
                        property.objectReferenceValue = null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
