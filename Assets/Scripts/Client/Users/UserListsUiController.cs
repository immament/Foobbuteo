// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Users
{
    public sealed class UserListsUiController : IDisposable
    {
        private readonly IUserPresenceApi m_api;
        private readonly UserListSession m_global;
        private readonly UserListSession m_room;
        private VisualElement m_root;
        private VisualElement m_overlay;
        private VisualElement m_overlayLayer;
        private VisualElement m_roomContent;
        private VisualElement m_messages;
        private Button m_openButton;
        private Button m_closeButton;
        private Button m_messagesButton;
        private Button m_usersButton;
        private ListView m_globalList;
        private ListView m_roomList;
        private IVisualElementScheduledItem m_tick;
        private bool m_globalOpen;
        private bool m_usersSelected;
        private bool m_disposed;
        private readonly List<RowCallbacks> m_rowCallbacks = new();
        private const string k_HiddenClass = "user-list-hidden";
        private const int k_PollTickMilliseconds = 250;

        public UserListsUiController(
            IUserPresenceApi api,
            Func<bool> isAdmin,
            int roomId = 0,
            Func<TeamIndex, TeamVM> getTeam = null
        )
        {
            m_api = api;
            if (roomId > 0)
                m_room = new UserListSession(
                    api,
                    roomId,
                    () => Time.realtimeSinceStartupAsDouble,
                    row => new UserPresenceRowVM(
                        row,
                        isAdmin(),
                        roomId,
                        row.HasGameSlot ? getTeam?.Invoke(row.GameSlot.TeamIdx) : null
                    )
                );
            else
                m_global = new UserListSession(
                    api,
                    0,
                    () => Time.realtimeSinceStartupAsDouble,
                    row => new UserPresenceRowVM(row, isAdmin())
                );
            m_api.SessionChanged += OnSessionChanged;
        }

        public void Bind(VisualElement root)
        {
            Unbind();
            if (m_disposed || root == null)
                return;
            m_root = root;
            if (m_global != null)
            {
                m_overlay = root.Q("onlineUsersOverlay");
                m_overlayLayer = root.Q("onlineUsersLayer");
                m_openButton = root.Q<Button>("onlineUsersButton");
                m_closeButton = m_overlay?.Q<Button>("closeOnlineUsersButton");
            }
            m_roomContent = root.Q("roomUsersContent");
            m_messages = root.Q("roomMessagesContent");
            m_messagesButton = root.Q<Button>("messagesTabButton");
            m_usersButton = root.Q<Button>("usersTabButton");
            if (m_openButton != null)
                m_openButton.clicked += OpenGlobal;
            if (m_closeButton != null)
                m_closeButton.clicked += CloseGlobal;
            m_overlayLayer?.RegisterCallback<ClickEvent>(OnBackdropClicked);
            if (m_messagesButton != null)
                m_messagesButton.clicked += SelectMessages;
            if (m_usersButton != null)
                m_usersButton.clicked += SelectUsers;
            m_globalList = BindList(m_overlay, m_global);
            m_roomList = BindList(m_roomContent, m_room);
            root.RegisterCallback<DetachFromPanelEvent>(OnDetached);
            root.RegisterCallback<AttachToPanelEvent>(OnAttached);
            ApplyVisibility();
        }

        private ListView BindList(VisualElement container, UserListSession session)
        {
            if (container == null || session == null)
                return null;
            var listRoot = container.Q("userListRoot");
            listRoot.dataSource = session;
            var list = container.Q<ListView>("usersList");
            list.makeItem = () =>
            {
                var row = list.itemTemplate.CloneTree();
                var callbacks = new RowCallbacks(row, m_api, session);
                m_rowCallbacks.Add(callbacks);
                row.userData = callbacks;
                return row;
            };
            list.bindItem = (row, index) => row.dataSource = list.itemsSource[index];
            list.unbindItem = (row, index) => row.dataSource = null;
            list.destroyItem = row =>
            {
                if (row.userData is RowCallbacks callbacks)
                {
                    callbacks.Dispose();
                    m_rowCallbacks.Remove(callbacks);
                }
            };
            return list;
        }

        private void OpenGlobal()
        {
            m_globalOpen = true;
            ApplyVisibility();
        }

        private void CloseGlobal()
        {
            m_globalOpen = false;
            ApplyVisibility();
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (!ReferenceEquals(evt.target, m_overlayLayer))
                return;
            evt.StopPropagation();
            CloseGlobal();
        }

        private void SelectMessages()
        {
            m_usersSelected = false;
            ApplyVisibility();
        }

        private void SelectUsers()
        {
            m_usersSelected = true;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            m_overlay?.EnableInClassList(k_HiddenClass, !m_globalOpen);
            m_overlayLayer?.EnableInClassList(k_HiddenClass, !m_globalOpen);
            m_roomContent?.EnableInClassList(k_HiddenClass, !m_usersSelected);
            m_messages?.EnableInClassList(k_HiddenClass, m_usersSelected);
            m_messagesButton?.EnableInClassList("user-tab-selected", !m_usersSelected);
            m_usersButton?.EnableInClassList("user-tab-selected", m_usersSelected);
            bool attached = m_root?.panel != null;
            m_global?.SetVisible(attached && m_globalOpen);
            m_room?.SetVisible(attached && m_usersSelected);
            m_tick?.Pause();
            m_tick = null;
            if (attached && (m_globalOpen || m_usersSelected) && m_api.IsReady)
                m_tick = m_root.schedule.Execute(Tick).Every(k_PollTickMilliseconds);
        }

        private void Tick()
        {
            if (m_root == null || m_root.resolvedStyle.display == DisplayStyle.None)
            {
                m_global?.SetVisible(false);
                m_room?.SetVisible(false);
                m_tick?.Pause();
                return;
            }
            m_global?.Tick();
            m_room?.Tick();
        }

        private void OnSessionChanged()
        {
            if (!m_api.IsReady)
                m_globalOpen = false;
            ApplyVisibility();
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, m_root))
                ApplyVisibility();
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (ReferenceEquals(evt.target, m_root))
                Unbind();
        }

        public void Unbind()
        {
            m_tick?.Pause();
            m_tick = null;
            m_global?.SetVisible(false);
            m_room?.SetVisible(false);
            foreach (var callbacks in m_rowCallbacks)
                callbacks.Dispose();
            m_rowCallbacks.Clear();
            if (m_openButton != null)
                m_openButton.clicked -= OpenGlobal;
            if (m_closeButton != null)
                m_closeButton.clicked -= CloseGlobal;
            m_overlayLayer?.UnregisterCallback<ClickEvent>(OnBackdropClicked);
            if (m_messagesButton != null)
                m_messagesButton.clicked -= SelectMessages;
            if (m_usersButton != null)
                m_usersButton.clicked -= SelectUsers;
            m_root?.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            m_root?.UnregisterCallback<AttachToPanelEvent>(OnAttached);
            ClearList(m_overlay, m_globalList);
            ClearList(m_roomContent, m_roomList);
            m_root = null;
            m_overlay = null;
            m_overlayLayer = null;
            m_roomContent = null;
            m_messages = null;
            m_openButton = m_closeButton = m_messagesButton = m_usersButton = null;
            m_globalList = m_roomList = null;
        }

        private static void ClearList(VisualElement container, ListView list)
        {
            if (list == null)
                return;
            container.Q("userListRoot").dataSource = null;
            list.itemsSource = null;
            list.bindItem = null;
            list.unbindItem = null;
            list.makeItem = null;
            list.destroyItem = null;
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            Unbind();
            m_disposed = true;
            m_api.SessionChanged -= OnSessionChanged;
            m_global?.Dispose();
            m_room?.Dispose();
        }

        private sealed class RowCallbacks : IDisposable
        {
            private readonly VisualElement m_row;
            private readonly IUserPresenceApi m_api;
            private readonly UserListSession m_session;
            private readonly Button m_inRoomButton;
            private readonly Button m_playGameButton;
            private readonly Button m_disconnectButton;

            public RowCallbacks(VisualElement row, IUserPresenceApi api, UserListSession session)
            {
                m_row = row;
                m_api = api;
                m_session = session;
                m_inRoomButton = row.Q<Button>("userInRoomBtn");
                m_playGameButton = row.Q<Button>("userInRoomBtn");
                m_disconnectButton = row.Q<Button>("disconnectUserButton");
                if (m_inRoomButton != null)
                    m_inRoomButton.clicked += JoinInRoom;
                if (m_playGameButton != null)
                    m_playGameButton.clicked += JoinPlayGame;
                m_disconnectButton.clicked += Disconnect;
            }

            private void JoinInRoom()
            {
                if (m_row.dataSource is UserPresenceRowVM vm && vm.Data.InRoomId > 0)
                    m_api.JoinRoom(vm.Data.InRoomId);
            }

            private void JoinPlayGame()
            {
                if (m_row.dataSource is UserPresenceRowVM vm && vm.Data.HasGameSlot)
                    m_api.JoinRoom(vm.Data.GameSlot.GameId);
            }

            private void Disconnect()
            {
                if (m_row.dataSource is UserPresenceRowVM vm)
                    m_session.Disconnect(vm);
            }

            public void Dispose()
            {
                if (m_inRoomButton != null)
                    m_inRoomButton.clicked -= JoinInRoom;
                m_disconnectButton.clicked -= Disconnect;
                m_row.dataSource = null;
            }
        }
    }
}
