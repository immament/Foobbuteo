#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Imma.Foobbuteo.Common.dev
{
    [Serializable]
    public sealed class DevAuthConfig
    {
        public string hostProfile = "host";
        public string clientProfile = "client";
        public List<DevAuthUser> users = new();
    }
}
#endif
