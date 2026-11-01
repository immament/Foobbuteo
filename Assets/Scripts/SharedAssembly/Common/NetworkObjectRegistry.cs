// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using Imma.Common;

namespace Imma.Foobbuteo.Common
{
    public interface INetworkBehaviour
    {
        public bool IsSpawned { get; }
    }

    public class NetworkObjectRegistry : IDisposable
    {
        private readonly Dictionary<Type, INetworkBehaviour> _activeInstances = new();

        // Explicit multicast delegate storage per Type
        private readonly Dictionary<Type, Action<INetworkBehaviour>> _onSpawnedEvents = new();
        private readonly Dictionary<Type, Action> _onDespawnedEvents = new();

        // Call this when a network object spawns
        public void Register<T>(T instance)
            where T : INetworkBehaviour
        {
            var type = typeof(T);
            if (instance == null)
            {
                throw new AppException($"Can not register NULL instance. Type {type}");
            }
            this.LDebug(() => $"{instance}");
            if (_activeInstances.ContainsKey(type))
            {
                throw new AppException($"INetworkBehaviour of type {type} is already registered.");
            }
            _activeInstances[type] = instance;

            if (_onSpawnedEvents.TryGetValue(type, out var spawnEvent))
            {
                spawnEvent?.Invoke(instance);
            }
        }

        public void Unregister<T>()
            where T : INetworkBehaviour
        {
            var type = typeof(T);
            this.LDebug(type.Name);
            if (_activeInstances.Remove(type))
            {
                if (_onDespawnedEvents.TryGetValue(type, out var despawnEvent))
                {
                    despawnEvent?.Invoke();
                }
            }
        }

        public bool Unregister<T>(T instance)
            where T : INetworkBehaviour
        {
            if (!_activeInstances.TryGetValue(typeof(T), out var current) || !ReferenceEquals(current, instance))
                return false;
            Unregister<T>();
            return true;
        }

        public void SetActive<T>(T instance)
            where T : INetworkBehaviour
        {
            if (instance == null)
                throw new AppException($"Can not register NULL instance. Type {typeof(T)}");
            if (_activeInstances.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, instance))
                return;
            Unregister<T>();
            Register(instance);
        }

        /// <summary>
        /// Binds an observer to a network type lifecycle.
        /// Supports infinite concurrent observers. Dispose the returned token to unsubscribe.
        /// </summary>
        public IDisposable BindLifecycle<T>(Action<T> onSpawn, Action onDespawn)
            where T : INetworkBehaviour
        {
            var type = typeof(T);

            this.LDebug(type.Name);

            // 1. Create a concrete reference to the wrapper so C# can track it for removal later
            void spawnWrapper(INetworkBehaviour rawObj) => onSpawn?.Invoke((T)rawObj);

            // 2. Safely append to the multicast delegate chain
            if (!_onSpawnedEvents.ContainsKey(type))
                _onSpawnedEvents[type] = null;
            _onSpawnedEvents[type] += spawnWrapper;

            if (!_onDespawnedEvents.ContainsKey(type))
                _onDespawnedEvents[type] = null;
            _onDespawnedEvents[type] += onDespawn;

            // 3. Immediate check: If the object is already active, fire the spawn callback right away
            if (_activeInstances.TryGetValue(type, out var currentInstance))
            {
                if (currentInstance?.IsSpawned ?? false)
                    onSpawn?.Invoke((T)currentInstance);
            }

            // 4. Return a subscription token that knows EXACTLY how to unbind itself
            return new NetworkSubscriptionToken(() =>
            {
                if (_onSpawnedEvents.ContainsKey(type))
                    _onSpawnedEvents[type] -= spawnWrapper;
                // else this.LError($"_onSpawnedEvents {type}");
                if (_onDespawnedEvents.ContainsKey(type))
                    _onDespawnedEvents[type] -= onDespawn;
                // else this.LError($"_onDespawnedEvents {type}");
            });
        }

        /// <summary>
        /// A lightweight handle used by services to safely unsubscribe from network lifecycles.
        /// </summary>
        public class NetworkSubscriptionToken : IDisposable
        {
            private readonly Action _onDispose;
            private bool _isDisposed;

            public NetworkSubscriptionToken(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                this.LTrace(() => $"-- _isDisposed: {_isDisposed} --");
                if (_isDisposed)
                    return;
                _onDispose?.Invoke();
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            this.LTrace("--");
            _activeInstances.Clear();
            _onSpawnedEvents.Clear();
            _onDespawnedEvents.Clear();
        }
    }
}
