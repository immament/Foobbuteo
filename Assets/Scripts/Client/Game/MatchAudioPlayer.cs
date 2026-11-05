// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.Preferences;
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
        private ClientPreferences m_preferences;
        internal ClientPreferences Preferences => m_preferences;

        internal void Initialize(ClientPreferences preferences)
        {
            if (ReferenceEquals(m_preferences, preferences))
                return;
            if (m_preferences != null)
                m_preferences.propertyChanged -= OnSettingsChanged;
            m_preferences = preferences;
            preferences.propertyChanged += OnSettingsChanged;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (m_source != null && m_preferences != null)
                m_source.volume = m_preferences.Volume / 100f;
        }

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
            ApplyVolume();
        }

        private void OnSettingsChanged(object sender, BindablePropertyChangedEventArgs args)
        {
            if (args.propertyName == nameof(ClientPreferences.Volume))
                ApplyVolume();
        }

        public void Play(MatchSoundCue cue)
        {
            if (!Application.isPlaying || m_source == null || m_preferences == null || m_preferences.Volume == 0)
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
            if (m_preferences != null)
                m_preferences.propertyChanged -= OnSettingsChanged;
        }
    }
}
