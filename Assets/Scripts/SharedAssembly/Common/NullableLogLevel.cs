// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;

namespace Imma.Common
{
    [Serializable]
    public struct NullableLogLevel
    {
        public bool HasValue;
        public ImmLogger.LogLevel Value;
    }
}
