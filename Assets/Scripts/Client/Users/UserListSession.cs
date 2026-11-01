// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Linq;
using Imma.Foobbuteo.Shared.Users;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Users
{
    public sealed class UserListSession : IDisposable, INotifyBindablePropertyChanged
    {
        private readonly IUserPresenceApi m_api;
        private readonly Func<double> m_nowSeconds;
        private readonly Func<UserPresenceRow, UserPresenceRowVM> m_createRow;
        private readonly int m_roomId;
        private bool m_visible;
        private bool m_disposed;
        private ulong? m_pendingRequest;
        private ulong? m_pendingDisconnect;
        private double m_requestDeadline;
        private double m_disconnectDeadline;
        private double m_nextRefresh;
        private List<UserPresenceRowVM> m_rows = new();
        private string m_status = "";
        private const int k_MinimumResponseTimeoutSeconds = 10;

        [CreateProperty]
        public List<UserPresenceRowVM> Rows => m_rows;

        [CreateProperty]
        public string Status => m_status;

        [CreateProperty]
        public bool StatusVisible { get; }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        public UserListSession(
            IUserPresenceApi api,
            int roomId,
            Func<double> nowSeconds,
            Func<UserPresenceRow, UserPresenceRowVM> createRow
        )
        {
            m_api = api;
            m_roomId = roomId;
            m_nowSeconds = nowSeconds;
            m_createRow = createRow;
            m_api.UserListReceived += OnUsersReceived;
            m_api.UserDisconnectCompleted += OnDisconnectCompleted;
            m_api.SessionChanged += OnSessionChanged;
            StatusVisible = roomId == 0;
        }

        public void SetVisible(bool visible)
        {
            if (m_disposed || m_visible == visible)
                return;
            m_visible = visible;
            m_pendingRequest = null;
            m_pendingDisconnect = null;
            if (visible)
            {
                m_nextRefresh = m_nowSeconds();
                SetStatus("Loading users…");
                Tick();
            }
        }

        public void Tick()
        {
            if (m_disposed || !m_visible)
                return;
            if (!m_api.IsReady)
            {
                SetStatus("Waiting for connection and server settings…");
                return;
            }
            double now = m_nowSeconds();
            if (m_pendingDisconnect.HasValue && now >= m_disconnectDeadline)
            {
                m_pendingDisconnect = null;
                SetStatus("Disconnect request timed out. Wait for the list to refresh.");
            }
            if (m_pendingRequest.HasValue)
            {
                if (now < m_requestDeadline)
                    return;
                m_pendingRequest = null;
                SetStatus("User list request timed out. Retrying…");
            }
            if (now < m_nextRefresh)
                return;
            m_pendingRequest = m_api.NextRequestId();
            m_requestDeadline = now + Math.Max(k_MinimumResponseTimeoutSeconds, IntervalSeconds);
            m_nextRefresh = now + IntervalSeconds;
            m_api.RequestUsers(m_pendingRequest.Value, m_roomId);
        }

        private int IntervalSeconds => m_api.Settings.UserListRefreshIntervalSeconds;

        public void Disconnect(UserPresenceRowVM row)
        {
            if (m_disposed || !m_visible || !m_api.IsReady || m_pendingDisconnect.HasValue || !row.CanDisconnect)
                return;
            m_pendingDisconnect = m_api.NextRequestId();
            m_disconnectDeadline = m_nowSeconds() + Math.Max(k_MinimumResponseTimeoutSeconds, IntervalSeconds);
            SetStatus($"Disconnecting {row.PlayerName}…");
            m_api.DisconnectUser(m_pendingDisconnect.Value, row.Data.UserId, row.Data.ClientId);
        }

        private void OnUsersReceived(UserListResponse response)
        {
            if (
                m_disposed
                || !m_visible
                || !m_api.IsReady
                || response.RoomId != m_roomId
                || m_pendingRequest != response.RequestId
            )
                return;
            m_pendingRequest = null;
            if (!string.IsNullOrEmpty(response.Error))
            {
                SetStatus(response.Error);
                return;
            }
            m_rows = (response.Users ?? Array.Empty<UserPresenceRow>()).Select(m_createRow).ToList();
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Rows)));
            if (!m_pendingDisconnect.HasValue)
                SetStatus(
                    m_rows.Count == 0 ? "No users to display."
                    : m_rows.Count == 1 ? "1 user"
                    : $"{m_rows.Count} users"
                );
        }

        private void OnDisconnectCompleted(UserDisconnectResult result)
        {
            if (m_disposed || !m_visible || m_pendingDisconnect != result.RequestId)
                return;
            m_pendingDisconnect = null;
            SetStatus(string.IsNullOrEmpty(result.Error) ? "User disconnected." : result.Error);
        }

        private void OnSessionChanged()
        {
            if (m_disposed)
                return;
            if (!m_api.IsReady)
            {
                m_pendingRequest = null;
                m_pendingDisconnect = null;
                m_nextRefresh = m_nowSeconds();
                m_rows = new();
                propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Rows)));
            }
            Tick();
        }

        private void SetStatus(string status)
        {
            if (m_status == status)
                return;
            m_status = status;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Status)));
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            SetVisible(false);
            m_disposed = true;
            m_api.UserListReceived -= OnUsersReceived;
            m_api.UserDisconnectCompleted -= OnDisconnectCompleted;
            m_api.SessionChanged -= OnSessionChanged;
        }
    }
}
