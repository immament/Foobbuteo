// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed partial class SqlGamesRepository
    {
        public async Task<GameDetails_PlayerContract[]> LoadMvMSquadAsync(int gameId, TeamIndex team)
        {
            try
            {
                using var connection = new SqlConnection(m_connectionString);
                using var command = CreateStoredProcedure(connection, "foobbuteo_api.list_mvm_squad");
                command.Parameters.Add("@game_id", SqlDbType.Int).Value = gameId;
                command.Parameters.Add("@team_index", SqlDbType.TinyInt).Value = (byte)team;
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                var players = new List<GameDetails_PlayerContract>();
                while (await reader.ReadAsync())
                    players.Add(ReadPlayerContract(reader));
                return players.ToArray();
            }
            catch (SqlException exception)
            {
                throw new RepositoryUnavailableException("The match squad could not be loaded.", exception);
            }
        }

        public async Task SaveMvMLineupAsync(int gameId, TeamIndex team, int managerId, int[] expected, int[] desired)
        {
            try
            {
                using var connection = new SqlConnection(m_connectionString);
                using var command = CreateStoredProcedure(connection, "foobbuteo_api.save_mvm_lineup");
                command.Parameters.Add("@game_id", SqlDbType.Int).Value = gameId;
                command.Parameters.Add("@team_index", SqlDbType.TinyInt).Value = (byte)team;
                command.Parameters.Add("@manager_user_id", SqlDbType.Int).Value = managerId;
                AddLineupParameter(command, "@expected", expected);
                AddLineupParameter(command, "@desired", desired);
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new RepositoryUnavailableException("The database did not return a lineup result.");
                if (!reader.GetBoolean(reader.GetOrdinal("success")))
                    throw new AppException(reader.GetString(reader.GetOrdinal("error")));
            }
            catch (SqlException exception)
            {
                throw new RepositoryUnavailableException("The match lineup could not be saved.", exception);
            }
        }

        private static void AddLineupParameter(SqlCommand command, string name, int[] lineup)
        {
            var rows = new DataTable();
            rows.Columns.Add("player_index", typeof(byte));
            rows.Columns.Add("player_id", typeof(int));
            for (int index = 0; index < lineup.Length; index++)
                rows.Rows.Add((byte)(index + 1), lineup[index]);
            var parameter = command.Parameters.Add(name, SqlDbType.Structured);
            parameter.TypeName = "foobbuteo_api.mvm_lineup";
            parameter.Value = rows;
        }
    }
}
