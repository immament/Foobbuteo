// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    public class TurnPersistenceRequestTests
    {
        [Test]
        public void Create_CapturesAllPlayersAndMarksFinalTurnEnded()
        {
            ServerGameState game = CreateGame(turn: 30, turns: 30);
            var commands = new List<PlayerCommand>();
            var processor = new TurnProcessor(game, commands);

            processor.ProcessTurn();
            TurnPersistenceRequest request = TurnPersistenceRequest.Create(game, processor);

            Assert.AreEqual(22, request.Players.Count);
            Assert.AreEqual(FoobbuteoGameStatus.Ended, request.Status);
            Assert.AreEqual(game.BallPosition, request.BallPosition);
            Assert.AreEqual(game.HomeTeam.Score, request.HomeScore);
            Assert.AreEqual(game.AwayTeam.Score, request.AwayScore);
        }

        [Test]
        public async Task MockRepository_AcceptsAllPersistenceOperations()
        {
            var repository = new MockGamePersistenceRepository();
            ServerGameState game = CreateGame(turn: 1, turns: 30);
            var processor = new TurnProcessor(game, new List<PlayerCommand>());
            processor.ProcessTurn();

            await repository.SavePlayerCommandAsync(
                game.GameId,
                new PlayerCommand(1, TeamIndex.Home, 2, 2, PlayerActionEnum.Move, Vector2.one),
                false
            );
            await repository.SavePlayerModeAsync(
                game.GameId,
                new PlayerModeCommand(1, TeamIndex.Home, 2, 2, PlayerModeEnum.Confrontation)
            );
            await repository.SaveTurnAsync(TurnPersistenceRequest.Create(game, processor));

            Assert.Pass();
        }

        private static ServerGameState CreateGame(int turn, int turns)
        {
            var home = new ServerTeamState(TeamIndex.Home, 1, "Home", default, CreatePlayers(TeamIndex.Home));
            var away = new ServerTeamState(TeamIndex.Away, 2, "Away", default, CreatePlayers(TeamIndex.Away));
            var game = ServerGameState.CreateForTest(
                77,
                DateTime.UtcNow,
                home,
                away,
                FoobbuteoGameStatus.Ongoing,
                turns,
                turn
            );
            game.MoveBall(PitchUtils.PitchBounds.Center);
            game.ProcessingStarted(0f);
            return game;
        }

        private static ServerPlayerState[] CreatePlayers(TeamIndex teamIndex)
        {
            var players = new ServerPlayerState[11];
            for (byte playerIndex = 1; playerIndex <= 11; playerIndex++)
                players[playerIndex - 1] = ServerPlayerState.CreateForTest(77, teamIndex, playerIndex);
            return players;
        }
    }
}
