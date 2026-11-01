// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tests.Editor.Utils
{
    public static class EditorWindowCapture
    {
        public static void Capture(EditorWindow window, string name)
        {
            Rect rect = window.position;
            var pixels = InternalEditorUtility.ReadScreenPixel(
                new Vector2(rect.x, rect.y),
                (int)rect.width,
                (int)rect.height
            );

            var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);

            texture.SetPixels(pixels);
            texture.Apply();

            var path = Path.Combine(Application.dataPath, "..\\..\\TestScreenshots", $"{name}.png");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log($"Window image captured to {path}");

            Object.DestroyImmediate(texture);
        }
    }
}
