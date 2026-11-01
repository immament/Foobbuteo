// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Game.NetState;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    public class ClientGameStateTests
    {
        private const int k_gameId = 701;
        private const int k_homeTeamId = 801;
        private const int k_awayTeamId = 802;

        [Test]
        public void FromContract_CopiesMatchState()
        {
            BaseGameInfo info = CreateBaseGameInfo();

            ClientGameState state = ClientGameState.FromContract(info);

            Assert.AreEqual(k_gameId, state.GameId);
            Assert.AreEqual(FoobbuteoGameType.PvP, state.MatchType);
            Assert.AreEqual(FoobbuteoGameStatus.Ongoing, state.Status);
            Assert.AreEqual(7, state.CurrentTurn);
            Assert.AreEqual(30, state.NumberOfTurns);
            Assert.AreEqual(TurnStageEnum.PlayerMove, state.TurnStage);
            Assert.AreEqual(12f, state.StageStartTime);
            Assert.AreEqual(42f, state.StageEndTime);
        }

        [Test]
        public void LoadDetails_PopulatesTeamsAndPlayerLookup()
        {
            ClientGameState state = ClientGameState.FromContract(CreateBaseGameInfo());

            state.LoadDetails(CreateGameDetails());

            Assert.AreEqual(k_homeTeamId, state.HomeTeam.Id);
            Assert.AreEqual("Home", state.HomeTeam.Name);
            Assert.AreEqual(ShirtColorEnum.Blue, state.HomeTeam.Color);
            Assert.AreEqual(k_awayTeamId, state.AwayTeam.Id);
            Assert.AreEqual("Away", state.AwayTeam.Name);
            Assert.AreEqual(ShirtColorEnum.Red, state.AwayTeam.Color);

            ClientPlayerState homePlayer = state.GetPlayer(TeamIndex.Home, 5);
            Assert.AreEqual(new PlayerKey(TeamIndex.Home, 5), homePlayer.Slot.PlayerKey);
            Assert.AreEqual(1005, homePlayer.PlayerId);
            Assert.AreEqual("Home 5", homePlayer.Name);
            Assert.AreEqual(new Vector2(50f, 25f), homePlayer.Position);
            Assert.AreSame(homePlayer, state.GetPlayer(new PlayerKey(TeamIndex.Home, 5)));
            Assert.AreSame(homePlayer, state.HomeTeam.GetPlayerAtIdx(5));

            ClientPlayerState captain = state.GetPlayer(TeamIndex.Home, 1);
            Assert.IsTrue(captain.IsCaptain);
        }

        [Test]
        public void UpdateTeamInfo_UpdatesLoadedTeamIdentity()
        {
            ClientGameState state = ClientGameState.FromContract(CreateBaseGameInfo());
            state.LoadDetails(CreateGameDetails());
            var teamInfo = new NetFoobbuteoTeam
            {
                Id = 999,
                Name = "New captain",
                Color = ShirtColorEnum.Blue,
            };

            state.UpdateTeamInfo(TeamIndex.Home, teamInfo);

            Assert.AreEqual(teamInfo.Id, state.HomeTeam.Id);
            Assert.AreEqual(teamInfo.Name, state.HomeTeam.Name);
        }

        [Test]
        public void GameHudVM_UpdateTeamInfo_NotifiesRootTeamBinding()
        {
            GameHudVM vm = ScriptableObject.CreateInstance<GameHudVM>();
            vm.TeamB = new TeamVM(TeamIndex.Away, k_awayTeamId, "Away", null, null, ShirtColorEnum.Red);
            int notificationCount = 0;
            vm.propertyChanged += (_, _) => notificationCount++;
            var teamInfo = new NetFoobbuteoTeam
            {
                Id = 999,
                Name = "New captain",
                Color = ShirtColorEnum.Red,
            };

            try
            {
                vm.UpdateTeamInfo(TeamIndex.Away, teamInfo);

                Assert.AreEqual(teamInfo.Name, vm.TeamB.Name);
                Assert.AreEqual(1, notificationCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void LoadDetails_ForDifferentGame_Throws()
        {
            ClientGameState state = ClientGameState.FromContract(CreateBaseGameInfo());
            GameDetailsContract details = CreateGameDetails(k_gameId + 1);

            AppException exception = Assert.Throws<AppException>(() => state.LoadDetails(details));

            StringAssert.Contains("do not match", exception.Message);
        }

        [Test]
        public void LoadDetails_WithInvalidPlayerSlot_Throws()
        {
            ClientGameState state = ClientGameState.FromContract(CreateBaseGameInfo());
            GameDetails_TeamContract homeTeam = CreateTeamDetails(TeamIndex.Home, k_homeTeamId, "Home", 1000);
            homeTeam.Players[4].PlayerIdx = 6;
            var details = new GameDetailsContract(
                k_gameId,
                Vector2.zero,
                homeTeam,
                CreateTeamDetails(TeamIndex.Away, k_awayTeamId, "Away", 2000)
            );

            AppException exception = Assert.Throws<AppException>(() => state.LoadDetails(details));

            StringAssert.Contains("expected (Home/5)", exception.Message);
        }

        [Test]
        public void CanMoveSelectedPlayer_OwnPlayerDuringPlayerMove_IsTrue()
        {
            const int userId = 901;
            ClientGameState state = CreateLoadedGameState(userId);
            ServerUser user = CreateUser(userId);
            GameHudVM vm = CreateHudVm(state, user, TeamIndex.Home, 1);

            try
            {
                Assert.IsTrue(vm.CanMoveSelectedPlayer);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void CanMoveSelectedPlayer_CaptainBotAndClaimedTeammate_UsesServerRules()
        {
            const int userId = 901;
            ClientGameState state = CreateLoadedGameState(userId, otherUserId: 902);
            ServerUser user = CreateUser(userId);
            GameHudVM vm = CreateHudVm(state, user, TeamIndex.Home, 2);

            try
            {
                Assert.IsTrue(vm.CanMoveSelectedPlayer, "Captain should control an unclaimed teammate bot.");

                vm.SwitchPlayer(CreatePlayerVm(state, TeamIndex.Home, 3));

                Assert.IsFalse(vm.CanMoveSelectedPlayer, "Captain must not control another user's player.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [TestCase(TurnStageEnum.Processing)]
        [TestCase(TurnStageEnum.ShowResults)]
        [TestCase(TurnStageEnum.EndPlayerMove)]
        public void CanMoveSelectedPlayer_OutsidePlayerMoveStage_IsFalse(TurnStageEnum stage)
        {
            const int userId = 901;
            ClientGameState state = CreateLoadedGameState(userId);
            state.UpdateTurn(new TurnStateChangedMessage(FoobbuteoGameStatus.Ongoing, stage, 7, 42f));
            GameHudVM vm = CreateHudVm(state, CreateUser(userId), TeamIndex.Home, 1);

            try
            {
                Assert.IsFalse(vm.CanMoveSelectedPlayer);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void CanClaimPlayer_TeamCaptainSelectsOpponent_IsFalse()
        {
            ClientGameState state = CreateLoadedGameState(0);
            ServerUser user = CreateUnassignedUser(k_homeTeamId);
            GameHudVM vm = CreateHudVm(state, user, TeamIndex.Away, 2);

            try
            {
                Assert.IsFalse(vm.CanClaimPlayer);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void CanClaimPlayer_TeamCaptainSelectsOwnTeam_IsTrue()
        {
            ClientGameState state = CreateLoadedGameState(0);
            ServerUser user = CreateUnassignedUser(k_homeTeamId);
            GameHudVM vm = CreateHudVm(state, user, TeamIndex.Home, 2);

            try
            {
                Assert.IsTrue(vm.CanClaimPlayer);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        [Test]
        public void UpdateTimer_EndPlayerMove_DisplaysEndPlayerMoveText()
        {
            GameHudVM vm = ScriptableObject.CreateInstance<GameHudVM>();

            try
            {
                vm.UpdateTimer(GameHudVM.TimerType.EndPlayerMove, 0);

                Assert.AreEqual("End player move", vm.TimerText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vm);
            }
        }

        private static BaseGameInfo CreateBaseGameInfo()
        {
            var fixedInfo = new FixedGameInfoNetState(
                k_gameId,
                FoobbuteoGameType.PvP,
                30,
                new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero),
                1000
            );
            var turnState = new TurnStageNetState(FoobbuteoGameStatus.Ongoing, 7, TurnStageEnum.PlayerMove, 12f, 42f);
            var homeTeam = new NetFoobbuteoTeam
            {
                Id = k_homeTeamId,
                Name = "Home",
                Color = ShirtColorEnum.Blue,
            };
            var awayTeam = new NetFoobbuteoTeam
            {
                Id = k_awayTeamId,
                Name = "Away",
                Color = ShirtColorEnum.Red,
            };
            return new BaseGameInfo(fixedInfo, turnState, homeTeam, awayTeam);
        }

        private static GameDetailsContract CreateGameDetails(int gameId = k_gameId)
        {
            return new GameDetailsContract(
                gameId,
                new Vector2(305f, 210f),
                CreateTeamDetails(TeamIndex.Home, k_homeTeamId, "Home", 1000),
                CreateTeamDetails(TeamIndex.Away, k_awayTeamId, "Away", 2000)
            );
        }

        private static ClientGameState CreateLoadedGameState(int captainUserId, int otherUserId = 0)
        {
            ClientGameState state = ClientGameState.FromContract(CreateBaseGameInfo());
            GameDetailsContract details = CreateGameDetails();
            details.HomeTeam.Players[0].UserId = captainUserId;
            details.HomeTeam.Players[2].UserId = otherUserId;
            state.LoadDetails(details);
            return state;
        }

        private static ServerUser CreateUser(int userId) =>
            new ServerUser(
                clientId: 1001,
                userId: userId,
                playerId: 1001,
                name: "Captain",
                role: UserRoles.Player,
                inGameRoomId: k_gameId,
                gameSlot: new GameSlot(k_gameId, TeamIndex.Home, 1)
            );

        private static ServerUser CreateUnassignedUser(int userId) =>
            new ServerUser(
                clientId: 1001,
                userId: userId,
                playerId: 1001,
                name: "Captain",
                role: UserRoles.Player,
                inGameRoomId: k_gameId
            );

        private static GameHudVM CreateHudVm(ClientGameState state, GameUser user, TeamIndex teamIdx, byte playerIdx)
        {
            GameHudVM vm = ScriptableObject.CreateInstance<GameHudVM>();
            vm.GameState = state;
            vm.User = user;
            vm.SwitchPlayer(CreatePlayerVm(state, teamIdx, playerIdx));
            return vm;
        }

        private static PlayerVM CreatePlayerVm(ClientGameState state, TeamIndex teamIdx, byte playerIdx)
        {
            var team = state.Team(teamIdx);
            var teamVm = new TeamVM(teamIdx, team.Id, team.Name, null, null, team.Color);
            return new PlayerVM(playerIdx, teamVm, state.GetPlayer(teamIdx, playerIdx));
        }

        private static GameDetails_TeamContract CreateTeamDetails(
            TeamIndex teamIdx,
            int teamId,
            string teamName,
            int playerIdBase
        )
        {
            var players = new GameDetails_PlayerContract[11];
            for (byte playerIdx = 1; playerIdx <= players.Length; playerIdx++)
            {
                players[playerIdx - 1] = new GameDetails_PlayerContract
                {
                    TeamIdx = teamIdx,
                    PlayerIdx = playerIdx,
                    PlayerId = playerIdBase + playerIdx,
                    Name = $"{teamName} {playerIdx}",
                    Position = new Vector2(playerIdx * 10f, playerIdx * 5f),
                    IsCaptain = playerIdx == 1,
                    PlayerMode = playerIdx == 1 ? PlayerModeEnum.GkSave : PlayerModeEnum.Interception,
                    ActionPoints = 100f,
                };
            }

            return new GameDetails_TeamContract(teamId, players, 0);
        }
    }
}
