// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Server.Game;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.dev;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

[Serializable]
internal class ServerPlayerState : FoobbuteoPlayerState
{
    private const int k_MaxTurnsMovingWithBall = 2;
    private int m_turnsMovedWithBall;
    private PlayerAI m_ai;

    public Vector2 FormationPosition { get; set; }
    internal PlayerAI AI => m_ai ??= new(this);
    internal bool CanMoveWithBall => m_turnsMovedWithBall < k_MaxTurnsMovingWithBall;

    internal void RegisterActivity(int turn)
    {
        LastActivityTurn = turn;
        IsInactive = false;
    }

    internal void CloseInput(int turn, int inactiveAfterTurns)
    {
        IsInactive = IsUser && turn - LastActivityTurn >= inactiveAfterTurns;
    }

    internal bool SetConnection(ulong? clientId)
    {
        bool changed = ClientId != clientId || IsConnected != clientId.HasValue;
        ClientId = clientId;
        IsConnected = IsUser && clientId.HasValue;
        return changed;
    }

    internal void SetCaptain(bool isCaptain) => IsCaptain = isCaptain;

    public ServerPlayerState(GameSlot slot)
        : base(slot)
    {
        FormationPosition = Formation442.GetPosition(slot.TeamIdx, slot.PlayerIdx);
        Position = FormationPosition;
    }

    internal void InitializeCreator(GameUser creator)
    {
        UserId = creator.UserId;
        PlayerId = creator.PlayerId;
        Name = creator.Name;
    }

    public string Server_RegisterMoveCmd(ref PlayerCommand command)
    {
        this.LTrace(command);
        return command.Action switch
        {
            PlayerActionEnum.Move => Server_RegisterMoveCmd(command.TargetPosition),
            PlayerActionEnum.LowPass => Server_RegisterLowPassCmd(command.TargetPosition),
            PlayerActionEnum.HighPass => Server_RegisterHighPassCmd(command.TargetPosition),
            PlayerActionEnum.Shot => Server_RegisterShotCmd(command.TargetPosition),
            _ => null,
        };
    }

    internal void RestoreActionPoints(float actionPoints)
    {
        ActionPoints = actionPoints;
    }

    private string Server_RegisterMoveCmd(Vector2 target)
    {
        if (HasBall && m_turnsMovedWithBall >= k_MaxTurnsMovingWithBall)
            return $"Player already moved with ball in {m_turnsMovedWithBall} consecutive turns. Only pass or shoot is allowed.";

        if (!target.IsOnPitch())
            return $"Target positon is outside of pitch! {target}";
        if (!IsGk && target.InAnyGkArea())
            return $"Goalkeeper Area {target}"; // LANG_FOOB7

        float distance = Vector2.Distance(Position, target);
        float cost = MoveCost(distance);

        if (cost > ActionPoints)
            return $"Not enough points! {cost} > {ActionPoints}"; // LANG_FOOB5

        ActionPoints -= cost;
        return null;
    }

    internal void Server_RegisterTurnMovedWithBall(bool movedWithBall)
    {
        m_turnsMovedWithBall = movedWithBall ? m_turnsMovedWithBall + 1 : 0;
    }

    private string Server_RegisterLowPassCmd(Vector2 target)
    {
        if (!HasBall)
            return $"Player has not ball!!";
        if (!target.IsOnPitch())
            return $"Target positon is outside of pitch! {target}";
        float distance = Vector2.Distance(Position, target);
        float cost = LowPassCost(distance);

        if (cost > ActionPoints)
            return $"Not enough points! {cost} > {ActionPoints}"; // LANG_FOOB5

        ActionPoints = 0f;
        return null;
    }

    private string Server_RegisterHighPassCmd(Vector2 target)
    {
        if (!HasBall)
            return $"Player has not ball!!";

        if (!target.IsOnPitch())
            return $"Target positon is outside of pitch! {target}";

        float distance = Vector2.Distance(Position, target);

        if (distance < MinHighPassDistance)
            return $"Distance too short! {distance} < {MinHighPassDistance}"; // LANG_FOOB6

        float cost = HighPassCost(distance);

        if (cost > ActionPoints)
            return $"Not enough points! {target}, {cost} > {ActionPoints}"; // LANG_FOOB5

        ActionPoints = 0f;
        return null;
    }

    private string Server_RegisterShotCmd(Vector2 target)
    {
        if (!HasBall)
            return $"Player has not ball!!";
        if (IsOnOwnHalf)
            return $"Shots only after midfield! [{Position}]"; // LANG_FOOB85

        // Ensure that target is on end line.
        target = new(InHomeTeam ? PitchUtils.PitchBounds.Max.x : PitchUtils.PitchBounds.Min.x, target.y);

        float distance = Vector2.Distance(Position, target);
        float cost = ShootCost(distance);

        if (cost > ActionPoints)
            return $"Not enough points! {cost} > {ActionPoints}"; // LANG_FOOB5

        ActionPoints = 0f;
        return null;
    }

    public void Server_PlayerMove(Vector2 targetPosition)
    {
        this.LDebug(() => $"{Slot} => {targetPosition}");
        Position = targetPosition;
    }

