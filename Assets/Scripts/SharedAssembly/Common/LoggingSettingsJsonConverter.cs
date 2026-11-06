// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imma.Common
{
    public sealed class LoggingSettingsJsonConverter : JsonConverter<LoggingSettings>
    {
        public override LoggingSettings ReadJson(
            JsonReader reader,
            Type objectType,
            LoggingSettings existingValue,
            bool hasExistingValue,
            JsonSerializer serializer
        )
        {
            var token = JToken.Load(reader);
            return token.Type == JTokenType.Null ? null : LoggingSettings.Parse(token);
        }

        public override void WriteJson(JsonWriter writer, LoggingSettings value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            var data = new JObject();
            if (value.LogLevel.HasValue)
                data["logLevel"] = value.LogLevel.Value.ToString();
            if (value.DefaultSubLogLevel.HasValue)
                data["defaultSubLogLevel"] = value.DefaultSubLogLevel.Value.ToString();
            if (value.LogDirectory != null)
                data["logDirectory"] = value.LogDirectory;
            var categories = new JObject();
            foreach (var pair in value.Categories)
                categories[pair.Key] = pair.Value.ToString();
            data["categories"] = categories;
            data.WriteTo(writer);
        }
    }
}
