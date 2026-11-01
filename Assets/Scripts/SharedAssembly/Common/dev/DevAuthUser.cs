#if UNITY_EDITOR
using System;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Model;

namespace Imma.Foobbuteo.Common.dev
{
    [Serializable]
    public sealed class DevAuthUser
    {
        public string profile;
        public string token;
        public int userId;
        public int playerId;
        public string name;
        public UserRoles role = UserRoles.Player;
        public ManagedTeam[] managedTeams = Array.Empty<ManagedTeam>();
    }
}
#endif
