// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Imma.Common;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class SqlAuthenticationRepository : IAuthenticationRepository
    {
        internal const string ConsumeLoginTokenProcedure = "foobbuteo_api.consume_login_token";

        private readonly string m_connectionString;
        private readonly int m_commandTimeoutSeconds;

        public SqlAuthenticationRepository(string connectionString, int commandTimeoutSeconds)
        {
            ImmLogger.AddType(typeof(SqlAuthenticationRepository), "Auth");
            m_connectionString = RequireConnectionString(connectionString);
            m_commandTimeoutSeconds = RequirePositiveTimeout(commandTimeoutSeconds);
        }

        public async Task<ServerUser> ValidateAndConsumeTokenAsync(ulong clientId, string token)
        {
            this.LDebug(() => $"++ Sql ValidateAndConsumeTokenAsync ^{clientId} ++");
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                using var connection = new SqlConnection(m_connectionString);
                using var command = CreateStoredProcedure(connection, ConsumeLoginTokenProcedure);
                command.Parameters.Add("@Token", SqlDbType.NVarChar, 256).Value = token;

                await connection.OpenAsync();
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                int userId = reader.GetInt32("user_id");
                var user = new ServerUser(
                    clientId,
                    userId,
                    reader.GetInt32("player_id"),
                    reader.GetString("player_name"),
                    (UserRoles)reader.GetInt32("user_role"),
                    0
                );
                var teams = new List<ManagedTeam>();
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                        teams.Add(ReadManagedTeam(reader));
                }
                user.RefreshTeams(teams.ToArray());
                return user;
            }
            catch (SqlException ex)
            {
                this.LError($"Authentication database is unavailable. {ex.Message}");
                throw new RepositoryUnavailableException("Authentication database is unavailable.", ex);
            }
        }

        public void Dispose() { }

        internal static ManagedTeam ReadManagedTeam(DbDataReader reader) =>
            new(
                new TeamKey(
                    (SourceGame)Convert.ToInt32(reader["source_game_id"]),
                    (TeamType)Convert.ToInt32(reader["team_type"]),
                    Convert.ToInt32(reader["team_id"]),
                    (AgeCategory)Convert.ToInt32(reader["age_category"])
                ),
                Convert.ToString(reader["team_name"])
            );

        private SqlCommand CreateStoredProcedure(SqlConnection connection, string procedureName) =>
            new(procedureName, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = m_commandTimeoutSeconds,
            };

        internal static string RequireConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("The SQL Server connection string is not configured.");
            return connectionString;
        }

        internal static int RequirePositiveTimeout(int commandTimeoutSeconds)
        {
            if (commandTimeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
            return commandTimeoutSeconds;
        }
    }
}
