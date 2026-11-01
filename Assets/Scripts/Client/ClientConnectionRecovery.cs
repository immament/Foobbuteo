// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;

namespace Imma.Foobbuteo.Client
{
    internal sealed class ClientConnectionRecovery
    {
        private const double MaximumDelaySeconds = 30;
        private readonly Func<double> m_jitter;
        private double m_nextAttempt;
        private int m_attempts;
        public bool IsPending { get; private set; }

        public ClientConnectionRecovery(Func<double> jitter = null) => m_jitter = jitter ?? new Random().NextDouble;

        public void Schedule(double now)
        {
            if (IsPending)
                return;
            IsPending = true;
            m_nextAttempt = now;
            m_attempts = 0;
        }

        public bool TryBegin(double now, bool paused, bool busy)
        {
            if (!IsPending || paused || busy || now < m_nextAttempt)
                return false;
            double delay = Math.Min(MaximumDelaySeconds, Math.Pow(2, Math.Min(++m_attempts, 5)));
            m_nextAttempt = now + delay * (0.8 + 0.2 * m_jitter());
            return true;
        }

        public void Cancel()
        {
            IsPending = false;
            m_attempts = 0;
        }
    }
}
