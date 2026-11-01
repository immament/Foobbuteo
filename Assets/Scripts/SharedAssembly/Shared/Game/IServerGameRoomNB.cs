// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    public interface IServerGameRoomNB : IDisposable
    {
        public int GameId { get; }

        void InitServer(IGameEngine serverGameRoomNBCtrl);
        public Coroutine StartGameCoroutine(IEnumerator routine);
        public void AddClientToGameRoom(ulong clientId);

        public void UpdateTeamInfo(TeamIndex teamIdx);
        void UpdateStartTime();
        void UpdatePlayerControlState(GameDetails_PlayerContract player);

        public void CloseGameRoom(ulong clientId);

        public void Close();

        // void StartServerGame(NetworkObject networkObject);
        // void Clean();
    }
}
