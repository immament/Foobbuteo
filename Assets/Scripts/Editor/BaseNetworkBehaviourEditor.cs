// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Common;
using UnityEditor;

[CustomEditor(typeof(BaseMonoBehaviour), true)]
public class BaseMonoBehaviourEditor : BaseBehaviourEditor { }

[CustomEditor(typeof(BaseNetworkBehaviour), true)]
public class BaseNetworkBehaviourEditor : BaseBehaviourEditor { }

public class BaseBehaviourEditor : Editor
{
    private bool m_showDevOptions;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_logLevel",
            "m_debug",
            "m_logClassColor",
            "m_logUnityObjColor",
            "m_logCategories"
        );

        EditorGUILayout.Space();
        m_showDevOptions = EditorGUILayout.BeginFoldoutHeaderGroup(m_showDevOptions, "Dev options:");
        if (m_showDevOptions)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_debug"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_logLevel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_logCategories"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_logClassColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_logUnityObjColor"));
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();
    }
}
