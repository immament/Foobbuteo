// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Help
{
    [UxmlElement]
    public partial class HelpItem : VisualElement
    {
        public HelpItem() { }

        public HelpItem(HelpItemData item)
        {
            AddToClassList("help-item");
            Add(new Image() { sprite = item.image, name = "image" });
            var panel = new VisualElement();
            panel.Add(new Label(item.title) { name = "title" });
            panel.Add(new Label(item.description) { name = "description" });
            Add(panel);
        }
    }
}
