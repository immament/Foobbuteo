// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class PlayerTableView : IDisposable
    {
        public event Action<VisualElement, PlayerListRowVM> RowCreated;
        private PlayerTableVM m_vm;
        private VisualElement m_root;
        private VisualElement m_content;
        private VisualElement m_views;
        private ScrollView m_scroll;
        private readonly List<(Button Button, Action Click)> m_buttons = new();

        public void Bind(
            VisualElement root,
            PlayerTableVM vm,
            IReadOnlyList<PlayerTableViewDefinition> views = null,
            VisualElement viewControls = null,
            Action<PlayerTableViewDefinition> selectView = null
        )
        {
            Dispose();
            m_vm = vm;
            m_root = root;
            m_views = viewControls ?? root.Q("player-table-views");
            m_content = root.Q("player-table-content");
            m_scroll = root.Q<ScrollView>("player-table-scroll");
            foreach (var view in views ?? PlayerTableViewDefinition.Defaults)
            {
                Action click = () =>
                {
                    if (selectView != null)
                        selectView(view);
                    else
                        m_vm.SetView(view);
                };
                var button = new Button(click) { text = view.Name };
                button.AddToClassList(
                    "fbt-btn",
                    "fbt-btn-solid",
                    "fbt-theme-primary",
                    "fbt-btn-compact",
                    "player-table__view"
                );
                m_views.Add(button);
                m_buttons.Add((button, click));
            }
            m_vm.propertyChanged += OnChanged;
            Render();
        }

        private void OnChanged(object sender, BindablePropertyChangedEventArgs evt)
        {
            if (evt.propertyName == nameof(PlayerTableVM.View))
                m_scroll.scrollOffset = UnityEngine.Vector2.zero;
            Render();
        }

        private void Render()
        {
            m_root.EnableInClassList("player-table--average-only", m_vm.View == PlayerTableViewDefinition.AverageOnly);
            foreach (var entry in m_buttons)
                entry.Button.EnableInClassList("player-table__view--selected", entry.Button.text == m_vm.View.Name);
            m_content.Clear();
            var header = new VisualElement();
            header.AddToClassList("player-table__row", "player-table__row--header");
            foreach (var column in m_vm.View.Columns)
            {
                var heading = new Label(column.Heading) { tooltip = column.Tooltip };
                StyleCell(heading, column);
                header.Add(heading);
            }
            m_content.Add(header);
            for (int i = 0; i < m_vm.Rows.Count; i++)
            {
                var data = m_vm.Rows[i];
                var row = new VisualElement { dataSource = data, userData = data.PlayerId };
                row.AddToClassList("player-table__row");
                row.EnableInClassList("player-table__row--alternate", i % 2 != 0);
                foreach (var column in m_vm.View.Columns)
                {
                    bool flag = column.Property == nameof(PlayerListRowVM.Flag);
                    VisualElement cell = flag
                        ? new Image { scaleMode = UnityEngine.ScaleMode.ScaleToFit }
                        : new Label { enableRichText = false };
                    StyleCell(cell, column);
                    cell.SetBinding(
                        flag ? nameof(Image.image) : nameof(Label.text),
                        new DataBinding
                        {
                            dataSourcePath = new PropertyPath(column.Property),
                            bindingMode = BindingMode.ToTarget,
                            updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                        }
                    );
                    if (column.Property == nameof(PlayerListRowVM.Name))
                    {
                        cell.tooltip = data.Name;
                        cell.EnableInClassList("player-table__cell--user-name", data.IsUser);
                    }
                    row.Add(cell);
                }
                m_content.Add(row);
                RowCreated?.Invoke(row, data);
            }
            if (m_vm.Rows.Count == 0)
                m_content.Add(new Label("No players available") { name = "player-table-empty" });
        }

        private static void StyleCell(VisualElement cell, PlayerTableColumn column)
        {
            cell.AddToClassList("player-table__cell", "player-table__cell--" + column.Modifier);
        }

        public void Dispose()
        {
            if (m_vm != null)
                m_vm.propertyChanged -= OnChanged;
            foreach (var entry in m_buttons)
                entry.Button.clicked -= entry.Click;
            m_buttons.Clear();
            m_views?.Clear();
            m_content?.Clear();
            m_vm = null;
            m_root = m_content = m_views = null;
            m_scroll = null;
        }
    }
}
