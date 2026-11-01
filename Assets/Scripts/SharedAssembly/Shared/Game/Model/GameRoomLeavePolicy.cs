// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

namespace Imma.Foobbuteo.Shared.Game.Model
{
    public static class GameRoomLeavePolicy
    {
        public const string LockedMessage = "You cannot leave your game room while the game is ongoing.";

        public static bool CanLeave(GameUser user, int roomId, FoobbuteoGameStatus status) =>
            user != null
            && (
                user.IsAdmin
                || status != FoobbuteoGameStatus.Ongoing
                || !user.GameSlot.HasValue
                || user.GameSlot.Value.GameId != roomId
            );
    }
}
