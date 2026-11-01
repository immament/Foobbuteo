// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using Imma.Common;
using Imma.Foobbuteo.Shared.Users;

namespace Imma.Foobbuteo.Server.Users
{
    internal sealed class UserPresenceService
    {
        private readonly IUserRegistry m_users;

        public UserPresenceService(IUserRegistry users) => m_users = users;

        public UserPresenceRow[] GetUsers(ulong requesterClientId, int roomId, Func<ulong, bool> isConnected)
        {
            ServerUser requester = m_users.GetUser(requesterClientId);
            if (!isConnected(requesterClientId))
                throw new AppException("Your connection is no longer active.");
            if (roomId < 0)
                throw new AppException("Invalid room.");
            if (roomId > 0 && requester.InGameRoomId != roomId)
                throw new AppException("You must be in this room to view its users.");

            return m_users
                .GetUsers()
                .Where(user =>
                    roomId == 0
                        ? user.ClientId.HasValue && isConnected(user.ClientId.Value)
                        : user.InGameRoomId == roomId || user.IsPlayGame(roomId)
                )
                .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(user => user.UserId)
                .Select(user => new UserPresenceRow
                {
                    IsConnected = user.ClientId.HasValue && isConnected(user.ClientId.Value),
                    ClientId =
                        user.ClientId.HasValue && isConnected(user.ClientId.Value) ? user.ClientId.Value : default,
                    UserId = user.UserId,
                    PlayerId = user.PlayerId,
                    PlayerName = user.Name ?? "",
                    InRoomId = user.InGameRoomId,
                    HasGameSlot = user.GameSlot.HasValue,
                    GameSlot = user.GameSlot.GetValueOrDefault(),
                })
                .ToArray();
        }

        public void AuthorizeDisconnect(
            ulong requesterClientId,
            int targetUserId,
            ulong targetClientId,
            ulong hostClientId,
            Func<ulong, bool> isConnected
        )
        {
            ServerUser requester = m_users.GetUser(requesterClientId);
            if (!isConnected(requesterClientId) || !requester.IsAdmin)
                throw new AppException("Only connected administrators can disconnect users.");
            if (targetClientId == hostClientId)
                throw new AppException("The host connection cannot be disconnected.");
            if (
                !m_users.TryGetUser(targetClientId, out var target)
                || target.UserId != targetUserId
                || target.ClientId != targetClientId
                || !isConnected(targetClientId)
            )
                throw new AppException("This user connection has changed. Wait for the list to refresh.");
        }
    }
}
