// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;

namespace Imma.Foobbuteo.Client.Auth
{
    internal sealed class EmptyRememberedSessionStore : IRememberedSessionStore
    {
        public bool IsSupported => false;

        public void Clear() { }

        public RememberedSession Load() => null;

        public void Save(RememberedSession session) =>
            throw new PlatformNotSupportedException("Remember Me is available only in Android players.");
    }
}
