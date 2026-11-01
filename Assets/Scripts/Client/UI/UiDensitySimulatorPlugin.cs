// SPDX-License-Identifier: AGPL-3.0-only
#if UNITY_EDITOR
using UnityEditor.DeviceSimulation;

namespace Imma.Foobbuteo.Client.UI
{
    public sealed class UiDensitySimulatorPlugin : DeviceSimulatorPlugin
    {
        public override string title => "";

        public override void OnCreate()
        {
            deviceSimulator.deviceChanged += UiDensityEditorEvents.RequestRefresh;
            UiDensityEditorEvents.RequestRefresh();
        }

        public override void OnDestroy()
        {
            deviceSimulator.deviceChanged -= UiDensityEditorEvents.RequestRefresh;
            UiDensityEditorEvents.RequestRefresh();
        }
    }
}
#endif
