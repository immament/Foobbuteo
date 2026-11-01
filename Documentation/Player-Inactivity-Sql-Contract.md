# Player inactivity and team persistence

Activity is runtime-only. After repository loading, the engine initializes each assigned user's
`LastActivityTurn` to the game's current completed turn and clears inactivity, before `NextTurn()`
increments it. Connection state comes from the live registry. No activity columns, additional
command/mode parameters, or turn-player table type changes are required.

Accepted commands and mode changes register only the sender's activity after persistence succeeds.
AI and reconnecting do not register activity. Input closure evaluates inactivity; the default
threshold is three consecutive missed turns.

At input closure before AI processing, select a captain when the current captain is inactive or
the team's captain user ID has no matching pitch player. An active captain remains assigned.
Player claims also select a captain when the captain is missing or an inactive captain's slot
is claimed. Select the lowest-slot connected, active user, including the newly active claimant.
Without an eligible replacement, retain the team ID, name, and any existing captain. Claims of
other slots do not replace an existing captain. The first claimant on a team with no assigned
captain user ID becomes captain.

## Future stored procedure

`SaveTeamInfoAsync` calls `foobbuteo_api.save_team_info` with:

| Parameter | SQL type | Meaning |
| --- | --- | --- |
| `@GameId` | int | Game being updated |
| `@TeamIndex` | tinyint | Home or away team |
| `@CaptainUserId` | int | Selected captain's user identity |
| `@CaptainPlayerId` | int | Database player identity, not slot number |

The procedure is maintained outside this repository and must be implemented before SQL-backed
captain replacement. In one transaction, validate the captain's game/team assignment, update the
team captain ID and name from the selected user's identity, clear other captain flags, and set the
selected player's captain flag. Repeated identical calls must be idempotent. Loading procedures
must return the updated team identity/name and captain flags.

The engine saves changed captain assignments after a successful claim or completed-turn save,
under its persistence gate. Failed team saves retry without repeating successful claims, turn
saves, or already successful team saves. Player/team updates follow successful persistence.
These are separate database operations, not one transaction. A server interruption between them
can leave the database captain temporarily unchanged.

Existing command, mode, claim, completed-turn, and pending-command contracts remain unchanged.
SQL integration must be verified externally after the new procedure is implemented.
