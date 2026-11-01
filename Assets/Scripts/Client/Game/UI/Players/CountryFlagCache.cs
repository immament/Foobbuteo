// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game.UI.Players
{
    internal sealed class CountryFlagCache
    {
        private readonly Dictionary<int, Texture2D> m_flags = new();
        private readonly ClientImageCatalog m_images;

        internal CountryFlagCache(ClientImageCatalog images) =>
            m_images = images ?? throw new System.ArgumentNullException(nameof(images));

        public Texture2D Get(int countryId)
        {
            if (!m_flags.TryGetValue(countryId, out var flag))
            {
                flag = m_images.GetCountryFlag(countryId);
                m_flags.Add(countryId, flag);
            }
            return flag;
        }
    }
}
