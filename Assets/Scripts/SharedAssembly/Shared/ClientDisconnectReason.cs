// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Shared
{
    public static class ClientDisconnectReason
    {
        public const string SessionReplaced = "session_replaced|Your account connected from another client.";
        public const string Kicked = "kicked|Disconnected by an administrator.";
        public const string InvalidToken = "invalid_token|The game login token is invalid or expired.";
        public const string ServiceUnavailable =
            "service_unavailable|Authentication service unavailable. Please try again.";
        public const string ServerBusy = "server_busy|Authentication server is busy. Please try again.";

        public static bool IsInvalidToken(string reason) => reason == InvalidToken || reason == "Token not found";

        public static bool CanRetry(string reason) =>
            string.IsNullOrEmpty(reason) || reason == ServiceUnavailable || reason == ServerBusy;

        public static string Message(string reason)
        {
            int separator = reason?.IndexOf('|') ?? -1;
            return separator < 0 ? reason : reason.Substring(separator + 1);
        }
    }
}
