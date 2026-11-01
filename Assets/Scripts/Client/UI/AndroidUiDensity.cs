// SPDX-License-Identifier: AGPL-3.0-only
using UnityEngine;
using UnityEngine.UIElements;

namespace Imma.Foobbuteo.Client.UI
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed partial class AndroidUiDensity : MonoBehaviour
    {
        internal const float ReferenceDpi = 160f;

        internal static void RefreshSettings(PanelSettings settings, PanelSettings originalSettings) =>
            RefreshSettings(
                settings,
                originalSettings,
                UnityEngine.Device.Application.platform,
                Application.isEditor,
                UnityEngine.Device.Screen.dpi
            );

        internal static void RefreshSettings(
            PanelSettings settings,
            PanelSettings originalSettings,
            RuntimePlatform platform,
            bool isEditor,
            float dpi
        )
        {
            if (settings == null || TryApplySettings(settings, platform, isEditor, dpi) || originalSettings == null)
                return;

            settings.scaleMode = originalSettings.scaleMode;
            settings.referenceDpi = originalSettings.referenceDpi;
            settings.fallbackDpi = originalSettings.fallbackDpi;
            settings.scale = originalSettings.scale;
        }

        internal static bool TryApplySettings(
            PanelSettings settings,
            RuntimePlatform platform,
            bool isEditor,
            float dpi = ReferenceDpi
        )
        {
            if (platform != RuntimePlatform.Android || settings == null)
                return false;

            // Explicit simulator scaling avoids UI Toolkit using the host monitor's DPI.
            settings.scaleMode = isEditor ? PanelScaleMode.ConstantPixelSize : PanelScaleMode.ConstantPhysicalSize;
            settings.referenceDpi = ReferenceDpi;
            settings.fallbackDpi = ReferenceDpi;
            float effectiveDpi = dpi > 0f && !float.IsInfinity(dpi) ? dpi : ReferenceDpi;
            settings.scale = isEditor ? effectiveDpi / ReferenceDpi : 1f;
            return true;
        }
    }
}
