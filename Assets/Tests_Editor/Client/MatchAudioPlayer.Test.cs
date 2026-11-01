// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Reflection;
using Imma.Foobbuteo.Client.Game;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.Client
{
    public class MatchAudioPlayerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MissingAudioSource_DisablesPlayerWithoutRecreatingAudio(bool stripSource)
        {
            var audioObject = new GameObject("Match audio stripping test");
            audioObject.SetActive(false);
            try
            {
                var source = stripSource ? audioObject.AddComponent<AudioSource>() : null;
                var player = audioObject.AddComponent<MatchAudioPlayer>();

                if (stripSource)
                    Object.DestroyImmediate(source);
                typeof(MatchAudioPlayer)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);
                player.Play(MatchSoundCue.Goal);

                Assert.IsNull(
                    audioObject.GetComponent<AudioSource>(),
                    "Server-role stripping must be able to remove audio without the player recreating it."
                );
                Assert.IsFalse(
                    player.enabled,
                    "A player with no audio source should disable itself without failing initialization."
                );
            }
            finally
            {
                Object.DestroyImmediate(audioObject);
            }
        }

        [Test]
        public void SceneAudioSource_InitializesForNonSpatialClientPlayback()
        {
            var audioObject = new GameObject("Match audio client test");
            audioObject.SetActive(false);
            try
            {
                var source = audioObject.AddComponent<AudioSource>();
                source.playOnAwake = true;
                source.spatialBlend = 1f;
                var player = audioObject.AddComponent<MatchAudioPlayer>();

                typeof(MatchAudioPlayer)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);

                Assert.IsTrue(player.enabled, "Client audio should remain enabled when its scene supplies a source.");
                Assert.IsFalse(source.playOnAwake, "Match sounds should play only when requested by match events.");
                Assert.AreEqual(0f, source.spatialBlend, "Match sounds should remain independent of camera position.");
            }
            finally
            {
                Object.DestroyImmediate(audioObject);
            }
        }
    }
}
