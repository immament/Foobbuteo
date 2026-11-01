// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk
#if UNITY_EDITOR
using System;
using System.Linq;
using Imma.Foobbuteo.Client.Lobby.UI;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Editor
{
    // TODO: Only DEV
    [CreateAssetMenu(fileName = "DevGameListData", menuName = "Foobbuteo/Dev/DevGameListData")]
    public class DevGameListData : ScriptableObject
    {
        public LobbyVM GamesVm = null;

        // WARN: Only works with one instance
        private static bool s_selectFirstOnResetLast;
        public bool SelectFirstOnReset;

        private void OnValidate()
        {
            s_selectFirstOnResetLast = SelectFirstOnReset;
        }

        public void Reset()
        {
            Debug.LogFormat("Reset, SelectFirstOnReset: {0}", SelectFirstOnReset);
            // Debug.Log($"DevGameListData::Reset: {GamesVm == null}");
            // TODO: to remove - Only tmp DEV
            // LocalServerMockData dev_mockData = new();
            var games = Editor_GetGamesContract();
            var gameListVM = new LobbyVM(games.Select(src => GameListItemVM.FromContract(src)).ToList());

            SelectFirstOnReset = s_selectFirstOnResetLast;

            if (SelectFirstOnReset)
            {
                gameListVM.SelectedGame = gameListVM.Games[0];
            }
            GamesVm = gameListVM;
        }

        // private DateTime Fix(DateTime date)
        // {
        //     DateTimeOffset offset = (DateTimeOffset)date;
        //     return offset.LocalDateTime;
        // }

        private GameInfoContract[] Editor_GetGamesContract()
        {
            var result = new GameInfoContract[]
            {
                new()
                {
                    GameId = 99001,
                    Status = FoobbuteoGameStatus.Ended,
                    Turn = 30,
                    NumberOfTurns = 30,
                    HasResult = true,
                    OwnerScore = 2,
                    OpponentScore = 1,
                    StartDateTime = new DateTime(2026, 5, 9, 17, 20, 0, DateTimeKind.Utc),
                    // CurrentDateTime = new DateTime(2026, 6, 9, 9, 41, 26),
                    // EndDateTime = new DateTime(2026, 5, 9, 17, 41, 57),
                    OwnerUid = 10,
                    OpponentUid = 536993,
                    OwnerName = "Bartłomiej Sarnat",
                    OpponentName = "Miguel Canelas",
                    MatchType = FoobbuteoGameType.PvP,
                    // OwnerCountryId = 1,
                    // OpponentCountryId = 2,
                    // OwnerRanking = 12,
                    // OpponentRanking = 33,
                    // Postponed = 0,
                    OwnerColor = ShirtColorEnum.Green,
                    OpponentColor = ShirtColorEnum.Orange,
                },
                new()
                {
                    GameId = 99002,
                    Status = FoobbuteoGameStatus.Ongoing,
                    Turn = 11,
                    NumberOfTurns = 30,
                    HasResult = true,
                    OwnerScore = 1,
                    OpponentScore = 0,
                    StartDateTime = new DateTime(2026, 5, 9, 17, 0, 0, DateTimeKind.Utc),
                    // CurrentDateTime = new DateTime(2026, 6, 9, 9, 41, 26),
                    OwnerUid = 11,
                    OpponentUid = 536993,
                    OwnerName = "Roque Vilar",
                    OpponentName = "Miguel Canelas",
                    MatchType = FoobbuteoGameType.PvP,
                    // OwnerCountryId = 1,
                    // OpponentCountryId = 2,
                    // OwnerRanking = 12,
                    // OpponentRanking = 33,
                    // // EndDate = new DateTime(2026, 6, 2, 8, 41, 57),
                    // Postponed = 0,
                    OwnerColor = ShirtColorEnum.Purple,
                    OpponentColor = ShirtColorEnum.White,
                },
                new()
                {
                    GameId = 99003,
                    Status = FoobbuteoGameStatus.Pending,
                    Turn = 0,
                    NumberOfTurns = 30,
                    StartDateTime = new DateTime(2026, 7, 9, 18, 10, 0, DateTimeKind.Utc),
                    // CurrentDateTime = new DateTime(2026, 7, 9, 12, 0, 0),
                    OwnerUid = 12,
                    OpponentUid = 536993,
                    OwnerName = "Aleksander Kołodziej-Hajdo",
                    OpponentName = "Miguel Canelas",
                    MatchType = FoobbuteoGameType.PvP,
                    // OwnerCountryId = 1,
                    // OpponentCountryId = 2,
                    // OwnerRanking = 12,
                    // OpponentRanking = 33,
                    // // EndDate = new DateTime(2026, 6, 2, 8, 41, 57),
                    // Postponed = 0,
                    OwnerColor = ShirtColorEnum.Brown,
                    OpponentColor = ShirtColorEnum.Red,
                },
                new()
                {
                    GameId = 99004,
                    Status = FoobbuteoGameStatus.Pending,
                    Turn = 0,
                    NumberOfTurns = 30,
                    StartDateTime = new DateTime(2026, 7, 9, 12, 11, 0, DateTimeKind.Utc),
                    // CurrentDateTime = new DateTime(2026, 7, 9, 12, 0, 0),
                    OwnerUid = 13,
                    OpponentUid = 536993,
                    OwnerName = "Aleksander Kołodziej",
                    OpponentName = "Miguel Canelas",
                    MatchType = FoobbuteoGameType.PvP,
                    // OwnerCountryId = 1,
                    // OpponentCountryId = 2,
                    // OwnerRanking = 12,
                    // OpponentRanking = 33,
                    // // EndDate = new DateTime(2026, 6, 2, 8, 41, 57),
                    // Postponed = 0,
                    OwnerColor = ShirtColorEnum.Brown,
                    OpponentColor = ShirtColorEnum.Red,
                },
            };
            return result;

            // MatchInfoContract match = JsonConvert.DeserializeObject<MatchInfoContract>(jsonString);
        }
    }
}
#endif
