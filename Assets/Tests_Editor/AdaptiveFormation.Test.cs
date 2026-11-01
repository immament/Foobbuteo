// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Server.Game.Model;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Model;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor
{
    [Category("BotAI")]
    public class AdaptiveFormationTests
    {
        [Test]
        public void BoundsCenter_IncludesMinimumOffset()
        {
            var bounds = new Bounds2d(new Vector2(100f, 50f), new Vector2(300f, 150f));
            Assert.AreEqual(new Vector2(200f, 100f), bounds.Center);
        }

        [Test]
        public void BoundsDistanceRange_ReturnsContainedPointAtLegalDistance()
        {
            var bounds = new Bounds2d(new Vector2(100f, 50f), new Vector2(300f, 150f));
            var origin = new Vector2(20f, 100f);

            Assert.IsTrue(bounds.TryGetPointInDistanceRange(origin, 90f, 120f, out Vector2 point));
            Assert.IsTrue(bounds.Contains(point));
            Assert.That(Vector2.Distance(origin, point), Is.InRange(90f, 120f));
        }

        [Test]
        public void ClampToOutfield_MovesGoalkeeperAreaPointToPlayablePitch()
        {
            Vector2 target = PitchUtils.GkAreaHome.Center.ClampToOutfield();

            Assert.IsTrue(target.IsOnPitch());
            Assert.IsFalse(target.InAnyGkArea());
            Assert.AreEqual(PitchUtils.GkAreaHome.Max.x + 1f, target.x, 0.001f);
        }

        [TestCase(TeamIndex.Home, 12, 13, true)]
        [TestCase(TeamIndex.Home, 12, 11, false)]
        [TestCase(TeamIndex.Away, 12, 11, true)]
        [TestCase(TeamIndex.Away, 12, 13, false)]
        public void ForwardZoneColumn_IsMirroredByTeam(TeamIndex team, int fromZone, int toZone, bool expected)
        {
            Assert.AreEqual(expected, PitchUtils.IsForwardOrSameZoneColumn(team, fromZone, toZone));
        }

        [Test]
        public void ZoneCenters_MapBackToTheirZone()
        {
            for (int zone = 1; zone <= PitchUtils.ZonesCount; zone++)
                Assert.AreEqual(zone, PitchUtils.GetZoneCenter(zone).GetZone());
        }

        [Test]
        public void GoalkeeperAreas_HaveDistinctSpecialZones()
        {
            Assert.AreEqual(PitchUtils.GkAreaZoneHome, PitchUtils.GkAreaHome.Center.GetZone());
            Assert.AreEqual(PitchUtils.GkAreaZoneAway, PitchUtils.GkAreaAway.Center.GetZone());
            Assert.IsTrue(PitchUtils.IsGoalkeeperAreaZone(PitchUtils.GkAreaZoneHome));
            Assert.IsTrue(PitchUtils.IsGoalkeeperAreaZone(PitchUtils.GkAreaZoneAway));
            Assert.IsFalse(PitchUtils.IsRegularZone(PitchUtils.GkAreaZoneHome));
            Assert.IsFalse(PitchUtils.IsRegularZone(PitchUtils.GkAreaZoneAway));
        }

        [Test]
        public void PositionOutsidePitch_DoesNotUseHomeGoalkeeperZone()
        {
            Assert.AreEqual(PitchUtils.InvalidZone, new Vector2(-1f, 210f).GetZone());
            Assert.AreNotEqual(PitchUtils.GkAreaZoneHome, new Vector2(-1f, 210f).GetZone());
        }

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void NeutralPositioning_PlacesPlayersBehindAndAheadOfBall(TeamIndex side)
        {
            var team = CreateTeam(side);
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);
            bool hasPlayerBehindBall = false;
            bool hasPlayerAheadOfBall = false;
            for (byte i = 2; i <= 11; i++)
            {
                float attackDepth =
                    (team.GetAdaptiveFormationPosition(i).x - PitchUtils.PitchBounds.Center.x)
                    * (side == TeamIndex.Home ? 1f : -1f);
                hasPlayerBehindBall |= attackDepth < 0f;
                hasPlayerAheadOfBall |= attackDepth > 0f;
            }

            Assert.IsTrue(hasPlayerBehindBall);
            Assert.IsTrue(hasPlayerAheadOfBall);
            AssertPosition(team.GetPlayerAtIdx(1).FormationPosition, team.GetAdaptiveFormationPosition(1));
        }

        [Test]
        public void BallOffset_TranslatesShapeWithoutChangingBasePositionsOrAccumulatingDrift()
        {
            var team = CreateTeam(TeamIndex.Home);
            var ball = PitchUtils.PitchBounds.Center + new Vector2(40f, 20f);
            var basePosition = team.GetPlayerAtIdx(2).FormationPosition;
            var initialAdaptivePosition = team.GetAdaptiveFormationPosition(2);
            team.UpdatePositioningBounds(ball);
            team.UpdatePositioningBounds(ball);

            AssertPosition(initialAdaptivePosition + new Vector2(30f, 10f), team.GetAdaptiveFormationPosition(2));
            AssertPosition(basePosition, team.GetPlayerAtIdx(2).FormationPosition);
            AssertPosition(basePosition, team.GetPlayerAtIdx(2).Position);

            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);
            AssertPosition(initialAdaptivePosition, team.GetAdaptiveFormationPosition(2));
        }

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void PossessionMode_ShiftsFormationAlongAttackingDirection(TeamIndex side)
        {
            var team = CreateTeam(side);
            var ball = PitchUtils.PitchBounds.Center;
            float attackDirection = side == TeamIndex.Home ? 1f : -1f;

            team.UpdatePositioningBounds(ball, TeamPositioningMode.Defensive);
            float defensiveCenter = team.FormationBounds.Center.x * attackDirection;
            team.UpdatePositioningBounds(ball, TeamPositioningMode.Neutral);
            float neutralCenter = team.FormationBounds.Center.x * attackDirection;
            team.UpdatePositioningBounds(ball, TeamPositioningMode.Offensive);
            float offensiveCenter = team.FormationBounds.Center.x * attackDirection;

            Assert.Greater(offensiveCenter, neutralCenter);
            Assert.Greater(neutralCenter, defensiveCenter);
        }

        [TestCase(TeamIndex.Home)]
        [TestCase(TeamIndex.Away)]
        public void BallAnchor_ConfiguresFormationDepthBehindBall(TeamIndex side)
        {
            var team = CreateTeam(side);
            team.OffensiveBallAnchor = 0.25f;
            team.BallAnchorInfluence = 1f;
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center, TeamPositioningMode.Offensive);

            float ballDepthInFormation =
                side == TeamIndex.Home
                    ? PitchUtils.PitchBounds.Center.x - team.FormationBounds.Min.x
                    : team.FormationBounds.Max.x - PitchUtils.PitchBounds.Center.x;
            Assert.AreEqual(team.FormationBounds.Size.x * 0.25f, ballDepthInFormation, 0.001f);
        }

        [Test]
        public void SameBall_ProducesDistinctTeamBoundsContainingAdaptiveTargets()
        {
            var home = CreateTeam(TeamIndex.Home);
            var away = CreateTeam(TeamIndex.Away);
            var ball = new Vector2(113f, 210f);

            home.UpdatePositioningBounds(ball);
            away.UpdatePositioningBounds(ball);

            Assert.AreNotEqual(home.FormationBounds.Center.x, away.FormationBounds.Center.x);
            for (byte i = 2; i <= 11; i++)
            {
                Assert.IsTrue(home.FormationBounds.Contains(home.GetAdaptiveFormationPosition(i)));
                Assert.IsTrue(away.FormationBounds.Contains(away.GetAdaptiveFormationPosition(i)));
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(610f, 420f)]
        [TestCase(0f, 420f)]
        [TestCase(610f, 0f)]
        public void BallAtPitchCorner_KeepsEveryOutfieldTargetLegal(float x, float y)
        {
            foreach (var side in new[] { TeamIndex.Home, TeamIndex.Away })
            {
                var team = CreateTeam(side);
                team.UpdatePositioningBounds(new Vector2(x, y));
                for (byte i = 2; i <= 11; i++)
                {
                    var target = team.GetAdaptiveFormationPosition(i);
                    Assert.IsTrue(PitchUtils.IsOnPitch(target));
                    Assert.IsFalse(PitchUtils.InAnyGkArea(target));
                    Assert.IsTrue(team.FormationBounds.Contains(target));
                }
            }
        }

        [Test]
        public void MirroredBallAndTeams_ProduceMirroredTargets()
        {
            var home = CreateTeam(TeamIndex.Home);
            var away = CreateTeam(TeamIndex.Away);
            var ball = new Vector2(100f, 70f);
            home.UpdatePositioningBounds(ball);
            away.UpdatePositioningBounds(PitchUtils.PitchBounds.Max - ball);
            for (byte i = 1; i <= 11; i++)
                AssertPosition(
                    PitchUtils.PitchBounds.Max - home.GetAdaptiveFormationPosition(i),
                    away.GetAdaptiveFormationPosition(i)
                );
        }

        [Test]
        public void FormationScale_ChangesDepthAndWidthButLeavesGoalkeeperAlone()
        {
            var team = CreateTeam(TeamIndex.Home);
            var originalSize = team.FormationBounds.Size;
            var goalkeeperPosition = team.GetPlayerAtIdx(1).FormationPosition;
            team.FormationScale = new Vector2(0.5f, 0.75f);
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);

            AssertPosition(Vector2.Scale(originalSize, team.FormationScale), team.FormationBounds.Size);
            AssertPosition(
                new Vector2(team.FormationBounds.Min.x, team.FormationBounds.Max.y),
                team.GetAdaptiveFormationPosition(2)
            );
            AssertPosition(goalkeeperPosition, team.GetAdaptiveFormationPosition(1));
        }

        [Test]
        public void OversizedFormation_IsLimitedToPlayableBounds()
        {
            var team = CreateTeam(TeamIndex.Home);
            team.FormationScale = new Vector2(100f, 100f);
            team.UpdatePositioningBounds(new Vector2(610f, 420f));
            for (byte i = 2; i <= 11; i++)
            {
                var target = team.GetAdaptiveFormationPosition(i);
                Assert.IsTrue(PitchUtils.IsOnPitch(target));
                Assert.IsFalse(PitchUtils.InAnyGkArea(target));
            }
        }

        [Test]
        public void DynamicFormation_AssignsPlayersToNearestFormationSlots()
        {
            var team = CreateTeam(TeamIndex.Home);
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);
            Vector2 secondPlayerTarget = team.GetAdaptiveFormationPosition(2);
            Vector2 thirdPlayerTarget = team.GetAdaptiveFormationPosition(3);
            var players = new ServerPlayerState[11];
            for (byte playerIdx = 1; playerIdx <= 11; playerIdx++)
            {
                Vector2 position =
                    playerIdx == 2 ? thirdPlayerTarget
                    : playerIdx == 3 ? secondPlayerTarget
                    : team.GetAdaptiveFormationPosition(playerIdx);
                players[playerIdx - 1] = ServerPlayerState.CreateForTest(
                    1,
                    TeamIndex.Home,
                    playerIdx,
                    playerIdx,
                    $"Player {playerIdx}",
                    position
                );
            }
            team = new ServerTeamState(TeamIndex.Home, 1, "Team", default, players) { FormationScale = Vector2.one };
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);

            var assignments = team.GetDynamicFormationPositions();

            Assert.AreEqual(3, assignments[2].FormationPlayerIdx);
            Assert.AreEqual(2, assignments[3].FormationPlayerIdx);
            AssertPosition(thirdPlayerTarget, assignments[2].Target);
            AssertPosition(secondPlayerTarget, assignments[3].Target);
        }

        [Test]
        public void BallCarrierAnchor_PlacesAssignedFormationSlotAtPlayerPosition()
        {
            var team = CreateTeam(TeamIndex.Home);
            var playerPosition = new Vector2(230f, 320f);

            team.UpdatePositioningBounds(playerPosition, TeamPositioningMode.Offensive, anchoredFormationPlayerIdx: 2);

            AssertPosition(playerPosition, team.GetAdaptiveFormationPosition(2));
            Assert.Greater(team.GetAdaptiveFormationPosition(9).x, playerPosition.x);
        }

        [Test]
        public void StartingPositions_ScaleBaseFormationAroundItsCenter()
        {
            var team = CreateTeam(TeamIndex.Home);
            team.FormationScale = new Vector2(0.5f, 0.75f);
            var min = team.GetPlayerAtIdx(2).FormationPosition;
            var max = min;
            for (byte playerIdx = 3; playerIdx <= 11; playerIdx++)
            {
                min = Vector2.Min(min, team.GetPlayerAtIdx(playerIdx).FormationPosition);
                max = Vector2.Max(max, team.GetPlayerAtIdx(playerIdx).FormationPosition);
            }
            Vector2 baseCenter = new Bounds2d(min, max).Center;

            team.UpdatePositioningBounds(
                new Vector2(500f, 50f),
                TeamPositioningMode.Offensive,
                useStartingPositions: true
            );

            Vector2 basePosition = team.GetPlayerAtIdx(9).FormationPosition;
            AssertPosition(
                baseCenter + Vector2.Scale(basePosition - baseCenter, team.FormationScale),
                team.GetAdaptiveFormationPosition(9)
            );
        }

        private static ServerTeamState CreateTeam(TeamIndex side)
        {
            var players = new ServerPlayerState[11];
            for (byte i = 1; i <= 11; i++)
                players[i - 1] = ServerPlayerState.CreateForTest(1, side, i);
            var team = new ServerTeamState(side, 1, "Team", default, players);
            team.FormationScale = Vector2.one;
            team.UpdatePositioningBounds(PitchUtils.PitchBounds.Center);
            return team;
        }

        private static void AssertPosition(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
        }
    }
}
