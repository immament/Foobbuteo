// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using Imma.Common;
using UnityEngine;

namespace Imma.Foobbuteo.Common
{
    public abstract class BaseMonoBehaviour : MonoBehaviour
    {
        [SerializeField]
        protected bool m_debug;

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
            this.LogTrace("**");
        }

        protected void OnEnable()
        {
            this.LogTrace("**");
        }

        protected void Start()
        {
            this.LogTrace("**");
        }

        protected void OnDisable()
        {
            this.LogTrace("**");
        }

        protected void OnDestroy()
        {
            this.LogTrace("**");
        }

        protected void OnDestroying()
        {
            this.LogTrace("**");
        }
    }
}
