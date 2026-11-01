// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Shared.Game.Contract;
using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game.Model
{
    [Serializable]
    public abstract class FoobbuteoPlayerState
    {
        public const float k_maxActionPoints = 100f;
        private const float k_minMoveDistance = 100f;
        private const float k_minLowPassDistance = 100f;
        private const float k_minLowHighDistance = 230f;
        private const float k_longShotTresholddDistance = 130f;
        private const float k_lowPassRatio = 0.6f;

        private static readonly Vector2 s_homeBallSlot = new(8f, 0f);
        private static readonly Vector2 s_awayBallSlot = new(-8f, 0f);

        public Vector2 GetLocalBallSlotPosition() =>
            Slot.TeamIdx switch
            {
                TeamIndex.Home => s_homeBallSlot,
                TeamIndex.Away => s_awayBallSlot,
                _ => throw new InvalidOperationException($"Invalid TeamIdx: {Slot.TeamIdx}"),
            };

        // public FoobbuteoPlayerState(TeamIndex teamIdx, byte playerIdx)
        // {
        //     TeamIdx = teamIdx;
        //     PlayerIdx = playerIdx;
        //     Key = new PlayerKey(teamIdx, playerIdx);
        //     if (IsGk)
        //         PlayerMode = PlayerModeEnum.GkSave;
        //     else
        //         PlayerMode = PlayerModeEnum.Confrontation;
        // }

        protected FoobbuteoPlayerState(GameSlot slot)
        {
            Slot = slot;
            if (IsGk)
                PlayerMode = PlayerModeEnum.GkSave;
            else
                PlayerMode = PlayerModeEnum.Confrontation;
        }

        public GameSlot Slot { get; }

        // public TeamIndex TeamIdx { get; }
        public bool InHomeTeam => Slot.TeamIdx == TeamIndex.Home;

        // public byte PlayerIdx { get; }
        // public PlayerKey Key { get; }
        public bool IsGk => Slot.IsGk;
        public int PlayerId { get; protected set; }
        public int UserId { get; protected set; }
        public bool IsUser => UserId > 0;
        public bool IsInactive { get; protected set; }
        public bool IsConnected { get; protected set; }
        public int LastActivityTurn { get; protected set; }
        public bool UsesAutomaticControl => !IsUser || IsInactive;
        public ulong? ClientId { get; protected set; }

        public string Name { get; set; }
        public Vector2 Position { get; protected set; }
        public int CountryId { get; protected set; }
        public int PlayerLevel { get; protected set; }

        [SerializeField]
        private bool hasBall;
        public bool HasBall
        {
            get => hasBall;
            set => hasBall = value;
        }
        public int BallDistance { get; protected set; }
        public int Zone => Position.GetZone();
        public bool IsCaptain { get; protected set; }
        public PlayerModeEnum PlayerMode { get; protected set; }
        public float ActionPoints { get; protected set; } = k_maxActionPoints;

        #region ++ Skills ++
        // Foobbuteo ratings supplied by the repository, on a 0–20 scale.
        public int MoveSkill { get; protected set; }
        public int ConfrontationSkill { get; protected set; }
        public int LowPassSkill { get; protected set; }
        public int LongPassSkill { get; protected set; }
        public int GkPassSkill { get; protected set; }
        public int CrossesSkill { get; protected set; }
        public int ShootingSkill { get; protected set; }
        public int LongShotsSkill { get; protected set; }
        public int GkSavesSkill { get; protected set; }
        public int OutOfAreaSkill { get; protected set; }

        public int GetEffectiveLowPassSkill() =>
            IsGk && PlayerMode == PlayerModeEnum.GkFieldPlayer ? GkPassSkill : LowPassSkill;

        public int GetEffectiveHighPassSkill()
        {
            if (IsGk && PlayerMode == PlayerModeEnum.GkFieldPlayer)
                return GkPassSkill;
            return InCrossZone ? CrossesSkill : LongPassSkill;
        }

        public int ShootingSkillForDistance(float distance) =>
            distance > k_longShotTresholddDistance ? LongShotsSkill : ShootingSkill;

        public float GetFinishingSkill(float distance, bool inPenaltyArea) =>
            Mathf.Pow(ShootingSkillForDistance(distance) + 1f, inPenaltyArea ? 0.7f : 0.4f);

        public float GkInterceptSkill
        {
            get
            {
                if (PitchUtils.IsGoalkeeperAreaZone(Zone))
                    return (PlayerMode == PlayerModeEnum.GkSave) ? GkSavesSkill : 0f;
                return (PlayerMode == PlayerModeEnum.GkOutOfArea) ? OutOfAreaSkill : 0f;
            }
        }

        #endregion -- skills -----

        protected bool IsOnOwnHalf => PitchUtils.IsOnOwnHalf(Slot.TeamIdx, Position);

        private bool InCrossZone => PitchUtils.InCrossZone(Slot.TeamIdx, Zone);

        private float MoveRatio => HasBall ? 0.2f : 0.3f;

        // Min: 100, max: (with ball 130, without ball 160)
        public float MaxMoveDistance => k_minMoveDistance + (MoveRatio * MoveSkill) * 10f;
        public float AvailableMoveDistance => (ActionPoints * MaxMoveDistance) / k_maxActionPoints;

        public float MoveCost(float distance) => (distance / MaxMoveDistance) * k_maxActionPoints;

        public float MaxLowPassDistance => k_minLowPassDistance + (k_lowPassRatio * GetEffectiveLowPassSkill()) * 10f;
        public float AvailableLowPassDistance => (ActionPoints * MaxLowPassDistance) / k_maxActionPoints;

        public float LowPassCost(float distance) => (distance / MaxLowPassDistance) * k_maxActionPoints;

        public float MaxHighPassDistance => k_minLowHighDistance + GetEffectiveHighPassSkill() * 4f;
        public float AvailableHighPassDistance => (ActionPoints * MaxHighPassDistance) / k_maxActionPoints;
        public float MinHighPassDistance => InCrossZone ? 150f : 220f;

        public float HighPassCost(float distance) => (distance / MaxHighPassDistance) * k_maxActionPoints;

        // (10 - (0.25 * skill)) * 10;
        public float ShootCost(float distance) => (10f - (0.25f * ShootingSkillForDistance(distance))) * 10f;

        public void StartTurn()
        {
            ActionPoints = k_maxActionPoints;
        }

        public void RefreshState(Vector2 ballPosition)
        {
            BallDistance = (int)Vector2.Distance(ballPosition, Position);
        }

        internal bool CanClaimPlayer(GameUser user, out string reason)
        {
#if UNITY_EDITOR
            if (user == null)
            {
                reason = "User is null";
                return false;
            }
#endif
            this.LDebug(() => $"{new { UserId, user.InGameRoomId, Slot.GameId, user.GameSlot.HasValue }}");
            if (IsUser && !IsInactive)
            {
                reason = (
                    $"User @{user.UserId} can not claim player {Slot}. Player already taken by another user @{UserId}."
                );
                return false;
            }
            if (user.InGameRoomId != Slot.GameId)
            {
                reason = $"User @{user.UserId} can not claim player {Slot}. User is in other room ${user.InGameRoomId}";
                return false;
            }

            if (user.GameSlot.HasValue)
            {
                reason =
                    $"User @{user.UserId} can not claim player {Slot}. User already control player ${user.GameSlot}";
                return false;
            }
            reason = null;
            return true;
        }

        public string Tag() => Slot.ToString();

        public void ChangePlayerMode(PlayerModeEnum mode)
        {
            // TODO: add validation
            this.PlayerMode = mode;
        }

        // Default Ball slot if player has ball
        public Vector2 BallSlotPosition => Position + GetLocalBallSlotPosition();

        public GameDetails_PlayerContract ToContract()
        {
            return new GameDetails_PlayerContract()
            {
                TeamIdx = Slot.TeamIdx,
                PlayerIdx = Slot.PlayerIdx,
                PlayerId = PlayerId,
                UserId = UserId,
                ClientId = ClientId,
                Name = Name,
                CountryId = CountryId,
                PlayerLevel = PlayerLevel,
                Position = Position,
                HasBall = HasBall,
                BallDistance = BallDistance,
                Zone = Zone,
                IsCaptain = IsCaptain,
                IsInactive = IsInactive,
                IsConnected = IsConnected,
                LastActivityTurn = LastActivityTurn,
                PlayerMode = PlayerMode,
                ActionPoints = ActionPoints,
                // Skills
                MoveSkill = MoveSkill,
                ConfrontationSkill = ConfrontationSkill,
                LowPassSkill = LowPassSkill,
                LongPassSkill = LongPassSkill,
                GkPassSkill = GkPassSkill,
                CrossesSkill = CrossesSkill,
                ShootingSkill = ShootingSkill,
                LongShotsSkill = LongShotsSkill,
                GkSavesSkill = GkSavesSkill,
                OutOfAreaSkill = OutOfAreaSkill,
            };
        }

        public void OnPlayerControlClaimed(GameDetails_PlayerContract pl)
        {
            this.LDebug(() => $"pl.IsCaptain: {pl.IsCaptain}");
            UserId = pl.UserId;
            PlayerId = pl.PlayerId;
            Name = pl.Name;
            CountryId = pl.CountryId;
            PlayerLevel = pl.PlayerLevel;
            ClientId = pl.ClientId;
            IsCaptain = pl.IsCaptain;
            IsInactive = pl.IsInactive;
            IsConnected = pl.IsConnected;
            LastActivityTurn = pl.LastActivityTurn;
            PlayerMode = pl.PlayerMode;
            ActionPoints = pl.ActionPoints;

            // Skill
            MoveSkill = pl.MoveSkill;
            ConfrontationSkill = pl.ConfrontationSkill;
            LowPassSkill = pl.LowPassSkill;
            LongPassSkill = pl.LongPassSkill;
            GkPassSkill = pl.GkPassSkill;
            CrossesSkill = pl.CrossesSkill;
            ShootingSkill = pl.ShootingSkill;
            LongShotsSkill = pl.LongShotsSkill;
            GkSavesSkill = pl.GkSavesSkill;
            OutOfAreaSkill = pl.OutOfAreaSkill;
        }

        internal void ClaimPlayer(GameUser user, bool isCaptain)
        {
            user.AssignPlayer(this);

            UserId = user.UserId;
            PlayerId = user.PlayerId;
            ClientId = user.ClientId.Value;
            Name = user.Name;
            IsCaptain = isCaptain;
            IsInactive = false;
            IsConnected = user.ClientId.HasValue;
            // TODO: skills
        }
    }
}
// Floor(100*(1.08^level))-(80-(level-1)*10)
// Level/ exp
// 1 28
// 2 46
// 3 65
// 4 86
// 5 106
// 6 128
// 7 151
// 8 175
// 9 199
// 10 225
// 11 253
// 12 281
// 13 311
// 14 343
// 15 377
// 16 412
// 17 450
// 18 489
// 19 531
// 20 576
// 21 623
// 22 673
// 23 727
// 24 784
// 25 844
// 26 909
// 27 978
// 28 1052
// 29 1131
// 30 1216
// 31 1306
// 32 1403
// 33 1507
// 34 1619
// 35 1738
// 36 1866
// 37 2004
// 38 2152
// 39 2311
// 40 2482
// 41 2666
// 42 2863
// 43 3076
// 44 3305
// 45 3552
// 46 3817
// 47 4103
// 48 4411
// 49 4742
// 50 ...
