// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;

namespace Tests.Editor.Client
{
    public class MatchSoundsTests
    {
        private List<MatchSoundCue> m_played;
        private MatchSoundService m_sounds;
        private int m_volume;

        [SetUp]
        public void SetUp()
        {
            m_played = new List<MatchSoundCue>();
            m_volume = 50;
            m_sounds = new MatchSoundService(m_played.Add, () => m_volume);
        }

        [TestCase(false, MatchSoundCue.Shot)]
        [TestCase(true, MatchSoundCue.Goal)]
        public void Shot_PlaysKickThenOnlyTheCorrectApplause(bool goal, MatchSoundCue applause)
        {
            m_sounds.BallAnimationStarted(MatchEventType.Shot);
            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Kick },
                m_played,
                "Only the kick should play before the shot animation finishes."
            );

            m_sounds.BallAnimationFinished(MatchEventType.Shot, goal);

            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Kick, applause },
                m_played,
                "A completed shot should produce exactly one applause cue appropriate to its outcome."
            );
        }

        [TestCase(MatchEventType.LowPass)]
        [TestCase(MatchEventType.HighPass)]
        public void Pass_PlaysOnlyTheKick(MatchEventType pass)
        {
            m_sounds.BallAnimationStarted(pass);
            m_sounds.BallAnimationFinished(pass, false);

            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Kick },
                m_played,
                "Pass animations should have a kick without applause."
            );
        }

        [Test]
        public void Repositioning_IsSilent()
        {
            m_sounds.BallAnimationStarted(MatchEventType.BallMove);
            m_sounds.BallAnimationFinished(MatchEventType.BallMove, false);

            Assert.IsEmpty(m_played, "Automatic ball repositioning must not sound like a kick.");
        }

        [Test]
        public void ZeroVolume_SkipsPlaybackAndDoesNotReplayMutedCues()
        {
            m_volume = 0;
            m_sounds.Play(MatchSoundCue.Goal);
            Assert.IsEmpty(m_played, "Muted requests must never reach the sound player.");

            m_volume = 50;
            m_sounds.Play(MatchSoundCue.Kick);

            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Kick },
                m_played,
                "Unmuting should play future cues without replaying the muted goal."
            );
        }

        [TestCase(FoobbuteoGameStatus.Pending, FoobbuteoGameStatus.Ongoing, 1)]
        [TestCase(FoobbuteoGameStatus.Ongoing, FoobbuteoGameStatus.Ongoing, 0)]
        [TestCase(FoobbuteoGameStatus.Ended, FoobbuteoGameStatus.Ended, 0)]
        public void StartWhistle_RequiresLiveMatchStart(
            FoobbuteoGameStatus previous,
            FoobbuteoGameStatus current,
            int expectedSounds
        )
        {
            m_sounds.TurnStarted(previous, current);

            Assert.AreEqual(
                expectedSounds,
                m_played.Count,
                "Only the live pending-to-ongoing transition should request a start whistle."
            );
        }

        [Test]
        public void DuplicateTurn_IsRejected()
        {
            const int turn = 7;
            Assert.IsTrue(m_sounds.BeginPlayback(turn), "The first result should start playback.");
            m_sounds.EndPlayback();

            Assert.IsFalse(
                m_sounds.BeginPlayback(turn),
                "A repeated turn result must not replay animations or sounds."
            );
        }

        [Test]
        public void FinalWhistle_WaitsForPlaybackAndPlaysOnce()
        {
            m_sounds.BeginPlayback(1);
            m_sounds.MatchEnded();
            m_sounds.MatchEnded();
            Assert.IsEmpty(m_played, "The final whistle must wait for the current animation playback.");

            m_sounds.EndPlayback();
            m_sounds.MatchEnded();

            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Final },
                m_played,
                "Repeated match-end events should still produce exactly one final whistle."
            );
        }

        [Test]
        public void MatchEndWithoutPlayback_PlaysFinalWhistle()
        {
            m_sounds.MatchEnded();

            CollectionAssert.AreEqual(
                new[] { MatchSoundCue.Final },
                m_played,
                "A match ending without an active animation should whistle immediately."
            );
        }
    }
}
