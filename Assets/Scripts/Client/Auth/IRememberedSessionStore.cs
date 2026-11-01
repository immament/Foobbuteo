// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Client.Auth
{
    internal interface IRememberedSessionStore
    {
        bool IsSupported { get; }
        RememberedSession Load();
        void Save(RememberedSession session);
        void Clear();
    }
}
