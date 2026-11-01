// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEditor;
using UnityEngine;

namespace Imma.Foobbuteo.Common.dev
{
    public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool oldState = GUI.enabled;
            GUI.enabled = false;

            EditorGUI.PropertyField(position, property, label, true);

            GUI.enabled = oldState;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
}
