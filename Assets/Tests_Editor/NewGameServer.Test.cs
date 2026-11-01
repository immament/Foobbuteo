// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Server;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Server.Model;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using NUnit.Framework;

namespace Tests.Editor
{
    public class NewGameServerTests
    {
        private const ulong k_ClientId = 91001;
        private const int k_UserId = 92001;
        private const int k_PlayerId = 93001;

        [TestCase(k_PlayerId, NewGameOptionsRules.MinStartInMinutes - 1, true)]
        [TestCase(k_PlayerId, 16, true)]
        [TestCase(0, 2, true)]
        [TestCase(-1, 2, true)]
        [TestCase(k_PlayerId, 2, false)]
        public void Create_InvalidOptionsOrIdentity_RejectsBeforePersistence(
            int playerId,
            int startInMinutes,
            bool authenticated
        )
        {
            ServiceLocator.Initialize();
            var registry = new UserRegistry();
            var repository = new CountingRepository();
            var server = new FoobbuteoMainServer();
            try
            {
                if (authenticated)
                {
                    var user = new ServerUser(k_ClientId, k_UserId, playerId, "Creator", UserRoles.Player);
                    Assert.IsNull(registry.RegisterUser(user), "The test creator must register successfully.");
                }
                SetField(server, "m_userRegistry", registry);
                SetField(server, "m_serverState", new FoobbuteoServerState(registry));
                SetField(server, "m_gamesRepo", repository);
                var progress = (GameLoadProgress)
                    typeof(FoobbuteoMainServer)
                        .GetField("m_gameLoadProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(server);
                progress.RecordSuccessfulQuery(Array.Empty<int>(), allGamesInitialized: true);
                var options = new NewGameOptions(startInMinutes: startInMinutes);

                Assert.ThrowsAsync<AppException>(
                    () => server.CreateNewGameAsync(k_ClientId, options),
                    $"Invalid options or missing authenticated player identity must be rejected by the server [{playerId}, {startInMinutes}, {authenticated}]"
                );

                Assert.AreEqual(0, repository.CreateCalls, "A rejected request must never reach persistence.");
            }
            finally
            {
                server.Dispose();
                ServiceLocator.ClearAll();
            }
        }

        private static void SetField(FoobbuteoMainServer server, string name, object value) =>
            typeof(FoobbuteoMainServer)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(server, value);

        private sealed class CountingRepository : IGamesRepository
        {
            public Task<Imma.Foobbuteo.Shared.Game.Contract.GameDetails_PlayerContract[]> LoadMvMSquadAsync(
                int gameId,
                TeamIndex team
            ) => throw new System.NotSupportedException();

            public Task SaveMvMLineupAsync(int gameId, TeamIndex team, int managerId, int[] expected, int[] desired) =>
                throw new System.NotSupportedException();

            public int CreateCalls { get; private set; }

            public Task<MvMJoinState> JoinMvMGameAsync(
                int gameId,
                Imma.Foobbuteo.Shared.Game.Model.TeamKey team,
                GameUser manager
            ) => throw new InvalidOperationException("This fixture does not join games.");

            public Task<int> CreateGameAsync(NewGameOptions options, GameUser creator)
            {
                CreateCalls++;
                throw new InvalidOperationException("Rejected requests must never create a game.");
            }

            public Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay = 240, int minId = 0) =>
                Task.FromResult(new List<ServerGameState>());

            public void Dispose() { }
        }
    }
}
