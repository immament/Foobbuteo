// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;

namespace Imma.Foobbuteo.Client.Auth
{
    [Serializable]
    internal sealed class RememberedSession
    {
        public string environment;
        public string refreshToken;
        public string refreshExpiresAtUtc;

        public bool IsValid(DateTimeOffset now) =>
            AuthenticationResult.TryUtcDate(refreshExpiresAtUtc, out var expiry)
            && expiry > now
            && AuthenticationResult.IsValidSecret(refreshToken);

        public void ClearSecret() => refreshToken = null;
    }
}
