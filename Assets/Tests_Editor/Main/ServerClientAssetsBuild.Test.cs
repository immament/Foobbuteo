// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using System.Reflection;
using Imma.Foobbuteo.Client;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.Editor.Main
{
    public sealed class ServerClientAssetsBuildTests
    {
        private const string AudioObjectName = "ClientMatchAudio";

        private sealed class CatalogHolder : MonoBehaviour
        {
            public ClientImageCatalog Images;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServerSceneProcessing_RemovesNestedAudioAndCatalogueReferences(bool inactiveParent)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("server-gameplay");
                SceneManager.MoveGameObjectToScene(root, scene);
                var holder = root.AddComponent<CatalogHolder>();
                var catalog = ClientImageCatalog.LoadForEditor();
                holder.Images = catalog;
                var audio = new GameObject(AudioObjectName);
                audio.transform.SetParent(root.transform);
                audio.AddComponent<AudioSource>();
                root.SetActive(!inactiveParent);

                var processor = EditorType("ServerMatchAudioBuildProcessor");
                processor
                    .GetMethod("StripClientAssets", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { scene });

                Assert.IsTrue(
                    audio == null,
                    "Server scene processing must remove audio beneath active or inactive parents."
                );
                Assert.IsTrue(root != null, "Server scene processing must preserve the surrounding gameplay object.");
                Assert.IsNull(holder.Images, "Server scene copies must not retain the client catalogue dependency.");
                Assert.IsNotNull(
                    catalog.GetCountryFlag(1),
                    "Stripping a scene reference must preserve the source catalogue and images."
                );
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SceneProcessing_WithoutBuildReportPreservesClientAudio()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var audio = new GameObject(AudioObjectName);
                SceneManager.MoveGameObjectToScene(audio, scene);
                var processor = Activator.CreateInstance(EditorType("ServerMatchAudioBuildProcessor"));
                ((UnityEditor.Build.IProcessSceneWithReport)processor).OnProcessScene(scene, null);
                Assert.IsTrue(
                    audio != null,
                    "Ordinary Editor scene processing must preserve client audio without a server build report."
                );
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("Assets/ClientAssets/CountryFlags/1.png", true)]
        [TestCase("Assets/Audio/Match/goal.mp3", true)]
        [TestCase("Assets/Settings/Icons/foobbuteo-icon-1024.png", false)]
        [TestCase("Assets/Resources/ClientSettings.json", false)]
        [TestCase("Packages/example/Resources/theme.tss", false)]
        public void ServerValidation_IdentifiesOnlyTheExcludedClientAssetFolders(string path, bool excluded)
        {
            var method = EditorType("ServerClientAssetBuildValidator")
                .GetMethod("IsClientAsset", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(
                excluded,
                method.Invoke(null, new object[] { path }),
                $"Server asset validation must classify {path} according to the requested image and audio scope."
            );
        }

        private static Type EditorType(string name) =>
            UnityEngine
                .Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .Select(assembly => assembly.GetType(name))
                .First(type => type != null);
    }
}
