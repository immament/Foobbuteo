// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

#if UNITY_EDITOR
using UnityEngine;

public static class DeviceSimulatorUtility
{
    public static bool IsRunningInDeviceSimulator()
    {
        return UnityEngine.Device.SystemInfo.deviceType != DeviceType.Desktop;
    }
}
#endif
