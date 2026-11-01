// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Client.Users;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Users;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace Tests.Editor
{
    public class UserListSessionTests
    {
        private const int k_RoomId = 401;
        private const int k_OtherRoomId = 402;
        private const int k_IntervalSeconds = 3;
        private const ulong k_TargetClient = 501;
        private const int k_TargetUser = 601;
        private FakePresenceApi m_api;
        private UserListSession m_session;
        private double m_now;

        [SetUp]
        public void SetUp()
        {
            m_now = 0;
            m_api = new FakePresenceApi();
            m_session = new UserListSession(
                m_api,
                k_RoomId,
                () => m_now,
                row => new UserPresenceRowVM(row, true, k_RoomId, null)
            );
        }

        [TearDown]
        public void TearDown() => m_session.Dispose();

        [Test]
        public void Opening_RequestsImmediately_AndPollsAtServerInterval()
        {
            m_session.SetVisible(true);
            Assert.AreEqual(1, m_api.Requests.Count, "Opening a ready list should request its initial snapshot.");
            m_api.Reply(m_api.Requests[0], Row());
            m_now = k_IntervalSeconds - 1;
            m_session.Tick();
            Assert.AreEqual(1, m_api.Requests.Count, "The list must wait for the configured interval.");
            m_now = k_IntervalSeconds;
            m_session.Tick();
            Assert.AreEqual(2, m_api.Requests.Count, "The next snapshot should use the server's interval.");
        }

        [Test]
        public void NotReady_WaitsForServerSettings_ThenFetches()
        {
            m_api.SetReady(false);
            m_session.SetVisible(true);
            m_now = k_IntervalSeconds;
            m_session.Tick();
            Assert.IsEmpty(m_api.Requests, "A list must not poll without server settings and a connection.");

            m_api.SetReady(true);

            Assert.AreEqual(
                1,
                m_api.Requests.Count,
                "Receiving the session settings should unlock the initial request."
            );
        }

        [Test]
        public void OutstandingRequest_PreventsOverlap_AndTimeoutRetriesWithNewIdentity()
        {
            m_session.SetVisible(true);
            var expired = m_api.Requests[0];
            m_now = k_IntervalSeconds;
            m_session.Tick();
            Assert.AreEqual(1, m_api.Requests.Count, "A slow request must not cause overlapping polls.");
            m_now = 10;
            m_session.Tick();
            Assert.AreEqual(2, m_api.Requests.Count, "A lost response must not stop polling permanently.");

            m_api.Reply(expired, Row());

            Assert.IsEmpty(m_session.Rows, "A late response for an expired request must not replace current rows.");
        }

        [Test]
        public void Closing_StopsPollingAndRejectsLateResponses_ReopeningFetchesImmediately()
        {
            m_session.SetVisible(true);
            var closedRequest = m_api.Requests[0];
            m_session.SetVisible(false);
            m_now = 30;
            m_session.Tick();
            m_api.Reply(closedRequest, Row());
            Assert.AreEqual(1, m_api.Requests.Count, "A hidden list must generate no polling traffic.");
            Assert.IsEmpty(m_session.Rows, "A hidden view must reject a late response.");

            m_session.SetVisible(true);

            Assert.AreEqual(
                2,
                m_api.Requests.Count,
                "Reopening must fetch immediately rather than wait for an old timer."
            );
            Assert.AreNotEqual(
                closedRequest.RequestId,
                m_api.Requests[1].RequestId,
                "Reopened views must use a new request identity."
            );
        }

        [Test]
        public void OtherRoomResponse_IsIgnored()
        {
            m_session.SetVisible(true);
            var request = m_api.Requests[0];

            m_api.Reply(new UserListResponse(request.RequestId, k_OtherRoomId), Row());

            Assert.IsEmpty(m_session.Rows, "A response for a previous or different room must be ignored.");
            m_api.Reply(request, Row());
            Assert.AreEqual(1, m_session.Rows.Count, "The matching scoped response should still be accepted.");
        }

        [Test]
        public void Disconnect_ClearsRowsAndPendingRequest_ReconnectFetchesImmediately()
        {
            m_session.SetVisible(true);
            m_api.Reply(m_api.Requests[0], Row());
            m_now = k_IntervalSeconds;
            m_session.Tick();
            var stale = m_api.Requests[1];

            m_api.SetReady(false);
            m_api.Reply(stale, Row());

            Assert.IsEmpty(m_session.Rows, "Rows from an old network session must be cleared.");
            m_now = 20;
            m_session.Tick();
            Assert.AreEqual(2, m_api.Requests.Count, "Disconnected clients must not keep polling.");
            m_api.SetReady(true);
            Assert.AreEqual(3, m_api.Requests.Count, "A ready replacement session should fetch without waiting.");
        }

        [Test]
        public void ErrorThenEmptyResponse_ShowsFailureAndRecoversAtNextPoll()
        {
            m_session.SetVisible(true);
            var request = m_api.Requests[0];
            m_api.Deliver(new UserListResponse(request.RequestId, k_RoomId, error: "Not in room"));
            Assert.AreEqual("Not in room", m_session.Status, "Server errors must be presented to the user.");

            m_now = k_IntervalSeconds;
            m_session.Tick();
            m_api.Reply(m_api.Requests[1]);

            Assert.AreEqual(
                "No users to display.",
                m_session.Status,
                "Successful empty responses must replace failure state."
            );
        }

        [Test]
        public void SettingsNotification_DoesNotIntroduceAnExtraRefreshMethod()
        {
            m_session.SetVisible(true);
            m_api.Reply(m_api.Requests[0], Row());

            m_api.SetReady(true);
            m_api.SetReady(true);

            Assert.AreEqual(
                1,
                m_api.Requests.Count,
                "Repeated server-info messages must not trigger extra presence requests."
            );
        }

        [Test]
        public void AdministratorAction_SubmitsDisplayedUserClientPair_AndPresentsFailure()
        {
            m_session.SetVisible(true);
            var row = new UserPresenceRowVM(Row(), true);
            m_session.Disconnect(row);
            m_session.Disconnect(row);

            Assert.AreEqual(1, m_api.Disconnects.Count, "Only one administrator action should be pending at once.");
            var (RequestId, UserId, ClientId) = m_api.Disconnects[0];
            Assert.AreEqual(k_TargetUser, UserId, "Disconnect must target the user shown in the row.");
            Assert.AreEqual(k_TargetClient, ClientId, "Disconnect must include the exact session shown in the row.");
            m_api.DeliverDisconnect(new UserDisconnectResult(RequestId, "Connection changed"));
            Assert.AreEqual(
                "Connection changed",
                m_session.Status,
                "A rejected stale target must show the server's reason."
            );
        }

        [Test]
        public void Dispose_UnsubscribesAndStopsRequests()
        {
            m_session.SetVisible(true);
            var request = m_api.Requests[0];
            m_session.Dispose();

            m_api.Reply(request, Row());
            m_api.SetReady(true);
            m_now = 30;
            m_session.Tick();
            m_session.SetVisible(true);

            Assert.IsEmpty(m_session.Rows, "Disposed sessions must no longer receive snapshot callbacks.");
            Assert.AreEqual(1, m_api.Requests.Count, "Disposed sessions must generate no further requests.");
            Assert.AreEqual(0, m_api.SubscriberCount, "Every session event subscription must be removed on disposal.");
        }

        [TestCase(true, k_TargetClient, true)]
        [TestCase(false, k_TargetClient, false)]
        [TestCase(true, NetworkManager.ServerClientId, false)]
        public void Row_AdminActionVisibility_ProtectsHost(bool isAdmin, ulong clientId, bool expected)
        {
            var data = Row();
            data.ClientId = clientId;

            var row = new UserPresenceRowVM(data, isAdmin);

            Assert.AreEqual(
                expected,
                row.CanDisconnect,
                "Only admins viewing remote connections should see disconnect controls."
            );
        }

        [Test]
        public void Row_AdminActionVisibility_ProtectsHost()
        {
            var data = Row();
            // data.ClientId = k_TargetClient;

            var row = new UserPresenceRowVM(data, true, k_RoomId, null);

            Assert.IsFalse(row.CanDisconnect, "Admins not seed disconnect on in room user list.");
        }

        [Test]
        public void OfflineAssignedPlayer_StillShowsShirtNumberAndTeamSprite()
        {
            var data = Row();
            data.IsConnected = false;
            data.InRoomId = k_OtherRoomId;
            data.HasGameSlot = true;
            data.GameSlot = new GameSlot(k_RoomId, TeamIndex.Home, 7);
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                var team = new TeamVM(TeamIndex.Home, 1, "Home", null, sprite, ShirtColorEnum.White);

                var row = new UserPresenceRowVM(data, true, k_RoomId, team);

                Assert.AreSame(sprite, row.Shirt, "An offline assignment must reuse the current team's shirt asset.");
                Assert.AreEqual(
                    "7",
                    row.ShirtNumber,
                    "The roster must display the assigned player index as the shirt number."
                );
                Assert.AreEqual(
                    "Disconnected",
                    row.Status,
                    "The assignment must not make an offline player look connected."
                );
                Assert.IsFalse(row.CanDisconnect, "Offline assignments must not offer a disconnect action.");
                Assert.IsEmpty(
                    new UserPresenceRowVM(data, true, k_OtherRoomId, team).ShirtNumber,
                    "A slot for another game must not be displayed as participation in the viewed room."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(k_RoomId, "In room")]
        [TestCase(k_OtherRoomId, "Connected · Elsewhere")]
        public void ConnectedRosterUser_StatusSeparatesLocation(int actualRoom, string expected)
        {
            var data = Row();
            data.InRoomId = actualRoom;

            Assert.AreEqual(
                expected,
                new UserPresenceRowVM(data, true, k_RoomId, null).Status,
                "Room status should describe where the connected user is viewing."
            );
        }

        private static UserPresenceRow Row() =>
            new()
            {
                IsConnected = true,
                ClientId = k_TargetClient,
                UserId = k_TargetUser,
                PlayerId = k_TargetUser,
                PlayerName = "Roster player",
                InRoomId = k_RoomId,
            };

        internal sealed class FakePresenceApi : IUserPresenceApi
        {
            public bool IsReady { get; private set; } = true;
            public ClientRuntimeSettings Settings { get; set; } = new(k_IntervalSeconds);
            public event Action SessionChanged;
            public event Action<UserListResponse> UserListReceived;
            public event Action<UserDisconnectResult> UserDisconnectCompleted;
            public readonly List<UserListResponse> Requests = new();
            public readonly List<(ulong RequestId, int UserId, ulong ClientId)> Disconnects = new();
            public readonly List<int> Joins = new();
            private ulong m_nextId;
            public int SubscriberCount =>
                (SessionChanged?.GetInvocationList().Length ?? 0)
                + (UserListReceived?.GetInvocationList().Length ?? 0)
                + (UserDisconnectCompleted?.GetInvocationList().Length ?? 0);

            public ulong NextRequestId() => ++m_nextId;

            public void RequestUsers(ulong requestId, int roomId) =>
                Requests.Add(new UserListResponse(requestId, roomId));

            public void DisconnectUser(ulong requestId, int userId, ulong clientId) =>
                Disconnects.Add((requestId, userId, clientId));

            public void JoinRoom(int roomId) => Joins.Add(roomId);

            public void SetReady(bool ready)
            {
                IsReady = ready;
                SessionChanged?.Invoke();
            }

            public void Reply(UserListResponse request, params UserPresenceRow[] rows) =>
                Deliver(new UserListResponse(request.RequestId, request.RoomId, rows));

            public void Deliver(UserListResponse response) => UserListReceived?.Invoke(response);

            public void DeliverDisconnect(UserDisconnectResult result) => UserDisconnectCompleted?.Invoke(result);
        }
    }
}
