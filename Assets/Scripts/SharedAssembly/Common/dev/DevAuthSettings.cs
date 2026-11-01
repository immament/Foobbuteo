#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json;

namespace Imma.Foobbuteo.Common.dev
{
    public static class DevAuthSettings
    {
        public static DevAuthConfig Config
        {
            get
            {
                try
                {
                    return EditorSettings.Config["devAuth"].ToObject<DevAuthConfig>();
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException(
                        "Invalid development authentication profiles in editor.settings.json."
                    );
                }
            }
        }

        public static DevAuthUser FindByToken(string token) =>
            Config.users.FirstOrDefault(user => !string.IsNullOrEmpty(user.token) && user.token == token);

        public static string GetProfileToken(string profile)
        {
            var user = Config.users.FirstOrDefault(user => user.profile == profile);
            if (user == null || string.IsNullOrWhiteSpace(user.token))
                throw new InvalidOperationException(
                    $"Development authentication profile '{profile}' is not configured."
                );
            return user.token;
        }
    }
}
#endif
