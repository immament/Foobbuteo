// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Shared.Game
{
    public interface IClientGameRoomNB : INetworkBehaviour, IDisposable
    {
        // NetworkBehaviour
        bool IsHost { get; }

        // public event Action<TurnStateChangedMessage> TurnStageChanged;
        event Action<TurnStateChangedMessage> TurnStarted;
        event Action<TurnStateChangedMessage> ProcessingStarted;
        event Action<TurnResultReadyMessage> TurnResultReady;
        event Action<GameDetailsContract> GameDetailsReady;
        event Action<PlayerCommandResponse> CommandResponseReceived;
        event Action<GameDetails_PlayerContract> PlayerControlClaimed;
        event Action<GameDetails_PlayerContract> PlayerControlStateChanged;
        event Action<TeamIndex, NetFoobbuteoTeam> TeamInfoChanged;
        event Action<TurnStateChangedMessage> GameEnded;
        event Action<MvMJoinResult> MvMJoinCompleted;

        event Action<MvMLineupResult> MvMLineupLoaded;
        event Action<MvMLineupResult> MvMLineupSaved;
        event Action<DateTimeOffset> StartTimeChanged;
        void LoadMvMLineup();
        void SaveMvMLineup(int[] expected, int[] desired);
        void InitClient();

        void UserJoinedRoom();
        void RequestLeaveGame();
        BaseGameInfo GetBaseGameInfo();

        // public void LeaveGameRpc(RpcParams rpcParams = default);
        void SendMoveCmd(PlayerCommand cmd);
        void SendModeCmd(PlayerModeCommand cmd);

        void RequestPlayerControl(PlayerKey playerKey);
        void JoinMvMGame(TeamKey team);

        // void SetGameDetails(GameDetailsContract info);
        // void HandleAlert(string message);
        // void PlayerCmdResponse(PlayerCommandResponse playerCmdResponse);
        // void InvokeTurnResultReady(TurnResultReadyMessage msg);
        // void InvokePlayerControlClaimed(GameDetails_PlayerContract gameDetails_PlayerContract);
    }

    // {
    //     event Action<TurnStartedMessage> TurnStarted;
    //     event Action<ProcessingStartedMessage> ProcessingStarted;
    //     event Action<TurnResultReadyMessage> TurnResultReady;

    //     BaseMatchInfo GetBaseMatchInfo();

    //     // Awaitable<GameInfoContract> LoadInitData();
    //     // Awaitable<GameDetailsContract> LoadMatchDetails();

    //     void LeaveGame();

    //     void SendMove(PlayerCommand command);
    //     void ClientJoinGame();
    // }
}
