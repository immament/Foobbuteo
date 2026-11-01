// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using HungarianAlgorithm;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Server.Game.Model
{
    internal enum TeamPositioningMode
    {
        Neutral,
        Offensive,
        Defensive,
    }

    internal readonly struct DynamicFormationAssignment
    {
        internal byte FormationPlayerIdx { get; }
        internal Vector2 Target { get; }

        internal DynamicFormationAssignment(byte formationPlayerIdx, Vector2 target)
        {
            FormationPlayerIdx = formationPlayerIdx;
            Target = target;
        }
    }

    [Serializable]
    internal class ServerTeamState : FoobbuteoTeamState<ServerPlayerState>
    {
        private Bounds2d m_baseFormationBounds;

        internal static void ValidateManagedRoster(int gameId, ServerTeamState team)
        {
            const byte requiredPlayerCount = 11;

            if (team == null)
                throw new InvalidOperationException($"Game #{gameId} has a missing managed team.");

            string teamContext = $"Game #{gameId} team {team.TeamIdx}";
            if (team.Players == null)
                throw new InvalidOperationException($"{teamContext} has a missing managed roster.");

            if (team.Players.Count != requiredPlayerCount)
                throw new InvalidOperationException(
                    $"{teamContext} must have {requiredPlayerCount} managed players; found {team.Players.Count}."
                );

            var identities = new HashSet<int>();
            for (byte index = 1; index <= requiredPlayerCount; index++)
            {
                var player = team.GetPlayerAtIdx(index);
                string playerContext = $"{teamContext} player #{index}";
                if (player == null)
                    throw new InvalidOperationException($"{playerContext} is missing from the managed roster.");

                if (player.PlayerId <= 0)
                    throw new InvalidOperationException(
                        $"{playerContext} must have a positive player ID; found {player.PlayerId}."
                    );

                if (player.UserId != 0)
                    throw new InvalidOperationException(
                        $"{playerContext} must not have an individual user assignment; found user ID {player.UserId}."
                    );

                if (player.IsCaptain)
                    throw new InvalidOperationException($"{playerContext} must not be a pitch captain in MvM.");

                var expectedSlot = GameSlot.ForPlayer(gameId, new PlayerKey(team.TeamIdx, index));
                if (player.Slot != expectedSlot)
                    throw new InvalidOperationException(
                        $"{playerContext} has slot {player.Slot}; expected {expectedSlot}."
                    );

                if (!identities.Add(player.PlayerId))
                    throw new InvalidOperationException(
                        $"{playerContext} has duplicate player ID {player.PlayerId} in the managed roster."
                    );
            }
        }

        public Bounds2d FormationBounds { get; private set; }

        // X is depth along the pitch; Y is width across the pitch.
        public Vector2 FormationScale { get; set; } = new Vector2(1f, 0.7f);

        // Fraction of the formation's attacking depth kept behind the ball.
        public float OffensiveBallAnchor { get; set; } = 0.4f;
        public float DefensiveBallAnchor { get; set; } = 0.7f;
        public float NeutralBallAnchor { get; set; } = 0.5f;
        public float BallAnchorInfluence { get; set; } = 0.5f;
        public float BallFollowWidth { get; set; } = 0.5f;
        public TeamPositioningMode PositioningMode { get; private set; }

        public ServerTeamState(
            TeamIndex teamIdx,
            int id,
            string name,
            ShirtColorEnum color,
            ServerPlayerState[] players,
            byte score = 0,
            TeamKey teamKey = default
        )
            : base(teamIdx, id, name, color, players, score, teamKey) { }

        internal void UpdatePositioningBounds(
            Vector2 ballPosition,
            TeamPositioningMode positioningMode = TeamPositioningMode.Neutral,
            byte anchoredFormationPlayerIdx = 0,
            bool useStartingPositions = false
        )
        {
            PositioningMode = positioningMode;
            var min = GetPlayerAtIdx(2).FormationPosition;
            var max = min;
            for (byte i = 3; i <= 11; i++)
            {
                var position = GetPlayerAtIdx(i).FormationPosition;
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
            }
            m_baseFormationBounds = new Bounds2d(min, max);

            // Keep the whole outfield rectangle clear of goal areas and pitch boundary lines.
            var allowed = new Bounds2d(
                new Vector2(PitchUtils.GkAreaHome.Max.x + 1f, PitchUtils.PitchBounds.Min.y + 12f),
                new Vector2(PitchUtils.GkAreaAway.Min.x - 1f, PitchUtils.PitchBounds.Max.y - 12f)
            );
            var requestedSize = Vector2.Scale(max - min, FormationScale);
            var allowedSize = allowed.Max - allowed.Min;
            var size = new Vector2(
                Mathf.Clamp(requestedSize.x, 0f, allowedSize.x),
                Mathf.Clamp(requestedSize.y, 0f, allowedSize.y)
            );
            var halfSize = size * 0.5f;
            Vector2 center;
            if (useStartingPositions)
            {
                center = m_baseFormationBounds.Center;
            }
            else if (anchoredFormationPlayerIdx >= 2 && anchoredFormationPlayerIdx <= 11)
            {
                Vector2 normalized = GetBaseFormationNormalized(anchoredFormationPlayerIdx);
                center = ballPosition - Vector2.Scale(normalized - Vector2.one * 0.5f, size);
            }
            else
            {
                var clampedBall = PitchUtils.PitchBounds.Clamp(ballPosition);
                float ballAnchor = Mathf.Clamp01(GetBallAnchor(positioningMode));
                float attackDirection = TeamIdx == TeamIndex.Home ? 1f : -1f;
                float anchoredCenterX = clampedBall.x + attackDirection * (0.5f - ballAnchor) * size.x;
                float legacyCenterX =
                    m_baseFormationBounds.Center.x + (clampedBall.x - PitchUtils.PitchBounds.Center.x) * 0.5f;
                center = new Vector2(
                    Mathf.Lerp(legacyCenterX, anchoredCenterX, Mathf.Clamp01(BallAnchorInfluence)),
                    m_baseFormationBounds.Center.y + (clampedBall.y - PitchUtils.PitchBounds.Center.y) * BallFollowWidth
                );
            }
            center = new Bounds2d(allowed.Min + halfSize, allowed.Max - halfSize).Clamp(center);
            FormationBounds = new Bounds2d(center - halfSize, center + halfSize);
        }

        private float GetBallAnchor(TeamPositioningMode positioningMode) =>
            positioningMode switch
            {
                TeamPositioningMode.Offensive => OffensiveBallAnchor,
                TeamPositioningMode.Defensive => DefensiveBallAnchor,
                _ => NeutralBallAnchor,
            };

        internal Vector2 GetAdaptiveFormationPosition(byte playerIdx)
        {
            var player = GetPlayerAtIdx(playerIdx);
            if (player.IsGk)
                return player.FormationPosition;

            var normalized = GetBaseFormationNormalized(playerIdx);
            return FormationBounds.Min + Vector2.Scale(normalized, FormationBounds.Max - FormationBounds.Min);
        }

        private Vector2 GetBaseFormationNormalized(byte playerIdx)
        {
            var baseSize = m_baseFormationBounds.Max - m_baseFormationBounds.Min;
            var position = GetPlayerAtIdx(playerIdx).FormationPosition;
            return new Vector2(
                baseSize.x > 0f
                    ? Mathf.InverseLerp(m_baseFormationBounds.Min.x, m_baseFormationBounds.Max.x, position.x)
                    : 0.5f,
                baseSize.y > 0f
                    ? Mathf.InverseLerp(m_baseFormationBounds.Min.y, m_baseFormationBounds.Max.y, position.y)
                    : 0.5f
            );
        }

        internal IReadOnlyDictionary<byte, DynamicFormationAssignment> GetDynamicFormationPositions()
        {
            const byte firstOutfieldPlayerIdx = 2;
            const int outfieldPlayerCount = 10;
            const float costScale = 10f;
            var costs = new int[outfieldPlayerCount, outfieldPlayerCount];
            var targets = new Vector2[outfieldPlayerCount];

            for (int targetIdx = 0; targetIdx < outfieldPlayerCount; targetIdx++)
                targets[targetIdx] = GetAdaptiveFormationPosition((byte)(firstOutfieldPlayerIdx + targetIdx));

            for (int playerIdx = 0; playerIdx < outfieldPlayerCount; playerIdx++)
            {
                Vector2 position = GetPlayerAtIdx((byte)(firstOutfieldPlayerIdx + playerIdx)).Position;
                for (int targetIdx = 0; targetIdx < outfieldPlayerCount; targetIdx++)
                {
                    // Squared distance penalizes a single long recovery more than several short shifts.
                    costs[playerIdx, targetIdx] = Mathf.RoundToInt(
                        (position - targets[targetIdx]).sqrMagnitude * costScale
                    );
                }
            }

            int[] assignments = costs.FindAssignments();
            var result = new Dictionary<byte, DynamicFormationAssignment>(outfieldPlayerCount);
            for (int playerIdx = 0; playerIdx < outfieldPlayerCount; playerIdx++)
            {
                byte playerNumber = (byte)(firstOutfieldPlayerIdx + playerIdx);
                byte formationPlayerIdx = (byte)(firstOutfieldPlayerIdx + assignments[playerIdx]);
                result[playerNumber] = new(formationPlayerIdx, targets[assignments[playerIdx]]);
            }

            return result;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            Gizmos.color = (TeamIdx == TeamIndex.Home) ? UnityEngine.Color.blue : UnityEngine.Color.yellow;

            Gizmos.DrawWireCube(
                CoordinateConverter.LogicToGizmo(FormationBounds.Center),
                CoordinateConverter.SizeToGizmo(FormationBounds.Size)
            );
        }
#endif
    }

    // internal class TeamManager
    // {
    //     public int Id;
    //     public string Name;
    // }
}
