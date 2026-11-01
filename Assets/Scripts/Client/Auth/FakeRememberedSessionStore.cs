// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_EDITOR

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class FakeRememberedSessionStore : IRememberedSessionStore
    {
        private static FakeRememberedSessionStore s_instance;
        public static FakeRememberedSessionStore Instance => s_instance ??= new FakeRememberedSessionStore();

        public bool IsSupported { get; set; } = true;
        public RememberedSession Saved;

        public RememberedSession Load()
        {
            return Saved == null ? null : Copy(Saved);
        }

        public void Save(RememberedSession session)
        {
            Saved = Copy(session);
        }

        public void Clear() => Saved = null;

        private static RememberedSession Copy(RememberedSession source) =>
            new RememberedSession
            {
                refreshToken = source.refreshToken,
                environment = source.environment,
                refreshExpiresAtUtc = source.refreshExpiresAtUtc,
            };
    }
}
#endif
