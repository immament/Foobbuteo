// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEngine;

namespace Imma.Foobbuteo.Client
{
    public sealed class ClientImageCatalog : ScriptableObject
    {
        [Serializable]
        private struct CountryFlag
        {
            public int CountryId;
            public Texture2D Texture;
        }

        [Serializable]
        private struct Shirt
        {
            public ShirtColorEnum Color;
            public Sprite Sprite;
        }

        [SerializeField]
        private CountryFlag[] m_countryFlags = Array.Empty<CountryFlag>();

        [SerializeField]
        private Shirt[] m_shirts = Array.Empty<Shirt>();

        [SerializeField]
        private Sprite m_moveHelpImage;

        [SerializeField]
        private Sprite m_passHelpImage;

        [SerializeField]
        private Sprite m_longPassHelpImage;

        [SerializeField]
        private Sprite m_shotHelpImage;

        [SerializeField]
        private Sprite m_confrontationHelpImage;

        [SerializeField]
        private Sprite m_interceptionHelpImage;

        [SerializeField]
        private Sprite m_goalkeeperSaveHelpImage;

        [SerializeField]
        private Sprite m_goalkeeperOutOfAreaHelpImage;

        [SerializeField]
        private Sprite m_goalkeeperFieldPlayerHelpImage;

        internal Sprite MoveHelpImage => m_moveHelpImage;
        internal Sprite PassHelpImage => m_passHelpImage;
        internal Sprite LongPassHelpImage => m_longPassHelpImage;
        internal Sprite ShotHelpImage => m_shotHelpImage;
        internal Sprite ConfrontationHelpImage => m_confrontationHelpImage;
        internal Sprite InterceptionHelpImage => m_interceptionHelpImage;
        internal Sprite GoalkeeperSaveHelpImage => m_goalkeeperSaveHelpImage;
        internal Sprite GoalkeeperOutOfAreaHelpImage => m_goalkeeperOutOfAreaHelpImage;
        internal Sprite GoalkeeperFieldPlayerHelpImage => m_goalkeeperFieldPlayerHelpImage;

        internal Texture2D GetCountryFlag(int countryId)
        {
            if (countryId <= 0)
                return null;
            foreach (var flag in m_countryFlags)
                if (flag.CountryId == countryId)
                    return flag.Texture;
            return null;
        }

        internal Sprite GetShirt(ShirtColorEnum color)
        {
            foreach (var shirt in m_shirts)
                if (shirt.Color == color)
                    return shirt.Sprite;
            return null;
        }

#if UNITY_EDITOR
        internal const string AssetPath = "Assets/ClientAssets/ClientImageCatalog.asset";

        internal static ClientImageCatalog LoadForEditor() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<ClientImageCatalog>(AssetPath);
#endif
    }
}
