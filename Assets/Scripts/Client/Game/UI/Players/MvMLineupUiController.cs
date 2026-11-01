// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Core;
using Imma.Foobbuteo.Shared;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Imma.Foobbuteo.Shared.Game.Model;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class MvMLineupUiController : IDisposable
    {
        private readonly IClientGameRoomNB m_api;
        private readonly ClientGameState m_game;
        private readonly Func<GameUser> m_user;
        private readonly Func<TeamIndex, TeamVM> m_team;
        private readonly MvMLineupVM m_vm;
        private readonly PlayerTableView m_starting = new();
        private readonly PlayerTableView m_reserves = new();
        private readonly List<Button> m_triggers = new();
        private VisualElement m_root;
        private VisualElement m_overlay;
        private VisualElement m_background;
        private VisualElement m_pitch;
        private Button m_apply;
        private Button m_cancel;
        private VisualElement m_opener;
        private MvMLineupLayout m_layout;
        private LineupDragSession m_dragSession;

        internal MvMLineupUiController(
            IClientGameRoomNB api,
            ClientGameState game,
            Func<GameUser> user,
            Func<TeamIndex, TeamVM> team,
            ClientImageCatalog images
        )
        {
            m_vm = new MvMLineupVM(game.GameId, images);
            m_vm.propertyChanged += OnLineupChanged;
            m_api = api;
            m_game = game;
            m_user = user;
            m_team = team;
            m_api.MvMLineupLoaded += OnLoaded;
            m_api.MvMLineupSaved += OnSaved;
            m_api.MvMJoinCompleted += OnJoined;
            m_api.TurnStarted += OnStarted;
            m_api.GameEnded += OnStarted;
            m_starting.RowCreated += AttachRow;
            m_reserves.RowCreated += AttachRow;
        }

        internal void Bind(VisualElement root)
        {
            Unbind();
            if (root == null)
                return;
            m_root = root;
            m_overlay = root.Q("mvm-lineup-overlay");
            m_background = root.Q("mvm-lineup-background");
            m_pitch = m_overlay.Q("mvm-lineup-pitch");
            m_dragSession = new LineupDragSession(m_overlay);
            m_overlay.dataSource = m_vm;
            m_overlay.dataSourcePath = new PropertyPath();
            m_background.SetBinding(
                nameof(VisualElement.enabledSelf),
                Binding(m_vm, nameof(MvMLineupVM.BackgroundEnabled))
            );
            foreach (
                string name in new[]
                {
                    "home-edit-lineup",
                    "away-edit-lineup",
                    "mobile-home-edit-lineup",
                    "mobile-away-edit-lineup",
                }
            )
            {
                var button = root.Q<Button>(name);
                button.dataSource = m_vm;
                button.dataSourcePath = new PropertyPath();
                button.clicked += Open;
                button.RegisterCallback<PointerDownEvent>(RememberOpener);
                m_triggers.Add(button);
            }
            m_apply = m_overlay.Q<Button>("mvm-lineup-apply");
            m_cancel = m_overlay.Q<Button>("mvm-lineup-cancel");
            m_apply.clicked += Apply;
            m_cancel.clicked += Cancel;
            m_overlay.RegisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            m_overlay.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            m_starting.Bind(
                m_overlay.Q("mvm-lineup-starting"),
                m_vm.Starting,
                viewControls: m_overlay.Q("mvm-lineup-views"),
                selectView: m_vm.SetView
            );
            m_reserves.Bind(
                m_overlay.Q("mvm-lineup-reserves"),
                m_vm.Reserves,
                Array.Empty<PlayerTableViewDefinition>()
            );
            m_layout = new MvMLineupLayout(root, m_overlay, m_vm);
            Refresh();
            RenderPitch();
        }

        internal void Refresh()
        {
            var user = m_user();
            TeamIndex? side = null;
            if (
                m_game.MatchType == FoobbuteoGameType.MvM
                && m_game.Status == FoobbuteoGameStatus.Pending
                && user != null
                && user.GameSlot.HasValue
                && user.GameSlot.Value.IsManager
                && user.GameSlot.Value.GameId == m_game.GameId
            )
            {
                var slot = user.GameSlot.Value;
                var team = slot.TeamIdx == TeamIndex.Home ? m_game.HomeTeam : m_game.AwayTeam;
                if (team?.Id == user.UserId)
                    side = slot.TeamIdx;
            }
            bool changed = side != m_vm.Team;
            m_vm.SetTeam(side);
            if (changed && side.HasValue)
                RequestLoad();
        }

        private void RequestLoad()
        {
            if (m_vm.Busy)
                return;
            m_vm.SetBusy();
            try
            {
                m_api.LoadMvMLineup();
            }
            catch (Exception)
            {
                OnLoaded(Failure("The squad request could not be sent. Try reopening the editor."));
            }
        }

        private void RememberOpener(PointerDownEvent evt) => m_opener = (VisualElement)evt.currentTarget;

        private void Open()
        {
            Refresh();
            if (!m_vm.Team.HasValue)
                return;
            m_opener ??= m_root.focusController.focusedElement as VisualElement;
            m_opener ??= m_triggers.Find(button => button.worldBound.width > 0 && button.worldBound.height > 0);
            m_layout.Refresh();
            m_vm.Open(m_team(m_vm.Team.Value).Name, m_layout.IsSmall);
            if (m_vm.Draft == null)
                RequestLoad();
            RenderPitch();
            m_cancel.Focus();
        }

        private void Apply()
        {
            if (!m_vm.CanApply)
                return;
            var draft = m_vm.Draft;
            m_vm.SetBusy();
            try
            {
                m_api.SaveMvMLineup(draft.Original, draft.Starting);
            }
            catch (Exception)
            {
                OnSaved(Failure("The lineup request could not be sent. Please try again."));
            }
        }

        private void Cancel()
        {
            if (m_vm.Busy)
                return;
            m_vm.Close();
            m_opener?.Focus();
        }

        private void OnLoaded(MvMLineupResult result)
        {
            if (!m_vm.Team.HasValue)
                return;
            m_vm.Load(result);
            RenderPitch();
        }

        private void OnSaved(MvMLineupResult result)
        {
            if (!m_vm.IsOpen)
                return;
            m_vm.SaveCompleted(result);
            if (result.Success)
                m_opener?.Focus();
        }

        private void OnJoined(MvMJoinResult result)
        {
            if (result.Success)
                Refresh();
        }

        private void OnStarted(TurnStateChangedMessage message)
        {
            if (m_vm.IsOpen)
                AppCtx.Client.ShowToast("The match has started. Lineup editing is closed.");
            m_vm.SetTeam(null);
        }

        private void Swap(int source, int destination)
        {
            if (m_vm.Swap(source, destination))
                RenderPitch();
        }

        private void OnLineupChanged(object sender, BindablePropertyChangedEventArgs evt)
        {
            if (!m_vm.IsOpen || m_vm.Busy || !m_vm.Team.HasValue)
                m_dragSession?.Cancel();
        }

        private void AttachRow(VisualElement element, PlayerListRowVM row) =>
            AttachDrag(element, row.PlayerId, row.Name);

        private void AttachDrag(VisualElement element, int id, string name)
        {
            element.userData = id;
            element.AddToClassList("lineup-editor__target");
            element.AddManipulator(
                new LineupDragManipulator(
                    m_dragSession,
                    id,
                    () =>
                    {
                        int index = Array.IndexOf(m_vm.Draft.Starting, id);
                        return CreatePlayerCard(name, index >= 0 ? index + 1 : null, m_team(m_vm.Team.Value), true);
                    },
                    () => m_vm.IsOpen && !m_vm.Busy && m_vm.Team.HasValue,
                    Swap
                )
            );
        }

        private void RenderPitch()
        {
            if (m_pitch == null)
                return;
            m_pitch.Clear();
            if (m_vm.Draft == null || !m_vm.Team.HasValue)
                return;
            var team = m_team(m_vm.Team.Value);
            var lineup = m_vm.Draft.Starting;
            for (int index = 0; index < lineup.Length; index++)
            {
                var player = m_vm.Draft.GetPlayer(lineup[index], m_vm.Team.Value);
                var card = CreatePlayerCard(player.Name, index + 1, team);
                card.AddToClassList("lineup-editor__player--slot-" + (index + 1));
                AttachDrag(card, player.PlayerId, player.Name);
                m_pitch.Add(card);
            }
        }

        private static VisualElement CreatePlayerCard(string playerName, int? number, TeamVM team, bool preview = false)
        {
            var card = new VisualElement();
            card.AddToClassList("lineup-editor__player");
            var shirt = new Label(number?.ToString() ?? "");
            shirt.AddToClassList("lineup-editor__shirt");
            if (preview && team?.Shirt == null)
            {
                shirt.text = "✥";
                shirt.AddToClassList("lineup-editor__shirt--move");
            }
            else
            {
                shirt.SetBinding("style.backgroundImage", Binding(team, nameof(TeamVM.Shirt)));
                shirt.SetBinding("style.color", Binding(team, nameof(TeamVM.ShirtTextColor)));
            }
            card.Add(shirt);
            var name = new Label(playerName) { enableRichText = false, tooltip = preview ? null : playerName };
            name.AddToClassList("lineup-editor__name");
            card.Add(name);
            return card;
        }

        private static DataBinding Binding(object source, string path) =>
            new()
            {
                dataSource = source,
                dataSourcePath = new PropertyPath(path),
                bindingMode = BindingMode.ToTarget,
                updateTrigger = BindingUpdateTrigger.OnSourceChanged,
            };

        private static MvMLineupResult Failure(string error) => new() { Error = error };

        private void OnBackdrop(ClickEvent evt)
        {
            if (evt.target == m_overlay)
                Cancel();
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Cancel();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Tab)
            {
                var focusable = m_overlay
                    .Query<VisualElement>()
                    .ToList()
                    .FindAll(element =>
                        element.focusable
                        && element.enabledInHierarchy
                        && element.worldBound.width > 0
                        && element.worldBound.height > 0
                    );
                if (focusable.Count == 0)
                    return;
                int current = focusable.IndexOf(m_overlay.focusController.focusedElement as VisualElement);
                int next = (current + (evt.shiftKey ? -1 : 1) + focusable.Count) % focusable.Count;
                focusable[next].Focus();
                evt.StopPropagation();
                m_overlay.focusController.IgnoreEvent(evt);
            }
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target == m_overlay)
                Unbind();
        }

        internal void Unbind()
        {
            m_dragSession?.Dispose();
            m_dragSession = null;
            m_layout?.Dispose();
            m_layout = null;
            m_starting.Dispose();
            m_reserves.Dispose();
            foreach (var trigger in m_triggers)
            {
                trigger.clicked -= Open;
                trigger.UnregisterCallback<PointerDownEvent>(RememberOpener);
                trigger.dataSource = null;
            }
            m_triggers.Clear();
            if (m_overlay == null)
                return;
            m_apply.clicked -= Apply;
            m_cancel.clicked -= Cancel;
            m_overlay.UnregisterCallback<ClickEvent>(OnBackdrop);
            m_overlay.UnregisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            m_overlay.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            m_background.ClearBinding(nameof(VisualElement.enabledSelf));
            m_background.SetEnabled(true);
            m_overlay.dataSource = null;
            m_root = m_overlay = m_background = m_pitch = null;
            m_opener = null;
        }

        public void Dispose()
        {
            Unbind();
            m_vm.propertyChanged -= OnLineupChanged;
            m_api.MvMLineupLoaded -= OnLoaded;
            m_api.MvMLineupSaved -= OnSaved;
            m_api.MvMJoinCompleted -= OnJoined;
            m_api.TurnStarted -= OnStarted;
            m_api.GameEnded -= OnStarted;
            m_starting.RowCreated -= AttachRow;
            m_reserves.RowCreated -= AttachRow;
            m_vm.Close();
        }
    }
}
