// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.dev;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.dev
{
    public class DevServerMockData
    {
        private readonly bool m_loadInitialGames;
        private bool m_forEdtitorTest;

        internal DevServerMockData(bool loadInitialGames = false)
        {
            m_loadInitialGames = loadInitialGames;
        }

        internal static DevServerMockData ForEditorTest(bool loadInitialGames)
        {
            return new DevServerMockData(loadInitialGames) { m_forEdtitorTest = true };
        }

        private static int TestUserId => 600101;
        private static int TestPlayerId => 31933;

        public static int AdminUserId => 600177;
        public static int EditorUserId => 92001;
        public static int EditorPlayerId => 93001;
        public static int EditorClientUserId => 92002;
        public static int EditorClientPlayerId => 93002;

        private static int m_counter = 0;

        // private static int m_lastGameId = 2000;

        // ++ States +++++++++++++++

        internal List<ServerGameState> CreateStateGameList(IUserRegistry userRegistry)
        {
            if (!m_loadInitialGames)
                return new List<ServerGameState>();

            int gameId = 94000;
            List<ServerGameState> games = m_forEdtitorTest
                ? new()
                {
                    CreateTestGame_Ended(++gameId),
                    CreateTestGame_Ongoing(++gameId),
                    CreateTestGame_Ongoing_EditorCaptain(++gameId),
                    CreateTestGame_Ongoing_BeforeEnd(++gameId),
                    CreateTestGame_Pending(++gameId, 120),
                }
                : new()
                {
                    // CreateTestGame_Ended(++gameId),
                    // CreateTestGame_Ongoing(++gameId),
                    // CreateTestGame_Ongoing_EditorCaptain(++gameId),
                    // CreateTestGame_Ongoing_BeforeEnd(++gameId),
                    CreateTestGame_Pending(++gameId, 1000, FoobbuteoGameType.MvM),
                };

            return games;

#pragma warning disable CS8321 // Local function is declared but never used
            ServerGameState CreateTestGame_Ended(int aGameId)
            {
                return ServerGameState.CreateForTest(
                    gameId: aGameId,
                    status: FoobbuteoGameStatus.Ended,
                    turn: 30,
                    turns: 30,
                    startDate: new DateTime(2026, 5, 9, 17, 0, 0, DateTimeKind.Utc),
                    endDate: new DateTime(2026, 5, 9, 17, 20, 0, DateTimeKind.Utc),
                    homeTeam: new ServerTeamState(
                        TeamIndex.Home,
                        id: 536993,
                        name: "Miguel Canelas [Ended]",
                        color: ShirtColorEnum.Red,
                        score: 1,
                        players: RegisterPlayers(aGameId, CreateStatePlayersA(aGameId), userRegistry)
                    ),
                    awayTeam: new ServerTeamState(
                        TeamIndex.Away,
                        id: TestUserId,
                        name: "Aleksander Kołodziej-Hajdo",
                        color: ShirtColorEnum.Black,
                        score: 2,
                        players: RegisterPlayers(aGameId, CreateStatePlayersB(aGameId), userRegistry)
                    )
                );
            }

            ServerGameState CreateTestGame_Ongoing(int aGameId)
            {
                return ServerGameState.CreateForTest(
                    gameId: aGameId,
                    status: FoobbuteoGameStatus.Ongoing,
                    turn: ++m_counter % 90,
                    turns: 90,
                    startDate: DateTime.UtcNow,
                    homeTeam: new ServerTeamState(
                        TeamIndex.Home,
                        id: 8311,
                        name: "Serge Willems",
                        color: ShirtColorEnum.Orange,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersA(aGameId, withBallIdx: 1), userRegistry)
                    ),
                    awayTeam: new ServerTeamState(
                        TeamIndex.Away,
                        id: -2,
                        name: "---",
                        color: ShirtColorEnum.Green,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersB(aGameId), userRegistry)
                    )
                );
            }

            ServerGameState CreateTestGame_Ongoing_EditorCaptain(int aGameId)
            {
                return ServerGameState.CreateForTest(
                    gameId: aGameId,
                    status: FoobbuteoGameStatus.Ongoing,
                    turn: ++m_counter % 90,
                    turns: 90,
                    startDate: DateTime.UtcNow,
                    homeTeam: new ServerTeamState(
                        TeamIndex.Home,
                        id: EditorUserId,
                        name: "Editor User",
                        color: ShirtColorEnum.Orange,
                        score: 1,
                        players: RegisterPlayers(aGameId, CreateStatePlayersA(aGameId), userRegistry)
                    ),
                    awayTeam: new ServerTeamState(
                        TeamIndex.Away,
                        id: 98281,
                        name: "Costantino Rusconi",
                        color: ShirtColorEnum.Green,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersB(aGameId), userRegistry)
                    )
                );
            }

            ServerGameState CreateTestGame_Ongoing_BeforeEnd(int aGameId)
            {
                return ServerGameState.CreateForTest(
                    gameId: aGameId,
                    status: FoobbuteoGameStatus.Pending,
                    turn: 29,
                    turns: 30,
                    startDate: DateTime.UtcNow.AddMinutes(10),
                    homeTeam: new ServerTeamState(
                        TeamIndex.Home,
                        id: 8311,
                        name: "Serge Willems",
                        color: ShirtColorEnum.Orange,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersA(aGameId), userRegistry)
                    ),
                    awayTeam: new ServerTeamState(
                        TeamIndex.Away,
                        id: 98281,
                        name: "Costantino Rusconi",
                        color: ShirtColorEnum.Green,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersB(aGameId), userRegistry)
                    )
                );
            }

            ServerGameState CreateTestGame_Pending(
                int aGameId,
                float startInSeconds,
                FoobbuteoGameType matchType = FoobbuteoGameType.PvP
            )
            {
                return ServerGameState.CreateForTest(
                    gameId: aGameId,
                    turns: 40,
                    startDate: DateTime.UtcNow.AddSeconds(startInSeconds),
                    matchType: matchType,
                    homeTeam: new ServerTeamState(
                        TeamIndex.Home,
                        id: EditorUserId,
                        name: "Miguel Canelas " + EditorUserId,
                        color: ShirtColorEnum.Brown,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersA(aGameId), userRegistry),
                        teamKey: new TeamKey(SourceGame.Footstar, TeamType.Club, 1, AgeCategory.Main)
                    ),
                    awayTeam: new ServerTeamState(
                        TeamIndex.Away,
                        id: TestUserId,
                        name: "Aleksander Kołodziej-Hajdo",
                        color: ShirtColorEnum.Purple,
                        score: 0,
                        players: RegisterPlayers(aGameId, CreateStatePlayersB(aGameId), userRegistry)
                    )
                );
            }
