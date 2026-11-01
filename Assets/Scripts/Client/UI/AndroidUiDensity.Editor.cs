// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
namespace Imma.Foobbuteo.Client.UI
{
    public sealed partial class AndroidUiDensity
    {
        partial void SubscribeToDensityChanges() => UiDensityEditorEvents.Changed += RefreshDensity;

        partial void UnsubscribeFromDensityChanges() => UiDensityEditorEvents.Changed -= RefreshDensity;
    }
}
#endif
