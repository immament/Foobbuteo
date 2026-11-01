// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;

namespace Imma.Foobbuteo.Shared.Localization
{
    public static class CountryNameTranslator
    {
        private static readonly Dictionary<string, string> EnglishNames = new(StringComparer.Ordinal)
        {
            ["LANG_PORTUGAL"] = "Portugal",
            ["LANG_ESPANHA"] = "Spain",
            ["LANG_FRANCA"] = "France",
            ["LANG_INGLATERRA"] = "England",
            ["LANG_HOLANDA"] = "Netherlands",
            ["LANG_ITALIA"] = "Italy",
            ["LANG_BELGICA"] = "Belgium",
            ["LANG_RUSSIA"] = "Russia",
            ["LANG_ANGOLA"] = "Angola",
            ["LANG_ALEMANHA"] = "Germany",
            ["LANG_BRASIL"] = "Brazil",
            ["LANG_ARGENTINA"] = "Argentina",
            ["LANG_AFRICADOSUL"] = "South Africa",
            ["LANG_AUSTRALIA"] = "Australia",
            ["LANG_USA"] = "USA",
            ["LANG_HUNGRIA"] = "Hungary",
            ["LANG_CANADA"] = "Canada",
            ["LANG_CHINA"] = "China",
            ["LANG_CROACIA"] = "Croatia",
            ["LANG_EGIPTO"] = "Egypt",
            ["LANG_FINLANDIA"] = "Finland",
            ["LANG_GRECIA"] = "Greece",
            ["LANG_IRLANDA"] = "Ireland",
            ["LANG_ISRAEL"] = "Israel",
            ["LANG_JAPAO"] = "Japan",
            ["LANG_JUGOSLAVIA"] = "Serbia",
            ["LANG_SUICA"] = "Switzerland",
            ["LANG_MEXICO"] = "Mexico",
            ["LANG_NIGERIA"] = "Nigeria",
            ["LANG_ESCOCIA"] = "Scotland",
            ["LANG_PERU"] = "Peru",
            ["LANG_SUECIA"] = "Sweden",
            ["LANG_ROMENIA"] = "Romania",
            ["LANG_ESLOVENIA"] = "Slovenia",
            ["LANG_BOLIVIA"] = "Bolivia",
            ["LANG_POLONIA"] = "Poland",
            ["LANG_URUGUAY"] = "Uruguay",
            ["LANG_CHILE"] = "Chile",
            ["LANG_COLOMBIA"] = "Colombia",
            ["LANG_DINAMARCA"] = "Denmark",
            ["LANG_BOSNIA"] = "Bosnia",
            ["LANG_AUSTRIA"] = "Austria",
            ["LANG_NORUEGA"] = "Norway",
            ["LANG_ESTONIA"] = "Estonia",
            ["LANG_TURQUIA"] = "Turkey",
            ["LANG_TAILANDIA"] = "Thailand",
            ["LANG_VENEZUELA"] = "Venezuela",
            ["LANG_INDONESIA"] = "Indonesia",
            ["LANG_LITUANIA"] = "Lithuania",
            ["LANG_BULGARIA"] = "Bulgaria",
            ["LANG_LETONIA"] = "Latvia",
            ["LANG_MALASIA"] = "Malaysia",
            ["LANG_SINGAPURA"] = "Singapore",
            ["LANG_REPCHECA"] = "Czech Republic",
            ["LANG_ESLOVAQUIA"] = "Slovakia",
            ["LANG_NOVAZELANDIA"] = "New Zealand",
            ["LANG_MALTA"] = "Malta",
            ["LANG_ILHASFAROE"] = "Faroe Islands",
            ["LANG_ISLANDIA"] = "Iceland",
            ["LANG_GALES"] = "Wales",
            ["LANG_UCRANIA"] = "Ukraine",
            ["LANG_PARAGUAY"] = "Paraguay",
            ["LANG_PANAMA"] = "Panama",
            ["LANG_FILIPINAS"] = "Philippines",
            ["LANG_LXBURGO"] = "Luxembourg",
            ["LANG_HKONG"] = "Hong Kong",
            ["LANG_JORDAN"] = "Jordan",
            ["LANG_MLDVA"] = "Moldova",
            ["LANG_BLRUS"] = "Belarus",
            ["LANG_CHIPRE"] = "Cyprus",
            ["LANG_QATAR"] = "Qatar",
            ["LANG_INDIA"] = "India",
            ["LANG_ARABSAU"] = "Saudi Arabia",
            ["LANG_KUWAIT"] = "Kuwait",
            ["LANG_UAE"] = "United Arab Emirates",
            ["LANG_IRAO"] = "Iran",
            ["LANG_KOREIAS"] = "South Korea",
            ["LANG_PAQUISTAO"] = "Pakistan",
            ["LANG_ALGERIA"] = "Algeria",
            ["LANG_MARROCOS"] = "Morocco",
            ["LANG_TUNISIA"] = "Tunisia",
            ["LANG_FYROM"] = "North Macedonia",
            ["LANG_IRLANDAN"] = "Northern Ireland",
            ["LANG_SMARINO"] = "San Marino",
            ["LANG_GEORGIA"] = "Georgia",
            ["LANG_MONTENEGRO"] = "Montenegro",
            ["LANG_KAZAK"] = "Kazakhstan",
        };

        public static string Translate(string tag, string language = "en")
        {
            // English is the fallback until additional languages are supported.
            return tag != null && EnglishNames.TryGetValue(tag, out string name) ? name : tag;
        }
    }
}
