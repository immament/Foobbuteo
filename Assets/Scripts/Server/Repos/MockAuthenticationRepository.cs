// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imma.Common;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class MockAuthenticationRepository : IAuthenticationRepository
    {
        private readonly ConcurrentDictionary<string, MockAuthenticationToken> m_tokens;
        private readonly Func<DateTime> m_utcNow;

        public MockAuthenticationRepository(IEnumerable<MockUserConfig> users, Func<DateTime> utcNow = null)
        {
            ImmLogger.AddType(typeof(MockAuthenticationRepository), "Auth");
            m_utcNow = utcNow ?? (() => DateTime.UtcNow);
            m_tokens = new ConcurrentDictionary<string, MockAuthenticationToken>(StringComparer.Ordinal);
            foreach (MockUserConfig user in users ?? Enumerable.Empty<MockUserConfig>())
            {
                if (string.IsNullOrWhiteSpace(user.Token))
                    continue;
                m_tokens[user.Token] = new MockAuthenticationToken(
                    user,
                    user.ExpiresAtUtc ?? m_utcNow().AddSeconds(60)
                );
            }
        }

        public Task<ServerUser> ValidateAndConsumeTokenAsync(ulong clientId, string token)
        {
            this.LDebug(() => $"++ Mock ValidateAndConsumeTokenAsync ^{clientId} ++");

            if (string.IsNullOrWhiteSpace(token) || !m_tokens.TryRemove(token, out MockAuthenticationToken entry))
                return Task.FromResult<ServerUser>(null);

            if (entry.ExpiresAtUtc < m_utcNow())
                return Task.FromResult<ServerUser>(null);

            MockUserConfig user = entry.User;
            return Task.FromResult(
                new ServerUser(clientId, user.UserId, user.PlayerId, user.Name, user.Role, teams: user.Teams)
            );
        }

        public void Dispose() => m_tokens.Clear();

        private sealed class MockAuthenticationToken
        {
            public MockAuthenticationToken(MockUserConfig user, DateTime expiresAtUtc)
            {
                User = user;
                ExpiresAtUtc = expiresAtUtc;
            }

            public MockUserConfig User { get; }
            public DateTime ExpiresAtUtc { get; }
        }
    }
}
