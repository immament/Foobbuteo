// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Server.Game.Persistence;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class SqlGamePersistenceRepository : IGamePersistenceRepository
    {
        internal const string k_SavePlayerCommandProcedure = "foobbuteo_api.save_player_command";
        internal const string k_SavePlayerModeProcedure = "foobbuteo_api.save_player_mode";
        internal const string k_SavePlayerClaimProcedure = "foobbuteo_api.save_player_claim";
        internal const string k_SaveTurnProcedure = "foobbuteo_api.save_turn";
        internal const string k_SaveTeamInfoProcedure = "foobbuteo_api.save_team_info";
        internal const string k_TurnPlayersType = "foobbuteo_api.turn_player_state";
        internal const string k_TurnStatisticsType = "foobbuteo_api.turn_statistic";

        private readonly string m_connectionString;
        private readonly int m_commandTimeoutSeconds;

        public SqlGamePersistenceRepository(string connectionString, int commandTimeoutSeconds)
        {
            m_connectionString = SqlAuthenticationRepository.RequireConnectionString(connectionString);
            m_commandTimeoutSeconds = SqlAuthenticationRepository.RequirePositiveTimeout(commandTimeoutSeconds);
        }

        public Task SavePlayerCommandAsync(int gameId, PlayerCommand command, bool hadBall)
        {
            this.LDebug(() =>
                $"++ #{gameId}, player: {command.PlayerIdx}, team: {command.TeamIdx}, turn: {command.Turn}, action: {command.Action}, hadBall: {hadBall}"
            );
            return ExecuteAsync(
                k_SavePlayerCommandProcedure,
                sql =>
                {
                    sql.Parameters.Add("@GameId", SqlDbType.Int).Value = gameId;
                    sql.Parameters.Add("@Turn", SqlDbType.Int).Value = command.Turn;
                    sql.Parameters.Add("@PlayerId", SqlDbType.Int).Value = command.PlayerId;
                    sql.Parameters.Add("@ActionType", SqlDbType.TinyInt).Value = (byte)command.Action;
                    sql.Parameters.Add("@TargetX", SqlDbType.Int).Value = ToDatabaseCoordinate(
                        command.TargetPosition.x
                    );
                    sql.Parameters.Add("@TargetY", SqlDbType.Int).Value = ToDatabaseCoordinate(
                        command.TargetPosition.y
                    );
                    sql.Parameters.Add("@HadBall", SqlDbType.Bit).Value = hadBall;
                    // sql.Parameters.Add("@Quadrant", SqlDbType.Int).Value = 0;
                }
            );
        }

        public Task SavePlayerModeAsync(int gameId, PlayerModeCommand command)
        {
            this.LDebug(() =>
                $"++ #{gameId}, player: {command.PlayerIdx}, team: {command.TeamIdx}, turn: {command.Turn}, mode: {command.Mode},"
            );
            return ExecuteAsync(
                k_SavePlayerModeProcedure,
                sql =>
                {
                    sql.Parameters.Add("@GameId", SqlDbType.Int).Value = gameId;
                    sql.Parameters.Add("@Turn", SqlDbType.Int).Value = command.Turn;
                    sql.Parameters.Add("@PlayerId", SqlDbType.Int).Value = command.PlayerId;
                    sql.Parameters.Add("@PlayerMode", SqlDbType.TinyInt).Value = (byte)command.Mode;
                }
            );
        }

        public async Task<GameDetails_PlayerContract?> SavePlayerClaimAsync(
            int gameId,
            PlayerKey playerKey,
            ServerUser user,
            bool isCaptain
        )
        {
            this.LDebug(() =>
                $"++ #{gameId}, playerKey: {playerKey}, userId: @{user.UserId}, playerID, {user.PlayerId}, clientId: ^{user.ClientId}, isCaptain: {isCaptain}"
            );
            try
            {
                using var connection = new SqlConnection(m_connectionString);
                using SqlCommand command = CreateCommand(connection, k_SavePlayerClaimProcedure);
                command.Parameters.Add("@GameId", SqlDbType.Int).Value = gameId;
                command.Parameters.Add("@TeamIndex", SqlDbType.TinyInt).Value = (byte)playerKey.TeamIdx;
                command.Parameters.Add("@PlayerIndex", SqlDbType.TinyInt).Value = playerKey.PlayerIdx;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
                command.Parameters.Add("@PlayerId", SqlDbType.Int).Value = user.PlayerId;
                command.Parameters.Add("@IsCaptain", SqlDbType.Bit).Value = isCaptain;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new RepositoryUnavailableException(
                        $"The claimed player was not returned by the database. {new { gameId, playerKey, user.UserId, user.PlayerId, isCaptain }}"
                    );
                return SqlGamesRepository.ReadPlayerContract(reader);
            }
            catch (SqlException ex)
            {
                throw WrapSqlException(k_SavePlayerClaimProcedure, ex);
            }
        }

        public Task SaveTurnAsync(TurnPersistenceRequest request)
        {
            return ExecuteAsync(
                k_SaveTurnProcedure,
                sql =>
                {
                    sql.Parameters.Add("@GameId", SqlDbType.Int).Value = request.GameId;
                    sql.Parameters.Add("@Turn", SqlDbType.Int).Value = request.Turn;
                    sql.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)request.Status;
                    sql.Parameters.Add("@BallX", SqlDbType.Int).Value = ToDatabaseCoordinate(request.BallPosition.x);
                    sql.Parameters.Add("@BallY", SqlDbType.Int).Value = ToDatabaseCoordinate(request.BallPosition.y);
                    sql.Parameters.Add("@ExpectedHomeScore", SqlDbType.Int).Value = request.HomeScore;
                    sql.Parameters.Add("@ExpectedAwayScore", SqlDbType.Int).Value = request.AwayScore;

                    PersistedGameEvent? gameEvent = request.GameEvent;
                    sql.Parameters.Add("@EventType", SqlDbType.TinyInt).Value = gameEvent.HasValue
                        ? gameEvent.Value.EventType
                        : DBNull.Value;
                    sql.Parameters.Add("@EventPlayerId", SqlDbType.Int).Value = gameEvent.HasValue
                        ? gameEvent.Value.PlayerId
                        : DBNull.Value;
                    sql.Parameters.Add("@EventTeamIndex", SqlDbType.TinyInt).Value = gameEvent.HasValue
                        ? (byte)gameEvent.Value.TeamIndex
                        : DBNull.Value;

                    AddStructuredParameter(sql, "@Players", k_TurnPlayersType, CreatePlayersTable(request));
                    AddStructuredParameter(sql, "@Statistics", k_TurnStatisticsType, CreateStatisticsTable(request));
                }
            );
        }

        public Task SaveTeamInfoAsync(
            int gameId,
            TeamIndex teamIndex,
            int captainUserId,
            int captainPlayerId,
            string teamName
        ) =>
            ExecuteAsync(
                k_SaveTeamInfoProcedure,
                sql => ConfigureTeamInfoCommand(sql, gameId, teamIndex, captainUserId, captainPlayerId, teamName)
            );

        internal static void ConfigureTeamInfoCommand(
            DbCommand command,
            int gameId,
            TeamIndex teamIndex,
            int captainUserId,
            int captainPlayerId,
            string teamName
        )
        {
            AddParameter("@GameId", DbType.Int32, gameId);
            AddParameter("@TeamIndex", DbType.Byte, (byte)teamIndex);
            AddParameter("@CaptainUserId", DbType.Int32, captainUserId);
            AddParameter("@CaptainPlayerId", DbType.Int32, captainPlayerId);
            AddParameter("@TeamName", DbType.String, teamName);

            void AddParameter(string name, DbType type, object value)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.DbType = type;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
        }

        public void Dispose() { }

        private async Task ExecuteAsync(string procedureName, Action<SqlCommand> configure)
        {
            try
            {
                using var connection = new SqlConnection(m_connectionString);
                using SqlCommand command = CreateCommand(connection, procedureName);
                configure(command);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                throw WrapSqlException(procedureName, ex);
            }
        }

        private SqlCommand CreateCommand(SqlConnection connection, string procedureName) =>
            new(procedureName, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = m_commandTimeoutSeconds,
            };

        private RepositoryUnavailableException WrapSqlException(string procedureName, SqlException ex)
        {
            this.LWarn($"Games database is unavailable while executing {procedureName}. {ex.Message}");
            this.LException(ex);
            return new RepositoryUnavailableException("Games database is unavailable.", ex);
        }

        private static DataTable CreatePlayersTable(TurnPersistenceRequest request)
        {
            var table = new DataTable();
            table.Columns.Add("team_index", typeof(byte));
            table.Columns.Add("player_index", typeof(byte));
            table.Columns.Add("player_id", typeof(int));
            table.Columns.Add("position_x", typeof(int));
            table.Columns.Add("position_y", typeof(int));
            table.Columns.Add("has_ball", typeof(bool));
            table.Columns.Add("player_mode", typeof(byte));
            foreach (PersistedPlayerState player in request.Players)
            {
                table.Rows.Add(
                    (byte)player.TeamIndex,
                    player.PlayerIndex,
                    player.PlayerId,
                    ToDatabaseCoordinate(player.Position.x),
                    ToDatabaseCoordinate(player.Position.y),
                    player.HasBall,
                    (byte)player.PlayerMode
                );
            }
            return table;
        }

        private static DataTable CreateStatisticsTable(TurnPersistenceRequest request)
        {
            var table = new DataTable();
            table.Columns.Add("player_id", typeof(int));
            table.Columns.Add("statistic_type", typeof(byte));
            foreach (PersistedStatistic statistic in request.Statistics)
                table.Rows.Add(statistic.PlayerId, statistic.StatisticType);
            return table;
        }

        private static void AddStructuredParameter(SqlCommand command, string name, string typeName, DataTable value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Structured);
            parameter.TypeName = typeName;
            parameter.Value = value;
        }

        private static int ToDatabaseCoordinate(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
