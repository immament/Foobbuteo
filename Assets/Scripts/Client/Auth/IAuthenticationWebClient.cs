// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Threading;
using System.Threading.Tasks;

namespace Imma.Foobbuteo.Client.Auth
{
    internal interface IAuthenticationWebClient
    {
        Task<AuthenticationResult> LoginAsync(
            string login,
            string password,
            bool remember,
            CancellationToken cancellation
        );
        Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellation);
        Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellation);
    }
}
