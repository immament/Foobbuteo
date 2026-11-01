// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.NetState;
using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game
{
    public partial class GameRoomNB
    {
        public event Action<MvMLineupResult> MvMLineupLoaded;
        public event Action<MvMLineupResult> MvMLineupSaved;
        public event Action<DateTimeOffset> StartTimeChanged;

        void IClientGameRoomNB.LoadMvMLineup()
        {
            if (CanSendGameAction)
                ToServer.ToServer_MvMLineupRpc(false, Array.Empty<int>(), Array.Empty<int>());
        }

        void IClientGameRoomNB.SaveMvMLineup(int[] expected, int[] desired)
        {
            if (CanSendGameAction)
                ToServer.ToServer_MvMLineupRpc(true, expected, desired);
        }

        private void Client_OnFixedGameInfoChanged(FixedGameInfoNetState previous, FixedGameInfoNetState current) =>
            StartTimeChanged?.Invoke(current.StartDateTime);

        [Rpc(SendTo.SpecifiedInParams, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClient_MvMLineupRpc(bool save, MvMLineupResult result, RpcParams rpcParams)
        {
            if (save)
                MvMLineupSaved?.Invoke(result);
            else
                MvMLineupLoaded?.Invoke(result);
        }

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        void IToClientRpc_GameRoomNB.ToClients_LineupChangedRpc(GameDetailsContract details) =>
            GameDetailsReady?.Invoke(details);

        [Rpc(SendTo.Server, DeferLocal = true)]
        void IToServerRpc_GameRoomNB.ToServer_MvMLineupRpc(
            bool save,
            int[] expected,
            int[] desired,
            RpcParams rpcParams
        )
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!AllowIncomingRpc(clientId))
            {
                ToClient.ToClient_MvMLineupRpc(
                    save,
                    new MvMLineupResult
                    {
                        Error = "Please try again shortly.",
                        Squad = Array.Empty<GameDetails_PlayerContract>(),
                        Lineup = Array.Empty<int>(),
                    },
                    RpcTarget.Single(clientId, RpcTargetUse.Temp)
                );
                return;
            }
            _ = HandleLineupAsync(clientId, save, expected, desired);
        }

        private async Task HandleLineupAsync(ulong clientId, bool save, int[] expected, int[] desired)
        {
            MvMLineupResult result;
            try
            {
                result = await m_serverGameEngine.PrepareMvMLineupAsync(clientId, expected, desired, save);
            }
            catch (Exception exception)
            {
                this.LExceptionTree(exception, "MvM lineup request failed");
                result = new MvMLineupResult
                {
                    Error = "The lineup request failed. Please try again.",
                    Squad = Array.Empty<GameDetails_PlayerContract>(),
                    Lineup = Array.Empty<int>(),
                };
            }
            if (!IsSpawned)
                return;
            if (save && result.Success)
                ToClient.ToClients_LineupChangedRpc(m_serverGameEngine.ToGameDetailsContract());
            ToClient.ToClient_MvMLineupRpc(save, result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }
}
