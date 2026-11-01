// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game
{
    // The scene supplies AudioSource; RequireComponent would prevent server-role audio stripping.
    public sealed class MatchAudioPlayer : MonoBehaviour
    {
        [SerializeField]
        private AudioClip m_kick;

        [SerializeField]
        private AudioClip m_start;

        [SerializeField]
        private AudioClip m_shot;

        [SerializeField]
        private AudioClip m_goal;

        [SerializeField]
        private AudioClip m_final;
        private AudioSource m_source;
        private GameSettingsVM m_settings;
        internal GameSettingsVM Settings => m_settings ??= new GameSettingsVM();

        private void Awake()
        {
            m_source = GetComponent<AudioSource>();
            if (m_source == null)
            {
                enabled = false;
                return;
            }
            m_source.playOnAwake = false;
            m_source.spatialBlend = 0f;
            m_source.volume = Settings.Volume / 100f;
            Settings.propertyChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, BindablePropertyChangedEventArgs args)
        {
            if (m_source != null)
                m_source.volume = Settings.Volume / 100f;
        }

        public void Play(MatchSoundCue cue)
        {
            if (!Application.isPlaying || m_source == null || Settings.Volume == 0)
                return;
            var clip = cue switch
            {
                MatchSoundCue.Kick => m_kick,
                MatchSoundCue.Start => m_start,
                MatchSoundCue.Shot => m_shot,
                MatchSoundCue.Goal => m_goal,
                MatchSoundCue.Final => m_final,
                _ => null,
            };
            if (clip != null)
                m_source.PlayOneShot(clip);
        }

        private void OnDisable()
        {
            if (m_source != null)
                m_source.Stop();
        }

        private void OnDestroy()
        {
            if (m_settings != null)
                m_settings.propertyChanged -= OnSettingsChanged;
        }
    }
}
