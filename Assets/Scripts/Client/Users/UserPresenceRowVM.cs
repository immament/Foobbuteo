// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Shared.Users;
using Unity.Netcode;
using Unity.Properties;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Users
{
    public sealed class UserPresenceRowVM
    {
        public UserPresenceRow Data { get; }

        [CreateProperty]
        public string PlayerName => Data.PlayerName;

        [CreateProperty]
        public string Identifiers { get; }

        [CreateProperty]
        public bool InRoomBtnVisible { get; }

        [CreateProperty]
        public string InRoomBtnText { get; }

        [CreateProperty]
        public bool PlayGameBtnVisible { get; }

        [CreateProperty]
        public string PlayGameBtnText { get; }

        [CreateProperty]
        public string Status { get; }

        [CreateProperty]
        public bool CanDisconnect { get; }

        [CreateProperty]
        public Sprite Shirt { get; }

        [CreateProperty]
        public string ShirtNumber { get; } = "";

        [CreateProperty]
        public Color ShirtTextColor { get; }

        [CreateProperty]
        public bool HasShirt => Shirt != null;

        public UserPresenceRowVM(UserPresenceRow data, bool forAdmin)
        {
            Data = data;

            PlayGameBtnVisible = Data.HasGameSlot;
            if (PlayGameBtnVisible)
            {
                PlayGameBtnText = $"Play game #{Data.GameSlot.GameId}";
                InRoomBtnVisible = Data.InRoomId > 0 && Data.InRoomId != Data.GameSlot.GameId;
            }
            else
            {
                InRoomBtnVisible = Data.InRoomId > 0;
            }
            if (InRoomBtnVisible)
            {
                InRoomBtnText = $"In room #{Data.InRoomId}";
            }

            Status = !data.IsConnected ? "Disconnected" : "";
            if (data.HasGameSlot && data.GameSlot.IsManager)
                Status = string.IsNullOrEmpty(Status) ? "Manager" : $"Manager · {Status}";

            CanDisconnect = forAdmin && data.IsConnected && data.ClientId != NetworkManager.ServerClientId;

            Identifiers = CreateIdentifiers(forAdmin);
        }

        public UserPresenceRowVM(UserPresenceRow data, bool forAdmin, int listRoomId, TeamVM team)
        {
            Data = data;

            Status =
                !data.IsConnected ? "Disconnected"
                : data.InRoomId == listRoomId ? "In room"
                : "Connected · Elsewhere";
            CanDisconnect = false;
            Identifiers = CreateIdentifiers(forAdmin);

            bool playsThisRoom = data.HasGameSlot && data.GameSlot.GameId == listRoomId;
            if (playsThisRoom && team != null)
            {
                Shirt = team.Shirt;
                ShirtNumber = data.GameSlot.IsPlayer ? data.GameSlot.PlayerIdx.ToString() : "";
                ShirtTextColor = team?.ShirtTextColor ?? Color.white;
                if (data.GameSlot.IsManager)
                    Status = $"Manager · {team.Name} · {Status}";
            }
        }

        private string CreateIdentifiers(bool forAdmin)
        {
            return forAdmin
                ? $"#{Data.PlayerId} @{Data.UserId} ^{(Data.IsConnected ? Data.ClientId.ToString() : "—")}"
                : $"#{Data.PlayerId}";
        }
    }
}
