// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Imma.Foobbuteo.Shared.Lobby;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed partial class SqlGamesRepository : IGamesRepository
    {
        internal const string k_CreateGameProcedure = "foobbuteo_api.create_game";
        internal const string k_ListGamesProcedure = "foobbuteo_api.list_games";
        internal const string k_ListGamePlayersProcedure = "foobbuteo_api.list_game_players";
        internal const string k_ListPendingCommandsProcedure = "foobbuteo_api.list_pending_commands";
        internal const string k_JoinMvmGameProcedure = "foobbuteo_api.join_mvm_game";

        private readonly string m_connectionString;
        private readonly int m_commandTimeoutSeconds;

        public SqlGamesRepository(string connectionString, int commandTimeoutSeconds)
        {
            m_connectionString = SqlAuthenticationRepository.RequireConnectionString(connectionString);
            m_commandTimeoutSeconds = SqlAuthenticationRepository.RequirePositiveTimeout(commandTimeoutSeconds);
        }

        public async Task<int> CreateGameAsync(NewGameOptions options, GameUser creator)
        {
            if (creator == null)
                throw new ArgumentNullException(nameof(creator));

            try
            {
                using var conn = new SqlConnection(m_connectionString);
                using var cmd = CreateGameCommand(options, creator);
                cmd.Connection = conn;

                await conn.OpenAsync();
                object result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value || Convert.ToInt32(result) <= 0)
                    throw new RepositoryUnavailableException("The database did not return a valid game id.");

                return Convert.ToInt32(result);
            }
            catch (SqlException ex)
            {
                this.LExceptionTree(ex, "Create game problem");
                throw new RepositoryUnavailableException("Games database is unavailable.", ex);
            }
        }

        internal DbCommand CreateGameCommand(NewGameOptions options, GameUser creator)
        {
            if (creator == null)
                throw new ArgumentNullException(nameof(creator));
            if (!NewGameOptionsRules.TryValidate(options, out string error))
                throw new ArgumentException(error, nameof(options));
            if (options.GameType == FoobbuteoGameType.MvM && !creator.TryGetManagedTeam(options.Team, out _))
                throw new ArgumentException("The creator does not manage the selected team.", nameof(creator));
            if (options.GameType == FoobbuteoGameType.PvP && creator.PlayerId <= 0)
                throw new ArgumentException("A valid creator player is required.", nameof(creator));

            var cmd = CreateStoredProcedure(null, k_CreateGameProcedure);

            cmd.Parameters.Add("@start_in_minutes", SqlDbType.Int).Value = options.StartInMinutes;
            cmd.Parameters.Add("@turn_count", SqlDbType.Int).Value = options.NumberOfTurns;
            cmd.Parameters.Add("@home_color", SqlDbType.TinyInt).Value = (byte)options.HomeTeamColor;
            cmd.Parameters.Add("@away_color", SqlDbType.TinyInt).Value = (byte)options.AwayTeamColor;
            cmd.Parameters.Add("@creator_user_id", SqlDbType.Int).Value = creator.UserId;
            if (options.GameType == FoobbuteoGameType.PvP)
            {
                cmd.Parameters.Add("@creator_player_id", SqlDbType.Int).Value = creator.PlayerId;
                cmd.Parameters.Add("@creator_position", SqlDbType.Int).Value = options.PlayerPosition;
            }
            else
                AddTeamParameters(cmd, options.Team);
            cmd.Parameters.Add("@game_type", SqlDbType.Int).Value = (int)options.GameType;
            return cmd;
        }

        private static void AddTeamParameters(SqlCommand cmd, TeamKey team)
        {
            cmd.Parameters.Add("@source_game_id", SqlDbType.Int).Value = (int)team.SourceGame;
            cmd.Parameters.Add("@team_type", SqlDbType.Int).Value = (int)team.TeamType;
            cmd.Parameters.Add("@team_id", SqlDbType.Int).Value = team.TeamId;
            cmd.Parameters.Add("@age_category", SqlDbType.Int).Value = (int)team.AgeCategory;
        }

        public async Task<MvMJoinState> JoinMvMGameAsync(int gameId, TeamKey team, GameUser manager)
        {
            if (manager == null || !manager.TryGetManagedTeam(team, out _))
                throw new ArgumentException("An eligible manager and team are required.", nameof(manager));
            try
            {
                using var conn = new SqlConnection(m_connectionString);
                using var cmd = CreateStoredProcedure(conn, k_JoinMvmGameProcedure);
                cmd.Parameters.Add("@game_id", SqlDbType.Int).Value = gameId;
                cmd.Parameters.Add("@user_id", SqlDbType.Int).Value = manager.UserId;
                cmd.Parameters.Add("@preparation_minutes", SqlDbType.Int).Value = Imma.Foobbuteo
                    .Server
                    .Game
                    .MvMPreparationRules
                    .MinimumMvMPreparationMinutes;
                AddTeamParameters(cmd, team);
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        throw new RepositoryUnavailableException("The database did not return a join result.");
                    if (!reader.GetBoolean("success"))
                        throw new AppException(reader.GetString("error"));
                }
                var games = await GetGameListAsync(240, gameId - 1);
                var joined =
                    games.Find(game => game.GameId == gameId)
                    ?? throw new RepositoryUnavailableException("The joined game could not be loaded.");
                return new MvMJoinState(joined.AwayTeam, joined.StartDateTime);
            }
            catch (SqlException ex)
            {
                throw new RepositoryUnavailableException("Games database is unavailable.", ex);
            }
        }

        public async Task<List<ServerGameState>> GetGameListAsync(int minutesToDisplay = 240, int minId = 0)
        {
            if (minutesToDisplay < 0)
                throw new ArgumentOutOfRangeException(nameof(minutesToDisplay));

            try
            {
                List<ServerGameState> games_raw = await GetGamesAsync(minutesToDisplay, minId);
                List<ServerGameState> games = new(games_raw.Count);
                foreach (ServerGameState game in games_raw)
                {
                    try
                    {
                        await LoadPlayersAsync(game);
                        await LoadPendingCommandsAsync(game);
                        ValidateLoadedGame(game);
                        games.Add(game);
                    }
                    catch (Exception ex)
                    {
                        this.LExceptionTree(ex);
                        this.LWarn($"Game skiped: #{game.GameId}. Problem during load from repository: {ex.Message}");
                    }
                }
                return games;
            }
            catch (SqlException ex)
            {
                this.LExceptionTree(ex, "Load games problem");
                throw new RepositoryUnavailableException("Games database is unavailable.", ex);
            }
        }

        public void Dispose()
        {
            this.LTrace("---");
        }

        internal static void ValidateLoadedGame(ServerGameState game)
        {
            if (game == null)
                throw new InvalidOperationException("The database returned a null game.");

            ValidateTeamPlayers(game.GameId, game.HomeTeam);
            ValidateTeamPlayers(game.GameId, game.AwayTeam);
            switch (game.MatchType)
            {
                case FoobbuteoGameType.PvP:
                    break;
                case FoobbuteoGameType.MvM:
                    ValidateLoadedMvMGame(game);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported match type: {game.MatchType}");
            }
        }

        private static void ValidateLoadedMvMGame(ServerGameState game)
        {
            ServerTeamState.ValidateManagedRoster(game.GameId, game.HomeTeam);
            if (game.AwayTeam.Id > 0)
                ServerTeamState.ValidateManagedRoster(game.GameId, game.AwayTeam);

            if (!game.HomeTeam.TeamKey.IsSupported || game.HomeTeam.Id <= 0)
                throw new InvalidOperationException($"Game #{game.GameId} has invalid MvM teams.");

            if (game.AwayTeam.Id <= 0)
                return;

            if (
                !game.AwayTeam.TeamKey.IsSupported
                || game.AwayTeam.Id == game.HomeTeam.Id
                || game.AwayTeam.TeamKey == game.HomeTeam.TeamKey
            )
                throw new InvalidOperationException($"Game #{game.GameId} has invalid MvM teams.");
        }

        internal static void ValidateTeamPlayers(int gameId, ServerTeamState team)
        {
            if (team == null)
                throw new InvalidOperationException($"Game #{gameId} has a missing team.");

            if (team.Players == null || team.Players.Count != 11)
                throw new InvalidOperationException($"Game #{gameId} team {team.TeamIdx} must have 11 players.");
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                if (team.GetPlayerAtIdx(playerIdx) == null)
                {
                    throw new InvalidOperationException(
                        $"Game #{gameId} team {team.TeamIdx} is missing player #{playerIdx}."
                    );
                }
            }
        }

        private async Task<List<ServerGameState>> GetGamesAsync(int minutesToDisplay, int minId)
        {
            var games = new List<ServerGameState>();
            using var conn = new SqlConnection(m_connectionString);
            using var cmd = CreateStoredProcedure(conn, k_ListGamesProcedure);
            cmd.Parameters.Add("@MinutesToDisplay", SqlDbType.Int).Value = minutesToDisplay;
            cmd.Parameters.Add("@MinGameId", SqlDbType.Int).Value = minId;

            await conn.OpenAsync();
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                games.Add(LoadGameState(reader));
            return games;
        }

        private async Task LoadPlayersAsync(ServerGameState game)
        {
            using var conn = new SqlConnection(m_connectionString);
            using var cmd = CreateStoredProcedure(conn, k_ListGamePlayersProcedure);
            cmd.Parameters.Add("@GameId", SqlDbType.Int).Value = game.GameId;

            await conn.OpenAsync();
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                GameDetails_PlayerContract contract = ReadPlayerContract(reader);
                if (contract.PlayerIdx < 1 || contract.PlayerIdx > 11)
                {
                    ImmLogger.LWarn<SqlGamesRepository>(
                        $"Incorrect contract playerIdx: {contract.PlayerIdx}. GameId #{game.GameId}, Team: {contract.TeamIdx}"
                    );
                    continue;
                }

                ServerPlayerState player = ServerPlayerState.FromContract(game.GameId, contract);
                player.RefreshState(game.BallPosition);
                game.LoadPlayer(player);
            }
            if (game.MatchType == FoobbuteoGameType.MvM && game.IsWaitingForOpponent)
                game.SetManagedTeam(
                    ServerGameState.CreateUnassignedTeam(game.GameId, TeamIndex.Away, game.AwayTeam.Color)
                );
        }

        internal static GameDetails_PlayerContract ReadPlayerContract(DbDataReader reader) =>
            new()
            {
                TeamIdx = (TeamIndex)reader.GetByte("team_index"),
                PlayerIdx = reader.GetByte("player_index"),
                PlayerId = reader.GetInt32("player_id"),
                Name = reader.GetString("player_name"),
                CountryId = reader.GetInt32("country_id"),
                PlayerLevel = reader.GetInt32("player_level"),
                Position = new Vector2(reader.GetInt32("position_x"), reader.GetInt32("position_y")),
                HasBall = reader.GetBoolean("has_ball"),
                UserId = reader.GetInt32("user_id"),
                PlayerMode = (PlayerModeEnum)reader.GetByte("player_mode"),
                ActionPoints = reader.GetInt32("action_points"),
                MoveSkill = reader.GetInt32("move_skill"),
                ConfrontationSkill = reader.GetInt32("confrontation_skill"),
                LowPassSkill = reader.GetInt32("low_pass_skill"),
                LongPassSkill = reader.GetInt32("long_pass_skill"),
                GkPassSkill = reader.GetInt32("gk_pass_skill"),
                CrossesSkill = reader.GetInt32("crosses_skill"),
                ShootingSkill = reader.GetInt32("shooting_skill"),
                LongShotsSkill = reader.GetInt32("long_shots_skill"),
                GkSavesSkill = reader.GetInt32("gk_saves_skill"),
                OutOfAreaSkill = reader.GetInt32("out_of_area_skill"),
            };

        private async Task LoadPendingCommandsAsync(ServerGameState game)
        {
            using var conn = new SqlConnection(m_connectionString);
            using var cmd = CreateStoredProcedure(conn, k_ListPendingCommandsProcedure);
            cmd.Parameters.Add("@GameId", SqlDbType.Int).Value = game.GameId;
            cmd.Parameters.Add("@Turn", SqlDbType.Int).Value = game.CurrentTurn + 1;

            await conn.OpenAsync();
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                game.PendingCommands.Add(
                    new PlayerCommand(
                        reader.GetInt32("turn_number"),
                        (TeamIndex)reader.GetByte("team_index"),
                        reader.GetByte("player_index"),
                        reader.GetInt32("player_id"),
                        (PlayerActionEnum)reader.GetByte("action_type"),
                        new Vector2(reader.GetInt32("target_x"), reader.GetInt32("target_y"))
                    )
                );
            }
        }

        private SqlCommand CreateStoredProcedure(SqlConnection conn, string procedureName) =>
            new(procedureName, conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = m_commandTimeoutSeconds,
            };

        internal static ServerGameState LoadGameState(DbDataReader reader)
        {
            var game = new ServerGameState(reader.GetInt32("game_id"), (FoobbuteoGameType)reader.GetByte("game_type"));
            game.Load(
                (FoobbuteoGameStatus)reader.GetByte("status_id"),
                reader.GetInt32("current_turn"),
                reader.GetInt32("turn_count"),
                DateTime.SpecifyKind(reader.GetDateTime("start_time_utc"), DateTimeKind.Utc),
                LoadTeam(reader, TeamIndex.Home, game.MatchType),
                LoadTeam(reader, TeamIndex.Away, game.MatchType),
                reader.IsDBNull("ball_x")
                    ? PitchUtils.PitchBounds.Center
                    : new Vector2(reader.GetInt32("ball_x"), reader.GetInt32("ball_y"))
            );
            return game;
        }

        private static ServerTeamState LoadTeam(
            DbDataReader reader,
            TeamIndex teamIndex,
            FoobbuteoGameType matchType
        ) =>
            matchType switch
            {
                FoobbuteoGameType.PvP => LoadPvPTeam(reader, teamIndex),
                FoobbuteoGameType.MvM => LoadMvMTeam(reader, teamIndex),
                _ => throw new InvalidOperationException($"Unsupported match type: {matchType}"),
            };

        private static ServerTeamState LoadPvPTeam(DbDataReader reader, TeamIndex teamIndex)
        {
            string prefix = teamIndex == TeamIndex.Home ? "home" : "away";
            string name = reader.IsDBNull($"{prefix}_name") ? "None" : reader.GetString($"{prefix}_name");
            return LoadTeamState(reader, teamIndex, prefix, name);
        }

        private static ServerTeamState LoadMvMTeam(DbDataReader reader, TeamIndex teamIndex)
        {
            string prefix = teamIndex == TeamIndex.Home ? "home" : "away";
            string name = reader.IsDBNull($"{prefix}_name") ? "None" : reader.GetString($"{prefix}_name");
            if (reader.GetInt32($"{prefix}_user_id") <= 0)
                return LoadTeamState(reader, teamIndex, prefix, name);

            TeamKey teamKey = ReadTeamKey(reader, prefix);
            name = new ManagedTeam(teamKey, name).DisplayName;
            return LoadTeamState(reader, teamIndex, prefix, name, teamKey);
        }

        private static ServerTeamState LoadTeamState(
            DbDataReader reader,
            TeamIndex teamIndex,
            string prefix,
            string name,
            TeamKey teamKey = default
        )
        {
            return new ServerTeamState(
                teamIndex,
                reader.GetInt32($"{prefix}_user_id"),
                name,
                (ShirtColorEnum)reader.GetByte($"{prefix}_color"),
                new ServerPlayerState[11],
                reader.GetByte($"{prefix}_score"),
                teamKey
            );
        }

        private static TeamKey ReadTeamKey(DbDataReader reader, string prefix)
        {
            return new TeamKey(
                (SourceGame)Convert.ToInt32(reader[$"{prefix}_source_game_id"]),
                (TeamType)Convert.ToInt32(reader[$"{prefix}_team_type"]),
                Convert.ToInt32(reader[$"{prefix}_team_id"]),
                (AgeCategory)Convert.ToInt32(reader[$"{prefix}_age_category"])
            );
        }
    }
}
