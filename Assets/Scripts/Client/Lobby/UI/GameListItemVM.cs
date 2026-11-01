// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Runtime.CompilerServices;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Lobby.UI
{
    [Serializable]
    public class GameListItemVM : INotifyBindablePropertyChanged
    {
        private static readonly Color32 s_endedBackgroundColor = new(96, 105, 115, 255);
        private static readonly Color32 s_ongoingBackgroundColor = new(31, 135, 68, 255);
        private static readonly Color32 s_pendingBackgroundColor = new(45, 125, 255, 255);

        public static GameListItemVM FromContract(GameInfoContract game)
        {
            bool isMvM = game.MatchType == FoobbuteoGameType.MvM;
            string ownerName = isMvM ? game.OwnerName : BuildTeamName(game.OwnerName, game.OwnerUid);
            string opponentName = isMvM
                ? (game.OpponentUid <= 0 ? "Waiting for opponent" : game.OpponentName)
                : BuildTeamName(game.OpponentName, game.OpponentUid);
            return new GameListItemVM()
            {
                GameId = game.GameId,
                GameIdText = $"#{game.GameId}",
                Status = game.Status,
                StartDateTime = game.StartDateTime.LocalDateTime,
                Turn = game.Turn,
                NumberOfTurns = game.NumberOfTurns,
                HasResult = game.HasResult,
                ResultText = $"{game.OwnerScore} : {game.OpponentScore}",
                OwnerName = ownerName,
                OpponentName = opponentName,
                GameName = BuildGameName(game.OwnerName, game.OpponentName),
            };
        }

        private static string BuildTeamName(string name, long uid)
        {
            if (uid > 0)
                return $"{name} <size=14>#{uid}</size>";
            return name;
        }

        private static string BuildGameName(string homeTeam, string awayTeam)
        {
            return $"{homeTeam} vs {awayTeam}";
        }

        public int GameId;

        [CreateProperty(ReadOnly = true)]
        public string GameIdText;

        private FoobbuteoGameStatus m_status;
        public FoobbuteoGameStatus Status
        {
            get => m_status;
            private set
            {
                m_status = value;
                // if (m_status == value)
                //     return;
                StatusText = BuildStatusText();
                Notify(nameof(StatusText));
                Notify(nameof(StatusBackgroundColor));
            }
        }

        private string BuildStatusText()
        {
            return m_status switch
            {
                FoobbuteoGameStatus.Pending => "Pending",
                FoobbuteoGameStatus.Ongoing => "Ongoing",
                FoobbuteoGameStatus.Ended => "Ended",
                _ => string.Empty,
            };
        }

        [CreateProperty(ReadOnly = true)]
        public string StatusText { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public Color StatusBackgroundColor =>
            m_status switch
            {
                FoobbuteoGameStatus.Ended => s_endedBackgroundColor,
                FoobbuteoGameStatus.Ongoing => s_ongoingBackgroundColor,
                FoobbuteoGameStatus.Pending => s_pendingBackgroundColor,
                _ => Color.clear,
            };

        [DontCreateProperty]
        private DateTime m_startDateTime;

        public DateTime StartDateTime
        {
            get => m_startDateTime;
            private set
            {
                if (m_startDateTime == value)
                    return;
                m_startDateTime = value;
                StartDateText = m_startDateTime.ToString("yyyy-MM-dd");
                StartTimeText = m_startDateTime.ToString("HH:mm");
                Notify(nameof(StartDateText));
                Notify(nameof(StartTimeText));
            }
        }

        [DontCreateProperty]
        public int Turn;

        [DontCreateProperty]
        public int NumberOfTurns;

        [CreateProperty(ReadOnly = true)]
        public string TurnText => $"{Turn}/{NumberOfTurns}";

        [CreateProperty(ReadOnly = true)]
        public bool HasResult { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string ResultText { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string StartDateText { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string StartTimeText { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string StartText => $"{StartDateText} {StartTimeText}";

        [CreateProperty(ReadOnly = true)]
        public string OwnerName { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string GameName { get; private set; }

        [CreateProperty(ReadOnly = true)]
        public string OpponentName { get; private set; }

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        private void Notify([CallerMemberName] string propertyName = "")
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propertyName));
        }
    }
}
