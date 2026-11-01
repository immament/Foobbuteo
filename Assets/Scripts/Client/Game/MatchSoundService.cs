// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Client.Game
{
    public sealed class MatchSoundService
    {
        private readonly Action<MatchSoundCue> m_play;
        private readonly Func<int> m_volume;
        private readonly HashSet<int> m_playedTurns = new();
        private int m_activePlaybacks;
        private bool m_ended;
        private bool m_finalPending;

        public MatchSoundService(Action<MatchSoundCue> play, Func<int> volume)
        {
            m_play = play;
            m_volume = volume;
        }

        public void Play(MatchSoundCue cue)
        {
            if (m_volume() > 0)
                m_play(cue);
        }

        public void TurnStarted(FoobbuteoGameStatus previous, FoobbuteoGameStatus current)
        {
            if (previous == FoobbuteoGameStatus.Pending && current == FoobbuteoGameStatus.Ongoing)
                Play(MatchSoundCue.Start);
        }

        public bool BeginPlayback(int turn)
        {
            if (m_ended || !m_playedTurns.Add(turn))
                return false;
            m_activePlaybacks++;
            return true;
        }

        public void EndPlayback()
        {
            m_activePlaybacks--;
            if (m_activePlaybacks == 0 && m_finalPending)
            {
                m_finalPending = false;
                Play(MatchSoundCue.Final);
            }
        }

        public void MatchEnded()
        {
            if (m_ended)
                return;
            m_ended = true;
            if (m_activePlaybacks > 0)
                m_finalPending = true;
            else
                Play(MatchSoundCue.Final);
        }

        public void BallAnimationStarted(MatchEventType type)
        {
            if (type is MatchEventType.LowPass or MatchEventType.HighPass or MatchEventType.Shot)
                Play(MatchSoundCue.Kick);
        }

        public void BallAnimationFinished(MatchEventType type, bool goal)
        {
            if (type == MatchEventType.Shot)
                Play(goal ? MatchSoundCue.Goal : MatchSoundCue.Shot);
        }

        internal static bool IsGoalEvent(TurnResultReadyMessage message, int eventIndex)
        {
            var cues = message.Result.Commentary;
            if (cues != null)
                foreach (var cue in cues)
                    if (cue.EventIndex == eventIndex && cue.AfterEvent && cue.Kind == MatchCommentaryKind.ScoreGoal)
                        return true;
            return false;
        }
    }
}
