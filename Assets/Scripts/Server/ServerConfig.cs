// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using System.IO;
using Imma.Common;
using Imma.Foobbuteo.Server.Repos;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Users;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class ServerConfig
{
    internal const string DataProviderEnvironmentVariable = "FOOBBUTEO_DATA_PROVIDER";
    internal const string SqlConnectionStringEnvironmentVariable = "FOOBBUTEO_SQL_CONNECTION_STRING";

    private static ServerConfig m_config;
    internal static ServerConfig Config => m_config ??= Load();
    public float PlayerMoveTime { get; set; } = 20.0f;
    public int InactiveAfterTurns { get; set; } = 3;

    internal int GetInactiveAfterTurns() =>
        InactiveAfterTurns > 0
            ? InactiveAfterTurns
            : throw new InvalidOperationException("InactiveAfterTurns must be positive.");

    public int UserListRefreshIntervalSeconds { get; set; } =
        ClientRuntimeSettings.DefaultUserListRefreshIntervalSeconds;

    internal ClientRuntimeSettings GetClientRuntimeSettings() => new(UserListRefreshIntervalSeconds);

    public JObject Logging { get; set; }
    public ImmLogger.LogLevel? LogLevel { get; set; }
    public ImmLogger.LogLevel? DefaultSubLogLevel { get; set; }
    public string LogDirectory { get; set; } = "Logs";
    public DataAccessConfig DataAccess { get; set; } = new();
    public ConnectionStrings ConnectionStrings { get; set; } = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEditorConfig()
    {
        m_config = null;
    }
#elif UNITY_SERVER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeServerConfig()
    {
        _ = Config;
    }
#endif

    private static ServerConfig Load()
    {
        var configPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "appsettings.json");
        var loadedConfig = File.Exists(configPath)
            ? JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(configPath)) ?? new ServerConfig()
            : new ServerConfig();

        loadedConfig.DataAccess ??= new DataAccessConfig();
        loadedConfig.ConnectionStrings ??= new ConnectionStrings();
        _ = loadedConfig.GetClientRuntimeSettings();
        _ = loadedConfig.GetInactiveAfterTurns();
        var logging = LoggingSettings.Parse(loadedConfig.Logging);
        m_config = loadedConfig;

        ImmLogger.Configure(logging);
#if UNITY_SERVER && !UNITY_EDITOR
        if (m_config.LogLevel.HasValue)
        {
            ImmLogger.Level = m_config.LogLevel.Value;
        }

        if (m_config.DefaultSubLogLevel.HasValue)
        {
            ImmLogger.DefaultSubLogLevel = m_config.DefaultSubLogLevel.Value;
        }
#endif

        return m_config;
    }

    internal DataProvider GetDataProvider()
    {
        string environmentValue = Environment.GetEnvironmentVariable(DataProviderEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(environmentValue))
            return DataAccess.Provider;
        if (Enum.TryParse(environmentValue, true, out DataProvider provider))
            return provider;
        throw new InvalidOperationException(
            $"Environment variable {DataProviderEnvironmentVariable} has unsupported value '{environmentValue}'."
        );
    }

    internal string GetSqlConnectionString()
    {
        string environmentValue = Environment.GetEnvironmentVariable(SqlConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;
        if (!string.IsNullOrWhiteSpace(ConnectionStrings.Default))
            return ConnectionStrings.Default;
        throw new InvalidOperationException(
            $"SQL Server is selected, but {SqlConnectionStringEnvironmentVariable} is not configured."
        );
    }
}

public sealed class ConnectionStrings
{
    public string Default { get; set; }
}

public enum DataProvider
{
    Mock,
    SqlServer,
}

public sealed class DataAccessConfig
{
    public DataProvider Provider { get; set; } = DataProvider.Mock;
    public bool LoadInitialGames { get; set; } = false;
    public int SqlCommandTimeoutSeconds { get; set; } = 30;
    public List<MockUserConfig> MockUsers { get; set; } = new();
    public List<MockTeamRoster> MockTeamRosters { get; set; } = new();
}
