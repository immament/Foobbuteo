// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
#if UNITY_ANDROID
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace Imma.Foobbuteo.Client
{
    internal static class ClientEnvironment
    {
        public const string Production = "prod";
        public const string Development = "dev";
        public const string OverrideFileName = "client-environment.json";

        public static void Validate(string environment)
        {
            if (environment != Production && environment != Development)
                throw new InvalidOperationException("Client environment must be 'prod' or 'dev'.");
        }

#if UNITY_ANDROID
        public static ClientEnvironmentConfig LoadAndroidOverride(string directory)
        {
            string path = Path.Combine(directory, OverrideFileName);
            string json;
            try
            {
                // File.Exists also returns false for some inaccessible paths; only absence means prod.
                json = File.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                return new ClientEnvironmentConfig(Production);
            }
            catch (DirectoryNotFoundException)
            {
                return new ClientEnvironmentConfig(Production);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Cannot read client environment override at '{path}'.", ex);
            }

            try
            {
                JObject data = JObject.Parse(json);
                foreach (var property in data.Properties())
                    if (property.Name != "environment" && property.Name != "uiLayoutDiagnostics")
                        throw new InvalidOperationException(
                            $"Unknown client environment override field '{property.Name}'."
                        );
                if (data["environment"]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Client environment override requires string 'environment'.");
                string environment = (string)data["environment"];
                Validate(environment);
                if (!ClientBootstrapConfig.TryParse(json, out var config, out var error))
                    throw new InvalidOperationException(error);
                return new ClientEnvironmentConfig(environment, config.UiLayoutDiagnostics ?? false);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Invalid JSON in client environment override.", ex);
            }
        }
#endif
    }
}
