#if UNITY_ANDROID
// SPDX-License-Identifier: AGPL-3.0-only
namespace Imma.Foobbuteo.Client
{
    internal sealed class ClientEnvironmentConfig
    {
        public string Environment { get; }
        public bool UiLayoutDiagnostics { get; }

        public ClientEnvironmentConfig(string environment, bool uiLayoutDiagnostics = false)
        {
            Environment = environment;
            UiLayoutDiagnostics = uiLayoutDiagnostics;
        }
    }
}
#endif
