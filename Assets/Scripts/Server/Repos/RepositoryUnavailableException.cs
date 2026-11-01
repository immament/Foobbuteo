// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class RepositoryUnavailableException : Exception
    {
        public RepositoryUnavailableException(string message)
            : base(message) { }

        public RepositoryUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
