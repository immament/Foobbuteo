// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    static class RegisterGlobalConverters
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        public static void Register()
        {
            // Debug.Log("++RegisterGlobalConverters++");
            ConverterGroups.RegisterGlobalConverter(
                (ref bool visible) =>
                {
                    // Debug.Log($"Converter Bool [{visible}] to DisplayStyle");
                    return (StyleEnum<DisplayStyle>)(visible ? DisplayStyle.Flex : DisplayStyle.None);
                }
            );
            ConverterGroups.RegisterGlobalConverter(
                (ref int visible) =>
                {
                    return (StyleEnum<DisplayStyle>)(visible > 0 ? DisplayStyle.Flex : DisplayStyle.None);
                }
            );
            ConverterGroups.RegisterGlobalConverter(
                (ref bool visible) =>
                {
                    // Debug.Log("Converter Bool to Visibility");
                    return (StyleEnum<Visibility>)(visible ? Visibility.Visible : Visibility.Hidden);
                }
            );
        }
    }
}
