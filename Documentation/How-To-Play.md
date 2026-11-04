# How to play Foobbuteo

[Back to the README](../README.md#how-to-play)

This guide describes the actions and player modes explained by the in-game help.
Movement range and action-point costs depend on your player's skills.

## Basic controls

1. Select a player you control.
2. Choose an action using its button: **Run**, **Low Pass**, **High Pass / Cross**, or **Shoot**.
3. Double-click the destination on the pitch. For movement, stay inside the available radius.

Passes target a **zone on the pitch**, rather than a specific teammate. For shots, target a point
in front of the goal rather than inside it.

## Actions

<h3>
  <img src="../Assets/ClientAssets/Buttons/Actions/opw_mover.png" width="40" height="40" align="middle" alt="">
  Run / Run with the Ball
</h3>

Select **Run**, then double-click a destination inside the available radius. Your skills and
available action points limit the distance you can cover in one turn.

When your player has possession, this action becomes **Run with the Ball**:

- It costs more action points than running without the ball, reducing your movement range.
- To keep possession, your player must win a confrontation against opponents close to the
  **final destination**.

<h3>
  <img src="../Assets/ClientAssets/Buttons/Actions/opw_passe.png" width="40" height="40" align="middle" alt="">
  Low Pass
</h3>

With possession, select **Low Pass**, then double-click the destination zone.

- Opponents can intercept low passes.
- Possession at the destination is decided by a confrontation between players in that zone,
  shown by the squares on the pitch.
- At most three players per team participate: the best three available.
- The action-point cost depends on your player's skills.

<h3>
  <img src="../Assets/ClientAssets/Buttons/Actions/opw_passelongo.png" width="40" height="40" align="middle" alt="">
  High Pass / Cross
</h3>

With possession, select **High Pass / Cross**, then double-click the destination zone.

- High passes cannot be intercepted in flight, but apply a penalty in the confrontation where
  the ball lands.
- Crosses are available only from the attacking corner zones and do **not** apply this penalty.
- Possession is decided by a confrontation in the landing zone, with at most three players
  per team participating.
- Having more teammates in the destination zone helps contest possession, within that limit.
- The action-point cost depends on your player's skills.

<h3>
  <img src="../Assets/ClientAssets/Buttons/Actions/opw_remate.png" width="40" height="40" align="middle" alt="">
  Shot
</h3>

With possession, select **Shoot**, then double-click **in front of the goal, not inside it**.
An arrow inside the goal shows the shot direction.

- Only goalkeepers can intercept shots.
- The action-point cost depends on your player's skills.
- Your player's Shot skill also affects shot direction.

## Player modes

### Confrontation and interception

Choose the mode that suits your player's defensive task:

| Mode | Effect |
| --- | --- |
| <img src="../Assets/ClientAssets/Buttons/Modes/opw_rb.png" width="40" height="40" align="middle" alt=""> **Confrontation** | Uses 100% of the player's confrontation skill, but disables pass interception. |
| <img src="../Assets/ClientAssets/Buttons/Modes/opw_inter.png" width="40" height="40" align="middle" alt=""> **Intercept Passes** | Allows interception of low passes, but reduces confrontation skill by 50%. |

### Goalkeeper modes

| Mode | Effect |
| --- | --- |
| <img src="../Assets/ClientAssets/Buttons/Modes/opw_defesa.png" width="40" height="40" align="middle" alt=""> **Saves (Goalkeeper)** | Gives a save bonus when the goalkeeper makes a save inside the area. |
| <img src="../Assets/ClientAssets/Buttons/Modes/opw_saida.png" width="40" height="40" align="middle" alt=""> **Out of Area (GK)** | Uses the Out of Area skill for saves, with a bonus to that skill when outside the goalkeeper box. |
| <img src="../Assets/ClientAssets/Buttons/Modes/opw_jogador.png" width="40" height="40" align="middle" alt=""> **Field Player** | Uses Goalkeeper Passing for both low and high passes; a higher rating increases passing range. Goalkeeper interception is disabled. |

## In-game help source

These instructions are adapted from
[`HelpData.cs`](../Assets/Scripts/Client/UI/Help/HelpData.cs), which supplies the in-game help text.
