// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Threading.Tasks;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    public interface IGameEngine : IDisposable
    {
        NetFoobbuteoTeam ToNetHomeTeam { get; }
        NetFoobbuteoTeam ToNetAwayTeam { get; }

        public event Action GameStarted;
        public event Action TurnStarted;
        public event Action<ProcessingStartedMessage> ProcessingStarted;
        public event Action<TurnResultReadyMessage> TurnResultReady;
        public event Action GameEnded;

        Task<FoobbuteoPlayerState> UserClaimPlayerAsync(ulong senderClientId, PlayerKey playerKey);
        Task<PlayerCommandResponse> AddPlayerMoveAsync(PlayerCommand command, ulong senderClientId);
        Task ChangePlayerModeAsync(PlayerModeCommand cmd, ulong senderClientId);
        Task JoinMvMGameAsync(ulong senderClientId, TeamKey team);

        Task<MvMLineupResult> PrepareMvMLineupAsync(ulong clientId, int[] expected, int[] desired, bool save);
        TurnStageNetState GetTurnStage();

        // GameRoomNB gameRoomNB
        bool RemoveClientFromGame(ulong senderClientId);
        void InitGame();

        void CloseGame();

        FixedGameInfoNetState ToFixedGameInfoNetState(long baseServerTime);
        GameDetailsContract ToGameDetailsContract();

#if UNITY_EDITOR
        bool EditorTryGetPlayerPosition(TeamIndex teamIdx, byte playerIdx, out Vector2 position);
        Vector2 EditorGetBallPosition();
        string EditorQueuePlayerMove(TeamIndex teamIdx, byte playerIdx, Vector2 target);
        string EditorMoveBall(Vector2 target);
        void EditorRefreshPositioningBounds();
#endif
    }
}
