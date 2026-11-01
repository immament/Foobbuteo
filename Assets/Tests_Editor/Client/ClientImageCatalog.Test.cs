// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.IO;
using Imma.Foobbuteo.Client;
using Imma.Foobbuteo.Client.Game;
using Imma.Foobbuteo.Client.Game.UI.Players;
using Imma.Foobbuteo.Client.UI.Help;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.Client
{
    public sealed class ClientImageCatalogTests
    {
        [Test]
        public void CountryFlags_IncludeEveryMigratedFlagAndPreserveCachedLookups()
        {
            var images = ClientImageCatalog.LoadForEditor();
            Assert.IsNotNull(images, "The client bootstrap catalogue must exist outside Resources.");
            var cache = new CountryFlagCache(images);
            foreach (string path in Directory.GetFiles("Assets/ClientAssets/CountryFlags", "*.png"))
            {
                int countryId = int.Parse(Path.GetFileNameWithoutExtension(path));
                var expected = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreSame(expected, cache.Get(countryId), $"Country {countryId} must retain its imported flag.");
                Assert.AreSame(
                    expected,
                    cache.Get(countryId),
                    $"Country {countryId} must reuse its flag on repeated lookup."
                );
            }
            Assert.IsNull(cache.Get(0), "An absent country must leave its flag cell empty.");
            Assert.IsNull(cache.Get(-1), "A negative country ID must leave its flag cell empty.");
            Assert.IsNull(cache.Get(int.MaxValue), "An unknown country must leave its flag cell empty.");
        }

        [Test]
        public void Shirts_ResolveEveryTeamColorThroughThePlayerDatabase()
        {
            var images = ClientImageCatalog.LoadForEditor();
            var gameObject = new GameObject("catalogue-shirt-test");
            try
            {
                var database = gameObject.AddComponent<PlayerSpriteDatabase>();
                database.Init(images);
                foreach (ShirtColorEnum color in Enum.GetValues(typeof(ShirtColorEnum)))
                {
                    var expected = images.GetShirt(color);
                    Assert.IsNotNull(expected, $"Team color {color} must have a shirt in the catalogue.");
                    Assert.IsTrue(
                        expected.name.StartsWith(color + "-"),
                        $"Team color {color} must keep its original shirt artwork."
                    );
                    Assert.AreSame(
                        expected,
                        database.GetShirtSprite(color),
                        $"The player database must use the supplied {color} shirt."
                    );
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(0, 0, "Run / Run with the Ball", "Actions/opw_mover")]
        [TestCase(0, 1, "Low Pass", "Actions/opw_passe")]
        [TestCase(0, 2, "High Pass / Cross", "Actions/opw_passelongo")]
        [TestCase(0, 3, "Shot", "Actions/opw_remate")]
        [TestCase(1, 0, "Confrontation", "Modes/opw_rb")]
        [TestCase(1, 1, "Intercept Passes", "Modes/opw_inter")]
        [TestCase(1, 2, "Saves (Goalkeeper)", "Modes/opw_defesa")]
        [TestCase(1, 3, "Out of Area (GK)", "Modes/opw_saida")]
        [TestCase(1, 4, "Field Player", "Modes/opw_jogador")]
        public void HelpItem_UsesMatchingActionArtwork(int page, int index, string title, string imagePath)
        {
            var images = ClientImageCatalog.LoadForEditor();
            var item = HelpData.CreateScreens(images)[page][index];
            var expected = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/ClientAssets/Buttons/{imagePath}.png");

            Assert.IsNotNull(expected, $"The action button artwork for {title} must exist as a sprite.");
            Assert.AreEqual(title, item.title, $"Page {page}, item {index} must describe {title}.");
            Assert.AreEqual(
                expected,
                item.image,
                $"The {title} help item must use its matching action button artwork."
            );
        }

        [Test]
        public void HelpPages_PopulateWithoutDuplicatingEntries()
        {
            var images = ClientImageCatalog.LoadForEditor();
            var screens = HelpData.CreateScreens(images);
            Assert.AreEqual(2, screens.Count, "Both existing help pages must remain available.");

            var screen = new HelpScreen();
            screen.SetImages(images);
            int expectedCount = screens[0].Count;
            Assert.AreEqual(expectedCount, screen.childCount, "Supplying the catalogue must populate the help page.");
            screen.SetImages(images);
            Assert.AreEqual(
                expectedCount,
                screen.childCount,
                "Reinitializing help must replace entries without duplicating them."
            );
        }

        [Test]
        public void ResourceFolders_ContainNoProjectClientImages()
        {
            var resources = Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories);
            foreach (string folder in resources)
            {
                if (!folder.Replace('\\', '/').StartsWith("Assets/Resources", StringComparison.Ordinal))
                    continue;
                Assert.IsEmpty(
                    AssetDatabase.FindAssets("t:Texture2D", new[] { folder }),
                    $"Client images must not be forced into server builds by {folder}."
                );
            }
        }
    }
}
