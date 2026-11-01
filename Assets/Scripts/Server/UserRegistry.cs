// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Linq;
using Imma.Common;
using Unity.Netcode;

namespace Imma.Foobbuteo.Server
{
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    internal class UserRegistry : IUserRegistry
    {
        public event Action<int> ConnectionChanged;

        #region ++ IUserRegistry ++++++++++++++++++++++++++++++++++++++++

        [SerializeReference]
        private SerializableDictionary<ulong, int> m_clientToUserId = new();

        [SerializeReference]
        private SerializableDictionary<int, ServerUser> m_users = new();

        public bool TryGetUser(ulong clientId, out ServerUser user)
        {
            if (m_clientToUserId.TryGetValue(clientId, out int userId))
                return m_users.TryGetValue(userId, out user);
            user = null;
            return false;
        }

        public bool TryGetUser(ulong clientId, out ServerUser user, out string reason)
        {
            if (m_clientToUserId.TryGetValue(clientId, out int userId))
            {
                if (!m_users.TryGetValue(userId, out user))
                {
                    reason = $"User @{userId} (^{clientId}) not not registered";
                    return false;
                }
                reason = null;
                return true;
            }
            user = null;
            reason = $"Client ^{clientId} mapping not registered";
            return false;
        }

        public bool TryGetUser(int userId, out ServerUser user)
        {
            return m_users.TryGetValue(userId, out user);
        }

        public ServerUser GetUser(ulong clientId)
        {
            if (!TryGetUser(clientId, out ServerUser user, out string reason))
            {
                throw new AppException(reason ?? $"Problem during get user for  ^{clientId}");
            }
            return user;
        }

        public List<ServerUser> GetUsersInRoom(int gameId)
        {
            return m_users.Values.Where(u => u.InGameRoomId == gameId || u.IsPlayGame(gameId)).ToList();
        }

        public void Dispose()
        {
            // throw new NotImplementedException();
        }

        #endregion -- IUserRegistry --------------------------------------

        public string RegisterUser(ServerUser user, bool validateClientId = true, bool dev_forceNew = false)
        {
            return RegisterUser(user, out _, validateClientId, dev_forceNew);
        }

        public IReadOnlyCollection<ServerUser> GetUsers() => m_users.Values.ToArray();

        public string RegisterUser(
            ServerUser user,
            out ulong? replacedClientId,
            bool validateClientId = true,
            bool dev_forceNew = false
        )
        {
            replacedClientId = null;
            if (validateClientId && !user.ClientId.HasValue)
                return $"User @{user.UserId} has not clientId";

            if (user.ClientId.HasValue)
            {
                if (m_clientToUserId.TryGetValue(user.ClientId.Value, out int tmpUserId))
                    return $"[^{user.ClientId}] already registered with userId: @{tmpUserId}. New user Id: @{user.UserId}";
            }

            if (m_users.TryGetValue(user.UserId, out var registeredUser))
            {
                if (registeredUser.ClientId == NetworkManager.ServerClientId)
                    return $"[^{user.ClientId}] User: @{user.UserId} already registered. Registered clientId ^{registeredUser.ClientId}";

                if (registeredUser.ClientId.HasValue && !user.ClientId.HasValue)
                    return $"[^{user.ClientId}] User: @{user.UserId} already registered. Registered clientId ^{registeredUser.ClientId}";

                if (user.ClientId.HasValue)
                {
                    replacedClientId = registeredUser.ClientId;
                    // Detach the old client before its disconnect callback can clear this session.
                    if (replacedClientId.HasValue)
                        m_clientToUserId.Remove(replacedClientId.Value);
                    registeredUser.Reconnected(user.ClientId.Value);
                    m_clientToUserId.Add(user.ClientId.Value, user.UserId);
                }
                registeredUser.RefreshTeams(user.Teams.ToArray());
                if (dev_forceNew)
                    registeredUser.CopyFrom(user);
                else
                    user.CopyFrom(registeredUser);
                this.LTrace(() => $"User re-registered {user}");
                ConnectionChanged?.Invoke(user.UserId);
                return null;
            }
            m_users.Add(user.UserId, user);
            if (user.ClientId.HasValue)
            {
                m_clientToUserId.Add(user.ClientId.Value, user.UserId);
            }
            this.LTrace(() => $"User registered {user}");
            ConnectionChanged?.Invoke(user.UserId);
            return null;
        }

        public void UnregisterUserOnDisconnect(ulong clientId)
        {
            if (!m_clientToUserId.Remove(clientId, out int userId))
            {
                this.LDebug(() => $"[^{clientId}] not registered.");
                return;
            }

            if (!m_users.TryGetValue(userId, out var user))
            {
                this.LDebug(() => $"[^{clientId}] UserId @{userId} not registered.");
                return;
            }
            if (user.ClientId != clientId)
            {
                this.LDebug(() =>
                    $"[^{clientId}] UserId @{userId} registered with different clientId: [^{user.ClientId}]."
                );
                return;
            }
            if (user.GameSlot.HasValue)
            {
                user.SetDisconnected();
                ConnectionChanged?.Invoke(user.UserId);
                return;
            }

            m_users.Remove(userId);
            ConnectionChanged?.Invoke(userId);
            this.LInfo($"[^{clientId}] ServerUser @{user.UserId} disconnected.");
        }
    }
}