    #region ++ TESTS ++++++


    public static ServerPlayerState CreateForTest(int gameId, TeamIndex teamIdx, byte playerIdx)
    {
        var result = new ServerPlayerState(new GameSlot(gameId, teamIdx, playerIdx))
        {
            PlayerId = playerIdx,
            Name = $"Player {(teamIdx == TeamIndex.Home ? "A" : "B")}{playerIdx}",
        };
        result.MoveSkill = 7;
        result.ConfrontationSkill = 6;
        result.LowPassSkill = 6;
        result.LongPassSkill = 6;
        result.GkPassSkill = 0;
        result.CrossesSkill = 6;
        result.ShootingSkill = 6;
        result.LongShotsSkill = 6;
        result.GkSavesSkill = 0;
        result.OutOfAreaSkill = 0;
        switch (playerIdx)
        {
            case 1:
                result.ConfrontationSkill = 10;
                result.GkPassSkill = 10;
                result.GkSavesSkill = 10;
                result.OutOfAreaSkill = 10;
                break;
            case 2:
            case 3:
            case 4:
            case 5:
                result.MoveSkill = 8;
                result.ConfrontationSkill = 8;
                result.LongPassSkill = 8;
                break;
            case 6:
            case 8:
                result.MoveSkill = 8;
                result.ConfrontationSkill = 7;
                result.LowPassSkill = 8;
                result.LongPassSkill = 8;
                result.LongShotsSkill = 8;
                break;
            case 7:
            case 11:
                result.ConfrontationSkill = 7;
                result.LowPassSkill = 8;
                result.LongPassSkill = 8;
                result.CrossesSkill = 10;
                result.ShootingSkill = 8;
                result.LongShotsSkill = 8;
                break;
            case 9:
            case 10:
                result.ConfrontationSkill = 7;
                result.LowPassSkill = 8;
                result.LongPassSkill = 8;
                result.CrossesSkill = 8;
                result.ShootingSkill = 10;
                result.LongShotsSkill = 8;
                break;
        }
        return result;
    }

    public static ServerPlayerState CreateForTest(
        int gameId,
        TeamIndex teamIdx,
        byte playerIdx,
        int playerId,
        string name,
        Vector2 position,
        int countryId = 1,
        int playerLevel = 5,
        bool hasBall = false,
        int ballDistance = 0,
        int userId = 0,
        bool isCaptain = false,
        PlayerModeEnum playerMode = PlayerModeEnum.Interception,
        float actionPoints = 100f,
        int moveSkill = 5,
        int confrontationSkill = 5,
        int lowPassSkill = 5,
        int longPassSkill = 5,
        int gkPassSkill = 5,
        int crossesSkill = 5,
        int shootingSkill = 5,
        int longShotsSkill = 5,
        int gkSavesSkill = 5,
        int outOfAreaSkill = 5
    )
    {
        return new ServerPlayerState(new GameSlot(gameId, teamIdx, playerIdx))
        {
            PlayerId = playerId,
            Name = name,
            Position = position,
            CountryId = countryId,
            PlayerLevel = playerLevel,
            HasBall = hasBall,
            BallDistance = ballDistance,
            // Zone = zone,
            UserId = userId,
            IsCaptain = isCaptain,
            PlayerMode = playerMode,
            ActionPoints = actionPoints,
            MoveSkill = moveSkill,
            ConfrontationSkill = confrontationSkill,
            LowPassSkill = lowPassSkill,
            LongPassSkill = longPassSkill,
            GkPassSkill = gkPassSkill,
            CrossesSkill = crossesSkill,
            ShootingSkill = shootingSkill,
            LongShotsSkill = longShotsSkill,
            GkSavesSkill = gkSavesSkill,
            OutOfAreaSkill = outOfAreaSkill,
        };
    }

    internal static ServerPlayerState FromContract(int gameId, GameDetails_PlayerContract src)
    {
        return new ServerPlayerState(new GameSlot(gameId, src.TeamIdx, src.PlayerIdx))
        {
            PlayerId = src.PlayerId,
            Name = src.Name,
            Position = src.Position,
            CountryId = src.CountryId,
            PlayerLevel = src.PlayerLevel,
            HasBall = src.HasBall,
            BallDistance = src.BallDistance,
            UserId = src.UserId,
            ClientId = src.ClientId,
            IsCaptain = src.IsCaptain,
            IsInactive = src.IsInactive,
            IsConnected = src.IsConnected,
            LastActivityTurn = src.LastActivityTurn,
            PlayerMode = src.PlayerMode,
            MoveSkill = src.MoveSkill,
            ConfrontationSkill = src.ConfrontationSkill,
            LowPassSkill = src.LowPassSkill,
            LongPassSkill = src.LongPassSkill,
            GkPassSkill = src.GkPassSkill,
            CrossesSkill = src.CrossesSkill,
            ShootingSkill = src.ShootingSkill,
            LongShotsSkill = src.LongShotsSkill,
            GkSavesSkill = src.GkSavesSkill,
            OutOfAreaSkill = src.OutOfAreaSkill,
            ActionPoints = src.ActionPoints,
        };
    }

    #endregion -- TESTS --
}
