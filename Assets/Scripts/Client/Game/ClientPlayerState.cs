// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;

[Serializable]
internal class ClientPlayerState : FoobbuteoPlayerState
{
    public ClientPlayerState(GameSlot slot)
        : base(slot) { }

    public void Client_UpdateActionPoints(float remaingActionPoints)
    {
        ActionPoints = remaingActionPoints;
    }

    public static ClientPlayerState FromContract(int gameId, GameDetails_PlayerContract src)
    {
        return new ClientPlayerState(new GameSlot(gameId, src.TeamIdx, src.PlayerIdx))
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
}
