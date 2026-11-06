// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
using Imma.Foobbuteo.Client.UI;

namespace Imma.Foobbuteo.Client.Game.UI
{
    public sealed partial class GamePanelSettingsRuntime
    {
        partial void SubscribeToDensityChanges() => UiDensityEditorEvents.Changed += RefreshDensity;

        partial void UnsubscribeFromDensityChanges() => UiDensityEditorEvents.Changed -= RefreshDensity;
    }
}
#endif
