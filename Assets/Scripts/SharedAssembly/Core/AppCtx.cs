// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Core
{
    public static class AppCtx
    {
        public static IClientAppCtrl Client { get; set; }
        public static IServerAppCtrl Server { get; set; }
        public static IAppCtrl App { get; set; }
    }
}
