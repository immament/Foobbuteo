using System;
using UnityEditor;
using UnityEngine;

namespace Imma.Foobbuteo.Editor
{
    [InitializeOnLoad]
    public static class AutoShowInspector
    {
        static AutoShowInspector()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static readonly Type[] InspectorAssetTypes =
        {
            typeof(Material),
            typeof(ScriptableObject),
            typeof(UnityEditorInternal.AssemblyDefinitionAsset),
            typeof(Texture2D),
        };

        private static void OnSelectionChanged()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return;
            // Debug.Log($"AutoShowInspector {selected.GetType()}");
            bool showInspector =
                selected is GameObject
                || (
                    AssetDatabase.Contains(selected)
                    && Array.Exists(InspectorAssetTypes, type => type.IsInstanceOfType(selected))
                );

            if (!showInspector)
                return;

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().FullName == "UnityEditor.InspectorWindow")
                {
                    window.Focus();
                    break;
                }
            }
        }
    }
}
