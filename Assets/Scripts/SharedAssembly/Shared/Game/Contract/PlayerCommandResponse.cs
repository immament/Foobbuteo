// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Unity.Netcode;

namespace Imma.Foobbuteo.Shared.Game.Contract
{
    public struct PlayerCommandResponse : INetworkSerializable
    {
        private PlayerCommand m_command;
        public PlayerCommand Command => m_command;

        private bool m_isAccepted;
        public bool IsAccepted => m_isAccepted;

        // private float m_moveCost;
        // public float MoveCost => m_moveCost;
        private float m_remaingActionPoints;
        public float RemaingActionPoints => m_remaingActionPoints;

        private string m_reason;
        public string Reason => m_reason;

        private PlayerCommandResponse(PlayerCommand cmd, bool isAccepted, string reason = null)
        {
            m_command = cmd;
            m_isAccepted = isAccepted;
            m_reason = reason ?? string.Empty;
            // m_moveCost = 0f;
            m_remaingActionPoints = 0f;
        }

        public static PlayerCommandResponse Accept(PlayerCommand cmd, float remaingActionPoints)
        {
            return new(cmd, true) { m_remaingActionPoints = remaingActionPoints };
        }

        public static PlayerCommandResponse Reject(PlayerCommand cmd, string reason)
        {
            return new(cmd, false, reason);
        }

        public override string ToString()
        {
            return $"(PlayerCommandResponse: {(new { IsAccepted })})";
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s)
            where T : IReaderWriter
        {
            m_command.NetworkSerialize(s);
            s.SerializeValue(ref m_isAccepted);
            s.SerializeValue(ref m_reason);
            s.SerializeValue(ref m_remaingActionPoints);
        }
    }

    // get_pvp_player = 10
    // substitutions  = 8
}
