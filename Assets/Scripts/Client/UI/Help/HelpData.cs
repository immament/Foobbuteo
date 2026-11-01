// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine.InputSystem.Utilities;

namespace Imma.Foobbuteo.Client.UI.Help
{
    public static class HelpData
    {
        internal static ReadOnlyArray<ReadOnlyArray<HelpItemData>> CreateScreens(ClientImageCatalog images)
        {
            return new ReadOnlyArray<ReadOnlyArray<HelpItemData>>(
                new[]
                {
                    new ReadOnlyArray<HelpItemData>(
                        new HelpItemData[]
                        {
                            new()
                            {
                                image = images.MoveHelpImage,
                                title = "Run / Run with the Ball",
                                description =
                                    "To move your player you'll need to select the 'Run' button and then double click in your destination inside the available radius. The distance that you can run with your player, in one turn, is limited by your skills (using the action points). If you have the ball in your possession and you decide to run, you'll Run with the Ball. When you 'Run with the Ball' you'll have to win the confrontation with all the opponents that are <b>close to you at your final destination</b> in order to keep the ball in your possession. 'Run with the Ball' uses more action points (therefore you can run a shorter distance).",
                            },
                            new()
                            {
                                image = images.PassHelpImage,
                                title = "Low Pass",
                                description =
                                    "If you have the ball in your possession you can make a low pass. To do that click in the 'Low Pass' button and then double click in the pass destination (a zone in the field and not a teammate). Low passes <b>can be intercept by opponent players</b>. To decide who gains the ball possession, a confrontation with all players in the destination zone (squares marked on the field) will be made (a maximum of 3 per team: the best 3 available). The amount of action points that you spend making a Low Pass depends on your skills.",
                            },
                            new()
                            {
                                image = images.LongPassHelpImage,
                                title = "High Pass / Cross",
                                description =
                                    "If you have the ball in your possession you can make a high pass/cross. To do that click in the 'High Pass / Cross' button and then double click in the pass destination (a zone in the field and not a player). High passes <b>cannot be intercept by opponent players</b> but they give you a penalty in confrontation at the ball destination. Crosses are not subject to this penalty. Crosses can be only made in the attacking corner zones. As said, once a high pass / cross is made, there must be a confrontation at the zone (squares marked on the field) where the ball lands. To decide who gains the ball possession, a confrontation with all players in the destination zone (squares marked on the field) will be made (a maximum of 3 per team). The amount of action points that you spend making a High Pass / Cross depends on your skills. It's obvious that having more players in the final zone will help to win the confrontation.",
                            },
                            new()
                            {
                                image = images.ShotHelpImage,
                                title = "Shot",
                                description =
                                    "If you have the ball in your possession you can make a Shot. To do that click in the 'Shoot' button and then double click in front of the net (not inside), an arrow will appear inside the net marking the shot direction. Shots can only be intercepted by goalkeepers. The amount of action points that you spend making a Shot depends on your skills. Your Shot skill also affects the direction of the shots.",
                            },
                        }
                    ),
                    new ReadOnlyArray<HelpItemData>(
                        new HelpItemData[]
                        {
                            new()
                            {
                                image = images.ConfrontationHelpImage,
                                title = "Confrontation",
                                description =
                                    "With this mode selected your player will <b>use 100%</b> of the confrontation skill, but <b>won't be able to intercept passes</b>.",
                            },
                            new()
                            {
                                image = images.InterceptionHelpImage,
                                title = "Intercept Passes",
                                description =
                                    "With this mode selected your player will be able to <b>intercept short passes</b> but his <b>confrontation skill will be reduced by 50%</b>.",
                            },
                            new()
                            {
                                image = images.GoalkeeperSaveHelpImage,
                                title = "Saves (Goalkeeper)",
                                description =
                                    "\"Saves\": With this mode selected, goalkeepers have a bonus in saves, when made inside the area.",
                            },
                            new()
                            {
                                image = images.GoalkeeperOutOfAreaHelpImage,
                                title = "Out of Area (GK)",
                                description =
                                    "With this mode selected, Goalkeepers will use their Out of Area skill to complete saves. When the player is out of the goalkeeper box with this mode selected, a boost on this skill will be awarded, therefore increasing the possibilities of a save.",
                            },
                            new()
                            {
                                image = images.GoalkeeperFieldPlayerHelpImage,
                                title = "Field Player",
                                description =
                                    "In Field Player mode, the goalkeeper uses the Goalkeeper Passing skill for both low and high passes. A higher rating increases passing range. Goalkeeper interception is disabled in this mode.",
                            },
                        }
                    ),
                }
            );
        }
    }
}
