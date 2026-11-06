# MvM: source-aware team and database contracts

MvM assigns one user to each team without a pitch-player claim. `GameSlot`
index 0 represents a manager; pitch indices remain 1–11. A team is identified
by `(source_game_id, team_type, team_id, age_category)`; its name is not identity.

Only [Footstar](https://www.footstar.org) (`source_game_id = 1`) is implemented. Value 2 is reserved for a
later source integration. That integration will retrieve teams through HTTP;
source identity in persisted matches does not require SQL-based team retrieval.
Its API client, authentication, roster retrieval, and snapshot persistence belong
to a separate task.

Team types: Club=0, NT=1, CT=2. Main age category=0; clubs additionally support
19, and NT/CT support 21 and 18. Club labels include the team ID, while NT/CT
labels include their type. Main labels omit the age category.

C# uses the `AgeCategory` enum (`Main=0`, `U18=18`, `U19=19`, `U21=21`);
SQL parameters and columns retain these integer values.

## Required procedure updates (not implemented here)

### `foobbuteo_api.consume_login_token`

Preserve the existing first result containing the authenticated user. Append a
second result with these non-null columns, one row per eligible team variant:

| Column | SQL type |
| --- | --- |
| `source_game_id` | int |
| `team_type` | int |
| `team_id` | int |
| `age_category` | int |
| `team_name` | nvarchar |

C# reads this with `NextResultAsync()`. Zero rows or an absent second result
means no eligible teams; existing authentication remains usable. Preserve
single-use token validation/consumption. Return only teams the user can manage.

### `foobbuteo_api.create_game`

Keep the existing PvP parameters and scalar game ID result. For MvM (`@game_type=0`)
accept four additional int parameters: `@source_game_id`, `@team_type`, `@team_id`,
and `@age_category`. Existing `@creator_player_id` and `@creator_position` must be
optional and are omitted for MvM. New team parameters must be optional for PvP.

Validate current [Footstar](https://www.footstar.org) management permission and absence of another open-game
participation. Atomically persist the home manager, complete team key, and a
snapshot of the actual starting eleven with their skills. No pitch player is
assigned to the manager. The away side starts unassigned.

### `foobbuteo_api.join_mvm_game` (new)

Inputs: `@game_id`, `@user_id`, `@source_game_id`, `@team_type`, `@team_id`, and
`@age_category`, all int. Return one row with `success` (bit) and `error` (nvarchar,
empty on success).

Atomically require a pending MvM game, a free away side, a different manager,
current management permission, a different complete team key, and no other
open-game participation. Persist the away manager, team key, and actual starting
eleven snapshot. Concurrent joins must produce only one winner. Repeating the
same successful assignment must be idempotent so a caller can recover when
the subsequent reload fails. The C# repository reloads the authoritative roster.

### `foobbuteo_api.list_games`

Keep existing fields, including `game_type`, `home_user_id`, and `away_user_id`.
For MvM these user IDs identify managers, not external teams or claimed players.
The unassigned away user ID is 0. Team names are raw source names; C# formats them.

Add nullable columns for each `home`/`away` prefix:
`{prefix}_source_game_id`, `{prefix}_team_type`, `{prefix}_team_id`, and
`{prefix}_age_category`. Use the complete key for assigned MvM sides. PvP and
unassigned sides have no external team key. The loader selects the team mapping
from `game_type`: PvP never reads source-team fields; assigned MvM sides require
all four fields to be present and non-null. Unassigned MvM sides skip those fields.

Include pending MvM games waiting for an opponent even after their scheduled
time falls outside `@MinutesToDisplay`; preserve the `@MinGameId` filter.

### `foobbuteo_api.list_game_players`

Keep existing player columns and return the Foobbuteo skill columns below. An assigned MvM side returns exactly
11 distinct positive player IDs with indices 1–11. `user_id=0`: player identity
and manager identity remain separate. An unassigned away side may return no
rows; C# creates presentation placeholders that cannot enter gameplay.

Command and turn-persistence procedure signatures remain unchanged. Existing
player-claim/captain procedures are not used for MvM. Apply the same claim
restriction to any independent database caller.

### Foobbuteo player skills

`list_game_players`, `save_player_claim`, and `list_mvm_squad` return ten required,
non-null `int` ratings on the 0–20 scale:

| SQL column | Player contract field |
| --- | --- |
| `move_skill` | `MoveSkill` |
| `confrontation_skill` | `ConfrontationSkill` |
| `low_pass_skill` | `LowPassSkill` |
| `long_pass_skill` | `LongPassSkill` |
| `gk_pass_skill` | `GkPassSkill` |
| `crosses_skill` | `CrossesSkill` |
| `shooting_skill` | `ShootingSkill` |
| `long_shots_skill` | `LongShotsSkill` |
| `gk_saves_skill` | `GkSavesSkill` |
| `out_of_area_skill` | `OutOfAreaSkill` |

SQL currently returns temporary values: 15 for slots with `user_id > 0`, 5 for
other slots, and 5 for every squad row. No database table migration is required
for this result-shape change. Unity stores and transmits these ratings directly;
it neither reads nor converts [Footstar](https://www.footstar.org) skills. Mock roster snapshots use the same
ten contract fields; old skill names must be migrated in local configuration.

Gameplay uses `GkPassSkill` for both passes only when the goalkeeper is in
`GkFieldPlayer` mode. Otherwise low passes use `LowPassSkill`, and high passes use
`CrossesSkill` in the attacking cross zones or `LongPassSkill` elsewhere. Player
tables and tooltips always show goalkeeper passing for goalkeepers and the stored
low/long passing ratings for outfield players, independently of position.
Shot cost and resolution both use `ShootingSkill` through distance 130 and
`LongShotsSkill` beyond it, retaining the existing penalty-area exponents.

The SQL result shape and network skill payload are incompatible with older
versions. Deploy the three updated procedures and matching server/client builds
together. This skill change does not require clearing matches or running the
destructive structure/upgrade scripts described for the earlier MvM rollout.

## Mock verification and rollout

Configure `DataAccess.MockUsers[].Teams` with `ManagedTeam` values containing
`Key` and `Name`. Configure `DataAccess.MockTeamRosters` with `Team` (the complete
key) and `Players` (11 `GameDetails_PlayerContract` snapshots, including identities,
positions, skills, and action points). The mock loads these snapshots, never
substituting a generated roster for an assigned team. Missing/invalid rosters
reject creation or joining. Configure side-appropriate coordinates for manual
testing; the mock remaps side and pitch indices and clears claim/control flags.

Deploy the procedures before enabling SQL-backed MvM. Deploy matching client and
server versions because network contracts changed. Managers retain assignments
until end/deletion, including after leaving pending rooms or disconnecting; they
may participate in only one open game. Disconnection does not pause the match:
the existing automatic planner handles players without manager commands.

Manual acceptance: use two managers with different teams plus a spectator;
create, wait past the scheduled start, join, command several players, reject
opponent/spectator commands and claims, reconnect, and confirm cleanup at end.


## Pending lineup preparation

The match stores an immutable full squad when each manager adds their team. The
starting eleven remains in `game_players`; `mvm_squad_players` stores all eligible
players separately, including identity, country, level, and skills. Current SQL
uses the existing placeholder skill values consistently for starters and reserves.
Source transfers/call-ups after assignment do not change this match snapshot.

`list_mvm_squad(@game_id int, @team_index tinyint)` returns player columns compatible
with `list_game_players`, with `player_index=0` for unassigned squad snapshots.
Only the assigned manager in the pending room may request this data through RPC.

`save_mvm_lineup(@game_id int, @team_index tinyint, @manager_user_id int,
@expected foobbuteo_api.mvm_lineup READONLY, @desired foobbuteo_api.mvm_lineup READONLY)`
returns `success` and `error`. The TVP columns are `player_index tinyint` and
`player_id int`, in that order. Require eleven distinct snapshot IDs in slots 1–11,
manager ownership, pending MvM status, and an unchanged expected lineup. A desired
lineup already saved succeeds idempotently. Save all active rows in one transaction;
identity and skills follow the player, while formation position and mode follow
the destination slot. Never modify the source team's lineup.

The client keeps a local draft shared by the UI pitch and both player tables.
Apply submits original/desired ID arrays; Cancel discards all local changes.
Reserve-to-reserve ordering is local only. Successful saves broadcast only active
player details to the room. Client and server must deploy together.

`join_mvm_game` additionally requires `@preparation_minutes int`, supplied from
`MvMPreparationRules.MinimumMvMPreparationMinutes` (default 3). On the first
successful opponent assignment, atomically set start time to the later of its
existing value and the database's current UTC time plus this duration. Rejected
joins and retries of an existing assignment never extend it. Return/load the saved
start time with the joined team; refresh replicated room countdowns and lobby data.
The engine serializes kickoff, joining, and lineup saves through its persistence gate.

Mock roster `Players` still contains exactly eleven snapshots; optional `Reserves`
contains additional eligible players. The mock captures both at assignment time.

Release preparation: clear all existing matches, apply the updated structure
(including squad table and TVP) before `foobbuteo_api_v2.sql`, then deploy matching
server/client builds. No old-data migration or backfill is supplied. SQL scripts
are maintained in the sibling `Foobbuteo2D-internal/Sql` repository; do not execute
the full structure script against an existing production database without the
normal schema deployment process.
