// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;

namespace Imma.Foobbuteo.Server.Repos
{
    internal interface IAuthenticationRepository : IDisposable
    {
        Task<ServerUser> ValidateAndConsumeTokenAsync(ulong clientId, string token);
    }
}
