// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Common;
using UnityEngine;

namespace Imma.Foobbuteo.Common.dev
{
    public class DevMonoBehaviour : MonoBehaviour
    {
        private void Awake()
        {
            this.LogDebug("++");
        }

        private void OnEnable()
        {
            this.LogDebug("++");
        }

        private void OnDisable()
        {
            this.LogDebug("++");
        }

        private void OnDestroy()
        {
            this.LogDebug("++");
        }

        private void OnDestroying()
        {
            this.LogDebug("++");
        }

        private void Start()
        {
            this.LogDebug("++");
        }
    }
}
