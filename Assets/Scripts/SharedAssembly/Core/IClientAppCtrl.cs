// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;
using Imma.Foobbuteo.Shared;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Core
{
    public interface IClientAppCtrl
    {
        GameUser User { get; set; }
        event Action<GameUser> UserInfoUpdated;
        bool IsClientConnected { get; }
        bool IsClientConnecting { get; }
        event Action ClientConnectionStateChanged;
        bool IsAuthenticating { get; }
        bool IsAuthenticationReady { get; }
        bool CanRememberLogin { get; }
        bool HasRememberedLogin { get; }
        string AuthenticationMessage { get; }
        ClientSessionView SessionView { get; }
        bool CanLogout { get; }
        int OpenGameId { get; }
        void OnLobbySpawned();
        event Action AuthenticationStateChanged;
        Task LoginAsync(string login, string password, bool remember);
        Task RetryRememberedLoginAsync();
        Task LogoutAsync();

        bool OpenLobbyScene();
        void OpenGameScene(int gameId);
        void StartClient();

        void DevDisconnect();
        void SetRootElement(VisualElement rootElement);

        void ShowToast(string message, float durationInSeconds = 3.0f);
        void ShowBanner(string message, float durationInSeconds = 3.0f);

        // IClientGameRoomNBCtrl ClientGameRoomNBCtrl(GameRoomNB gameRoomNB);
    }
}