#pragma warning restore CS8321 // Local function is declared but never used
        }

        private static ServerPlayerState[] CreateStatePlayersA(
            int gameId,
            TeamIndex teamIdx = TeamIndex.Home,
            int withBallIdx = 0
        )
        {
            byte playerIdx = 0;
            return new ServerPlayerState[]
            {
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 17951,
                    name: "Miguel Canelas",
                    // position: new Vector2(10f, 210f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    playerMode: PlayerModeEnum.GkOutOfArea,
                    countryId: 1,
                    userId: 1,
                    // isCaptain: true,
                    hasBall: withBallIdx == playerIdx,
                    confrontationSkill: 20,
                    gkPassSkill: 20,
                    gkSavesSkill: 20,
                    outOfAreaSkill: 20
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 999,
                    name: "Costantino Rusconi",
                    Formation442.GetPosition(teamIdx, playerIdx),
                    userId: 111,
                    hasBall: withBallIdx == playerIdx,
                    countryId: 6
                // userId: EditorUserId
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 8311,
                    name: "Serge Willems",
                    // position: new Vector2(90f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    // userId: 112,
                    countryId: 7,
                    confrontationSkill: 4
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 8290,
                    name: "Stef Kramer Stef KramerStef KramerStef KramerStef KramerStef KramerStef KramerStef Kramer",
                    // position: new Vector2(90f, 160f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    // userId: 113,
                    countryId: 7,
                    moveSkill: 4,
                    confrontationSkill: 4,
                    lowPassSkill: 3,
                    longPassSkill: 3,
                    crossesSkill: 3,
                    shootingSkill: 3,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 5605,
                    name: "Frits Tillaart",
                    // position: new Vector2(90f, 60f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    // userId: 114,
                    countryId: 5,
                    moveSkill: 1,
                    confrontationSkill: 1,
                    lowPassSkill: 2,
                    longPassSkill: 2,
                    crossesSkill: 2,
                    shootingSkill: 1,
                    longShotsSkill: 2
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 19048,
                    name: "José Fraga",
                    // position: new Vector2(150f, 160f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    countryId: 1
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 45221,
                    name: "Raúl Taboada",
                    // position: new Vector2(200f, 360f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    countryId: 93
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 32470,
                    name: "Thomas Monkko",
                    // position: new Vector2(150f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    countryId: 21,
                    moveSkill: 3,
                    confrontationSkill: 4,
                    lowPassSkill: 3,
                    longPassSkill: 3,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 92492,
                    name: "Vladimir Ortega",
                    // position: new Vector2(250f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    countryId: 31
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    // playerId: 65295,
                    // name: "Ledley Partridge",
                    playerId: EditorPlayerId,
                    name: "Editor User",
                    position: new Vector2(300f, 190f),
                    hasBall: withBallIdx == playerIdx,
                    // Formation442.GetPosition(teamIdx, playerIdx),
                    // userId: EditorUserId,
                    // hasBall: hasBall,
                    countryId: 119,
                    playerLevel: 50,
                    moveSkill: 20,
                    confrontationSkill: 20,
                    lowPassSkill: 20,
                    longPassSkill: 20,
                    gkPassSkill: 20,
                    crossesSkill: 20,
                    shootingSkill: 20,
                    longShotsSkill: 20,
                    gkSavesSkill: 20,
                    outOfAreaSkill: 20
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 99407,
                    name: "Colin Bruneau",
                    // position: new Vector2(200f, 60f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    hasBall: withBallIdx == playerIdx,
                    countryId: 30
                ),
            };
        }

        private static ServerPlayerState[] CreateStatePlayersB(int gameId, TeamIndex teamIdx = TeamIndex.Away)
        {
            byte playerIdx = 0;
            return new ServerPlayerState[]
            {
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 39827,
                    name: "Jonas Lämmler",
                    Formation442.GetPosition(teamIdx, playerIdx),
                    playerMode: PlayerModeEnum.GkSave,
                    userId: 2,
                    countryId: 27,
                    moveSkill: 1,
                    confrontationSkill: 20,
                    lowPassSkill: 1,
                    longPassSkill: 1,
                    gkPassSkill: 20,
                    crossesSkill: 1,
                    shootingSkill: 1,
                    longShotsSkill: 2,
                    gkSavesSkill: 20,
                    outOfAreaSkill: 20
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 59845,
                    name: "James LiVolti",
                    // position: new Vector2(520f, 60f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 23,
                    moveSkill: 3,
                    confrontationSkill: 2,
                    shootingSkill: 3,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 124080,
                    name: "Dean Leahy",
                    // position: new Vector2(520f, 160f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 13,
                    moveSkill: 3,
                    confrontationSkill: 3,
                    longPassSkill: 3,
                    shootingSkill: 3,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 50102,
                    name: "Laszlo Léon",
                    // position: new Vector2(520f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 3,
                    lowPassSkill: 3,
                    longPassSkill: 3,
                    crossesSkill: 3,
                    shootingSkill: 2,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 85453,
                    name: "Eemeli Kykkänen",
                    // position: new Vector2(520f, 360f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 21
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 38212,
                    name: "Qiao ChaoBao",
                    // position: new Vector2(460f, 150),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 18,
                    confrontationSkill: 4,
                    lowPassSkill: 3,
                    longPassSkill: 3,
                    crossesSkill: 3,
                    shootingSkill: 3,
                    longShotsSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 115510,
                    name: "Ronnie Rosenthal",
                    // position: new Vector2(410f, 60f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 247,
                    moveSkill: 2,
                    confrontationSkill: 2,
                    lowPassSkill: 2,
                    longPassSkill: 1,
                    crossesSkill: 2,
                    shootingSkill: 1,
                    longShotsSkill: 1
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 35778,
                    name: "Niels Aaen",
                    // position: new Vector2(460f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 78
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 50976,
                    name: "Romain Saha",
                    // position: new Vector2(360f, 260f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 3,
                    moveSkill: 3,
                    confrontationSkill: 3,
                    lowPassSkill: 3,
                    longPassSkill: 3
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: TestPlayerId,
                    name: "Aleksander Kołodziej",
                    // position: new Vector2(360f, 160f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 68,
                    // userId: EditorClientUserId,
                    // isCaptain: true,
                    confrontationSkill: 4,
                    lowPassSkill: 6,
                    longPassSkill: 6,
                    crossesSkill: 6,
                    shootingSkill: 7,
                    longShotsSkill: 6
                ),
                ServerPlayerState.CreateForTest(
                    gameId,
                    teamIdx,
                    ++playerIdx,
                    playerId: 113867,
                    name: "Abu Adnan",
                    // position: new Vector2(410f, 360f),
                    Formation442.GetPosition(teamIdx, playerIdx),
                    countryId: 20,
                    confrontationSkill: 3,
                    lowPassSkill: 2,
                    longPassSkill: 2,
                    crossesSkill: 3,
                    shootingSkill: 2,
                    longShotsSkill: 3
                ),
            };
        }

        // Rand colors

        private static readonly ShirtColorEnum[] AllShirtColors = (ShirtColorEnum[])
            Enum.GetValues(typeof(ShirtColorEnum));

        internal static ShirtColorEnum RandShirtColor(ShirtColorEnum except)
        {
            int count = AllShirtColors.Length - 1;
            if (count <= 0)
                return except;

            int target = UnityEngine.Random.Range(0, count);

            for (int i = 0; i < AllShirtColors.Length; i++)
            {
                if (AllShirtColors[i] == except)
                    continue;
                if (target-- == 0)
                    return AllShirtColors[i];
            }

            return except;
        }

        internal static ShirtColorEnum RandShirtColor()
        {
            return AllShirtColors[UnityEngine.Random.Range(0, AllShirtColors.Length)];
        }

        private ServerPlayerState[] RegisterPlayers(int gameId, ServerPlayerState[] players, IUserRegistry userRegistry)
        {
            foreach (var player in players)
            {
                if (player.UserId > 0)
                {
                    if (userRegistry.TryGetUser(player.UserId, out var user))
                    {
                        if (!user.GameSlot.HasValue)
                        {
                            user.RestoreGameSlot(player.Slot);
                            this.LTrace(() => $"@{player.UserId} RestoreGameSlot: " + player.Slot);
                        }
                        else
                        {
                            this.LTrace(() => $"@{player.UserId} Not RestoreGameSlot: {player.Slot}");
                        }
                        continue;
                    }

                    if (
                        userRegistry.RegisterUser(
                            new ServerUser(player, gameId),
                            validateClientId: false,
                            dev_forceNew: true
                        )
                        is string msg
                    )
                    {
                        this.LWarn("Mock user register problem: " + msg);
                    }
                    else
                    {
                        this.LDebug(() => $"@{player.UserId} registered {player.Slot}");
                    }
                }
            }
            return players;
        }
    }
}
