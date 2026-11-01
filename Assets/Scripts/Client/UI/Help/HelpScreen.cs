// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI.Help
{
    [UxmlElement]
    public partial class HelpScreen : VisualElement
    {
        private ClientImageCatalog m_images;

        [UxmlAttribute]
        public int PageIndex = 0;

        public HelpScreen()
        {
            RegisterCallback<AttachToPanelEvent>(e => RefreshImages());

            RegisterCallback<DetachFromPanelEvent>(e => Clear());
        }

        internal void SetImages(ClientImageCatalog images)
        {
            m_images = images;
            RefreshImages();
        }

        private void RefreshImages()
        {
#if UNITY_EDITOR
            if (m_images == null && !UnityEngine.Application.isPlaying)
                m_images = ClientImageCatalog.LoadForEditor();
#endif
            Clear();
            if (m_images == null)
                return;
            var items = HelpData.CreateScreens(m_images)[PageIndex];
            foreach (var item in items)
                Add(new HelpItem(item));
        }
    }
}
