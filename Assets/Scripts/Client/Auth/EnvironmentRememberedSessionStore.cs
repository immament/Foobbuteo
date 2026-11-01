// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class EnvironmentRememberedSessionStore : IRememberedSessionStore
    {
        private readonly IRememberedSessionStore m_store;
        private readonly string m_environment;

        public EnvironmentRememberedSessionStore(IRememberedSessionStore store, string environment)
        {
            ClientEnvironment.Validate(environment);
            m_store = store;
            m_environment = environment;
        }

        public bool IsSupported => m_store.IsSupported;

        public RememberedSession Load()
        {
            RememberedSession session = m_store.Load();
            if (session == null || session.environment == m_environment)
                return session;
            session.ClearSecret();
            m_store.Clear();
            return null;
        }

        public void Save(RememberedSession session)
        {
            m_store.Save(
                new RememberedSession
                {
                    environment = m_environment,
                    refreshToken = session.refreshToken,
                    refreshExpiresAtUtc = session.refreshExpiresAtUtc,
                }
            );
        }

        public void Clear() => m_store.Clear();
    }
}
