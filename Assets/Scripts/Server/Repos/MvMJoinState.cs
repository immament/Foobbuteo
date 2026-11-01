// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Imma.Foobbuteo.Server.Game.Model;

namespace Imma.Foobbuteo.Server.Repos
{
    internal sealed class MvMJoinState
    {
        public ServerTeamState Team { get; }
        public DateTimeOffset StartDateTime { get; }

        public MvMJoinState(ServerTeamState team, DateTimeOffset startDateTime)
        {
            Team = team;
            StartDateTime = startDateTime;
        }
    }
}
