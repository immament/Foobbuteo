// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Runtime.CompilerServices;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    public static class PitchUtils
    {
        public const int InvalidZone = -1;
        public const int GkAreaZoneHome = 0;
        public const int GkAreaZoneAway = 26;
        public const int ZonesCount = 25;
        public const int ZonesCountX = 5;
        public const int ZonesCountY = 5;
        public static readonly Vector2Int ZoneSize = new(122, 84);
        public static readonly Bounds2d PitchBounds = new(Vector2.zero, new(610f, 420f));
        public static readonly Bounds2d PitchBoundsAllowedToMove = new(
            PitchBounds.Min + new Vector2(10f, 10f),
            PitchBounds.Max - new Vector2(10f, 10f)
        );

        public static readonly FloatRange GoalRangeY = new(174f, 246f);

        // left Gk Area x: 0-32,    y: 142-278
        // left Gk Area x: 578-610, y: 142-278
        //           x: 610-32=578, y: 420-278=142
        public static readonly Bounds2d GkAreaHome = new(min: new(PitchBounds.Min.x, 142f), max: new(32f, 278f));
        public static readonly Bounds2d GkAreaAway = new(min: new(578f, 142f), max: new(PitchBounds.Max.x, 278f));
        private static readonly Vector2 m_goalKickOffset = new(8f, 30f);

        // left Panelty Area x: 0-96,    y: 84-336
        // left Panelty Area x: 514-610, y: 84-336
        //                   x: 610-96=514, y: 420-84=336
        public static readonly Bounds2d PenaltyAreaHome = new Bounds2d(PitchBounds.Min.x, 84f, 96f, 336f);
        public static readonly Bounds2d PenaltyAreaAway = new Bounds2d(514f, 84f, PitchBounds.Max.y, 336f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InCrossZone(TeamIndex teamIdx, int zone)
        {
            return (teamIdx == TeamIndex.Home) ? zone is 5 or 25 : zone is 1 or 21;
        }

        public static Vector2 ClampToPitch(this Vector2 position) => PitchBounds.Clamp(position);

        public static Vector2 ClampToOutfield(this Vector2 position, float margin = 1f)
        {
            position = new Vector2(
                Mathf.Clamp(position.x, PitchBounds.Min.x + margin, PitchBounds.Max.x - margin),
                Mathf.Clamp(position.y, PitchBounds.Min.y + margin, PitchBounds.Max.y - margin)
            );
            if (position.InGkAreaHome())
                position.x = GkAreaHome.Max.x + margin;
            else if (position.InGkAreaAway())
                position.x = GkAreaAway.Min.x - margin;
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnPitch(this Vector2 position) => PitchBounds.ContainsExclusive(position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAllowedMove(this Vector2 position) => PitchBoundsAllowedToMove.ContainsExclusive(position);

        public static bool IsOnOwnHalf(TeamIndex teamIdx, Vector2 position) =>
            (teamIdx == TeamIndex.Home) ? IsHomePitchSide(position.x) : IsAwayPitchSide(position.x);

        public static bool IsOnOwnHalf(TeamIndex teamIdx, float x) =>
            (teamIdx == TeamIndex.Home) ? IsHomePitchSide(x) : IsAwayPitchSide(x);

        public static bool InAnyGkArea(this Vector2 p) =>
            GkAreaHome.ContainsExclusive(p) || GkAreaAway.ContainsExclusive(p);

        public static bool InGkAreaHome(this Vector2 p) => GkAreaHome.ContainsExclusive(p);

        public static bool InGkAreaAway(this Vector2 p) => GkAreaAway.ContainsExclusive(p);

        public static bool InPenaltyArea(this Vector2 p, TeamIndex attackingTeam)
        {
            return (attackingTeam == TeamIndex.Home)
                ? PenaltyAreaAway.ContainsExclusive(p)
                : PenaltyAreaHome.ContainsExclusive(p);
        }

        public static bool IsOwnPenaltyAreaZone(TeamIndex attackingTeam, int zone) =>
            attackingTeam == TeamIndex.Home ? zone is 6 or 11 or 16 : zone is 10 or 15 or 20;

        public static bool IsRegularZone(int zone) => zone is >= 1 and <= ZonesCount;

        public static bool IsGoalkeeperAreaZone(int zone) => zone is GkAreaZoneHome or GkAreaZoneAway;

        public static bool IsGoal(this Vector2 p) =>
            GoalRangeY.Contains(p.y) && (p.x <= PitchBounds.Min.x || p.x >= PitchBounds.Max.x);

        public static int GetZone(this Vector2 pos)
        {
            if (!PitchBounds.ContainsExclusive(pos))
                return InvalidZone;
            if (InGkAreaHome(pos))
                return GkAreaZoneHome;
            if (InGkAreaAway(pos))
                return GkAreaZoneAway;
            int result = ((int)pos.y / ZoneSize.y) * ZonesCountX + ((int)pos.x / ZoneSize.x) + 1;
            if (IsRegularZone(result))
                return result;
            return InvalidZone;
        }

        public static Bounds2d GetZoneBounds(int zone)
        {
            if (!IsRegularZone(zone))
                throw new System.ArgumentOutOfRangeException(nameof(zone));

            int index = zone - 1;
            int x = index % ZonesCountX;
            int y = index / ZonesCountX;
            return new Bounds2d(
                new Vector2(x * ZoneSize.x, y * ZoneSize.y),
                new Vector2((x + 1) * ZoneSize.x, (y + 1) * ZoneSize.y)
            );
        }

        public static Vector2 GetZoneCenter(int zone) => GetZoneBounds(zone).Center;

        public static int GetZoneColumn(int zone)
        {
            if (!IsRegularZone(zone))
                throw new System.ArgumentOutOfRangeException(nameof(zone));
            return (zone - 1) % ZonesCountX;
        }

        public static bool IsForwardOrSameZoneColumn(TeamIndex teamIdx, int fromZone, int toZone)
        {
            if (!IsRegularZone(fromZone) || !IsRegularZone(toZone))
                return false;

            int fromColumn = GetZoneColumn(fromZone);
            int toColumn = GetZoneColumn(toZone);
            return teamIdx == TeamIndex.Home ? toColumn >= fromColumn : toColumn <= fromColumn;
        }

        public static float ForwardDirectionX(TeamIndex teamIdx) => teamIdx == TeamIndex.Home ? 1f : -1f;

        public static Vector2 GetBallZoneTarget(Vector2 ballPosition)
        {
            int zone = GetZone(ballPosition);
            return IsRegularZone(zone) ? GetZoneCenter(zone) : ballPosition.ClampToOutfield();
        }

        public static Vector2 GetGoalkeeperGuardPosition(Vector2 formationPosition, Vector2 ballPosition)
        {
            float goalY = Mathf.Lerp(
                GoalRangeY.Min,
                GoalRangeY.Max,
                Mathf.InverseLerp(PitchBounds.Min.y, PitchBounds.Max.y, ballPosition.y)
            );
            return new Vector2(formationPosition.x, goalY);
        }

        public static Vector2 GoalKickPosition(Vector2 ball)
        {
            if (IsHomePitchSide(ball)) // Home Team Goal kick
            {
                if (ball.y < PitchBounds.Center.y)
                    return new(GkAreaHome.Max.x - m_goalKickOffset.x, GkAreaHome.Min.y + m_goalKickOffset.y);

                return new(GkAreaHome.Max.x - m_goalKickOffset.x, GkAreaHome.Max.y - m_goalKickOffset.y);
            }
            // Away Team Goal kick
            if (ball.y < PitchBounds.Center.y)
                return new(GkAreaAway.Min.x + m_goalKickOffset.x, GkAreaAway.Min.y + m_goalKickOffset.y);

            return new(GkAreaAway.Min.x + m_goalKickOffset.x, GkAreaAway.Max.y - m_goalKickOffset.y);
        }

        public static Vector2 OpponentGoalCenter(TeamIndex teamIdx)
        {
            return new Vector2(
                teamIdx == TeamIndex.Home ? PitchBounds.Max.x : PitchBounds.Min.x,
                (GoalRangeY.Min + GoalRangeY.Max) * 0.5f
            );
        }

        private static bool IsHomePitchSide(Vector2 position) => position.x < PitchBounds.Center.x;

        private static bool IsAwayPitchSide(Vector2 position) => position.x > PitchBounds.Center.x;

        private static bool IsHomePitchSide(float x) => x < PitchBounds.Center.x;

        private static bool IsAwayPitchSide(float x) => x > PitchBounds.Center.x;

        /// <summary>
        /// Not used
        /// Returns a list of points that are at a distance less than or equal to 'threshold' from the segment AB.
        /// </summary>
        // private static List<Vector2> GetPointsNearLine(
        //     Vector2 A,
        //     Vector2 B,
        //     List<Vector2> points,
        //     float threshold
        // )
        // {
        //     List<Vector2> nearPoints = new List<Vector2>();
        //     float sqrThreshold = threshold * threshold;

        //     Vector2 AB = B - A;
        //     float abLenSqr = AB.sqrMagnitude;

        //     // If points A and B are almost in the same place, we treat the line segment as a single point
        //     if (abLenSqr < 0.0001f)
        //     {
        //         for (int i = 0; i < points.Count; i++)
        //         {
        //             if ((points[i] - A).sqrMagnitude <= sqrThreshold)
        //             {
        //                 nearPoints.Add(points[i]);
        //             }
        //         }
        //         return nearPoints;
        //     }

        //     for (int i = 0; i < points.Count; i++)
        //     {
        //         Vector2 P = points[i];
        //         Vector2 AP = P - A;

        //         // d - The length of the projection of vector ap onto the line defined by ab (which is the distance from A to the closest point to P on this line)
        //         //   cos(theta) = adjacent / hypotenuse = d / |AP|
        //         //   d = |AP| * cos(theta)
        //         //   Dot(AP, AB) = |AB| * (|AP| * cos(theta)) = |AB| * d
        //         //   d = Dot(AP, AB) / |AB|
        //         // The coefficient t determines what fraction of the entire segment AB the distance d represents
        //         //   t = d / |AB| = (Dot(AP, AB) / |AB|) / |AB| = Dot(AP, AB) / (|AB|*|AB|) =  Dot(AP, AB) / AB.sqrMagnitude

        //         float t = Vector2.Dot(AP, AB) / abLenSqr;
        //         t = Mathf.Clamp01(t); // Ograniczamy do przedziału odcinka AB

        //         // Finding the P closest point on AB
        //         Vector2 closestPoint = A + t * AB;

        //         // Distance squared comparison (no expensive square root)
        //         if ((P - closestPoint).sqrMagnitude <= sqrThreshold)
        //         {
        //             nearPoints.Add(P);
        //         }
        //     }

        //     return nearPoints;
        // }
    }
}
