// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using UnityEngine;

namespace Imma.Foobbuteo.Common.Utils
{
    public static class ServerRpcRateLimiter
    {
        private const double k_requestsPerSecond = 30d;
        private const double k_burstCapacity = 30d;
        private const double k_warningIntervalSeconds = 1d;

        private static readonly object s_lock = new();
        private static readonly Dictionary<ulong, ClientBudget> s_clientBudgets = new();

        public static bool TryConsume(ulong clientId, out bool shouldLogWarning)
        {
            return TryConsume(clientId, Time.realtimeSinceStartupAsDouble, out shouldLogWarning);
        }

        internal static bool TryConsume(ulong clientId, double now, out bool shouldLogWarning)
        {
            lock (s_lock)
            {
                if (!s_clientBudgets.TryGetValue(clientId, out var budget))
                {
                    budget = new ClientBudget(now);
                    s_clientBudgets.Add(clientId, budget);
                }

                double elapsed = now - budget.LastRefillTime;
                budget.Tokens = System.Math.Min(k_burstCapacity, budget.Tokens + elapsed * k_requestsPerSecond);
                budget.LastRefillTime = now;

                if (budget.Tokens >= 1d)
                {
                    budget.Tokens -= 1d;
                    shouldLogWarning = false;
                    return true;
                }

                shouldLogWarning = now - budget.LastWarningTime >= k_warningIntervalSeconds;
                if (shouldLogWarning)
                    budget.LastWarningTime = now;
                return false;
            }
        }

        public static void RemoveClient(ulong clientId)
        {
            lock (s_lock)
            {
                s_clientBudgets.Remove(clientId);
            }
        }

        public static void Clear()
        {
            lock (s_lock)
            {
                s_clientBudgets.Clear();
            }
        }

        private sealed class ClientBudget
        {
            public ClientBudget(double now)
            {
                Tokens = k_burstCapacity;
                LastRefillTime = now;
                LastWarningTime = double.NegativeInfinity;
            }

            public double Tokens;
            public double LastRefillTime;
            public double LastWarningTime;
        }
    }
}
