// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using Imma.Foobbuteo.Common.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Common
{
    public abstract class BaseNetworkBehaviour : NetworkBehaviour
    {
        [SerializeField]
        protected bool m_debug;
        public bool Debug => m_debug;

        [SerializeField]
        protected NullableLogLevel m_logLevel;

        [SerializeField]
        protected string[] m_logCategories = Array.Empty<string>();

        [SerializeField]
        protected Color m_logClassColor;

        [SerializeField]
        protected Color m_logUnityObjColor;

        protected void Awake()
        {
            this.SetLoggerOptions(m_logLevel, m_logClassColor, m_logUnityObjColor, m_logCategories);
            this.LogTrace(() => $"** {this.InfoTag()}");
        }

        void OnEnable()
        {
            this.LogTrace(() => $"** {this.InfoTag()}");
        }

        void Start()
        {
            this.LogTrace(() => $"** {this.InfoTag()}");
        }

        protected void OnDisable()
        {
            this.LogTrace(() => $"** {this.InfoTag()}");
        }

        public override void OnDestroy()
        {
            this.LogTrace(() => $"** {this.InfoTag()}");
            base.OnDestroy();
        }

        void OnDestroying()
        {
            this.LogTrace(() => $"** {this.InfoTag()} / {this.GetType()}");
            // ImmLogger.RemoveType(this.GetType());
        }
    }
}
