// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Localization;
using NUnit.Framework;

namespace Tests.Editor.SharedAssembly
{
    public class CountryNameTranslatorTests
    {
        [TestCase("LANG_PORTUGAL", "Portugal")]
        [TestCase("LANG_ESPANHA", "Spain")]
        [TestCase("LANG_FRANCA", "France")]
        [TestCase("LANG_INGLATERRA", "England")]
        [TestCase("LANG_HOLANDA", "Netherlands")]
        [TestCase("LANG_ITALIA", "Italy")]
        [TestCase("LANG_BELGICA", "Belgium")]
        [TestCase("LANG_RUSSIA", "Russia")]
        [TestCase("LANG_ANGOLA", "Angola")]
        [TestCase("LANG_ALEMANHA", "Germany")]
        [TestCase("LANG_BRASIL", "Brazil")]
        [TestCase("LANG_ARGENTINA", "Argentina")]
        [TestCase("LANG_AFRICADOSUL", "South Africa")]
        [TestCase("LANG_AUSTRALIA", "Australia")]
        [TestCase("LANG_USA", "USA")]
        [TestCase("LANG_HUNGRIA", "Hungary")]
        [TestCase("LANG_CANADA", "Canada")]
        [TestCase("LANG_CHINA", "China")]
        [TestCase("LANG_CROACIA", "Croatia")]
        [TestCase("LANG_EGIPTO", "Egypt")]
        [TestCase("LANG_FINLANDIA", "Finland")]
        [TestCase("LANG_GRECIA", "Greece")]
        [TestCase("LANG_IRLANDA", "Ireland")]
        [TestCase("LANG_ISRAEL", "Israel")]
        [TestCase("LANG_JAPAO", "Japan")]
        [TestCase("LANG_JUGOSLAVIA", "Serbia")]
        [TestCase("LANG_SUICA", "Switzerland")]
        [TestCase("LANG_MEXICO", "Mexico")]
        [TestCase("LANG_NIGERIA", "Nigeria")]
        [TestCase("LANG_ESCOCIA", "Scotland")]
        [TestCase("LANG_PERU", "Peru")]
        [TestCase("LANG_SUECIA", "Sweden")]
        [TestCase("LANG_ROMENIA", "Romania")]
        [TestCase("LANG_ESLOVENIA", "Slovenia")]
        [TestCase("LANG_BOLIVIA", "Bolivia")]
        [TestCase("LANG_POLONIA", "Poland")]
        [TestCase("LANG_URUGUAY", "Uruguay")]
        [TestCase("LANG_CHILE", "Chile")]
        [TestCase("LANG_COLOMBIA", "Colombia")]
        [TestCase("LANG_DINAMARCA", "Denmark")]
        [TestCase("LANG_BOSNIA", "Bosnia")]
        [TestCase("LANG_AUSTRIA", "Austria")]
        [TestCase("LANG_NORUEGA", "Norway")]
        [TestCase("LANG_ESTONIA", "Estonia")]
        [TestCase("LANG_TURQUIA", "Turkey")]
        [TestCase("LANG_TAILANDIA", "Thailand")]
        [TestCase("LANG_VENEZUELA", "Venezuela")]
        [TestCase("LANG_INDONESIA", "Indonesia")]
        [TestCase("LANG_LITUANIA", "Lithuania")]
        [TestCase("LANG_BULGARIA", "Bulgaria")]
        [TestCase("LANG_LETONIA", "Latvia")]
        [TestCase("LANG_MALASIA", "Malaysia")]
        [TestCase("LANG_SINGAPURA", "Singapore")]
        [TestCase("LANG_REPCHECA", "Czech Republic")]
        [TestCase("LANG_ESLOVAQUIA", "Slovakia")]
        [TestCase("LANG_NOVAZELANDIA", "New Zealand")]
        [TestCase("LANG_MALTA", "Malta")]
        [TestCase("LANG_ILHASFAROE", "Faroe Islands")]
        [TestCase("LANG_ISLANDIA", "Iceland")]
        [TestCase("LANG_GALES", "Wales")]
        [TestCase("LANG_UCRANIA", "Ukraine")]
        [TestCase("LANG_PARAGUAY", "Paraguay")]
        [TestCase("LANG_PANAMA", "Panama")]
        [TestCase("LANG_FILIPINAS", "Philippines")]
        [TestCase("LANG_LXBURGO", "Luxembourg")]
        [TestCase("LANG_HKONG", "Hong Kong")]
        [TestCase("LANG_JORDAN", "Jordan")]
        [TestCase("LANG_MLDVA", "Moldova")]
        [TestCase("LANG_BLRUS", "Belarus")]
        [TestCase("LANG_CHIPRE", "Cyprus")]
        [TestCase("LANG_QATAR", "Qatar")]
        [TestCase("LANG_INDIA", "India")]
        [TestCase("LANG_ARABSAU", "Saudi Arabia")]
        [TestCase("LANG_KUWAIT", "Kuwait")]
        [TestCase("LANG_UAE", "United Arab Emirates")]
        [TestCase("LANG_IRAO", "Iran")]
        [TestCase("LANG_KOREIAS", "South Korea")]
        [TestCase("LANG_PAQUISTAO", "Pakistan")]
        [TestCase("LANG_ALGERIA", "Algeria")]
        [TestCase("LANG_MARROCOS", "Morocco")]
        [TestCase("LANG_TUNISIA", "Tunisia")]
        [TestCase("LANG_FYROM", "North Macedonia")]
        [TestCase("LANG_IRLANDAN", "Northern Ireland")]
        [TestCase("LANG_SMARINO", "San Marino")]
        [TestCase("LANG_GEORGIA", "Georgia")]
        [TestCase("LANG_MONTENEGRO", "Montenegro")]
        [TestCase("LANG_KAZAK", "Kazakhstan")]
        public void Translate_KnownTag_ReturnsSuppliedEnglishName(string tag, string expected)
        {
            Assert.AreEqual(
                expected,
                CountryNameTranslator.Translate(tag),
                $"Country tag {tag} must use its supplied English name by default."
            );
        }

        [TestCase("en")]
        [TestCase("pl")]
        [TestCase("")]
        [TestCase(null)]
        public void Translate_Language_UsesEnglishOrFallsBackToEnglish(string language)
        {
            Assert.AreEqual(
                "Portugal",
                CountryNameTranslator.Translate("LANG_PORTUGAL", language),
                $"Language '{language}' must resolve the known country tag using English."
            );
        }

        [TestCase("LANG_UNKNOWN")]
        [TestCase("lang_portugal")]
        [TestCase(" LANG_PORTUGAL")]
        [TestCase("Portugal")]
        [TestCase("")]
        [TestCase(null)]
        public void Translate_UnrecognizedInput_PreservesOriginal(string tag)
        {
            Assert.AreEqual(
                tag,
                CountryNameTranslator.Translate(tag),
                "Unknown tags, plain names, null and empty values must remain unchanged."
            );
        }
    }
}
