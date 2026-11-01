// SPDX-License-Identifier: AGPL-3.0-only
using System;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    internal sealed class SessionLoadingSpinner : IDisposable
    {
        private const long k_intervalMilliseconds = 50;
        private const float k_degreesPerSecond = 240f;
        private readonly IVisualElementScheduledItem m_animation;

        public SessionLoadingSpinner(VisualElement spinner)
        {
            m_animation = spinner
                .schedule.Execute(timer =>
                    spinner.style.rotate = new Rotate((timer.now / 1000.0f * k_degreesPerSecond) % 360f)
                )
                .Every(k_intervalMilliseconds);
            m_animation.Pause();
        }

        public void SetActive(bool active)
        {
            if (active)
                m_animation.Resume();
            else
                m_animation.Pause();
        }

        public void Dispose() => m_animation.Pause();
    }
}
