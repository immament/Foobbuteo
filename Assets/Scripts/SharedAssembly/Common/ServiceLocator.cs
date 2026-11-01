// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;

namespace Imma.Foobbuteo.Common
{
    public class ServiceLocator
    {
        private static ServiceLocator _current;
        public static ServiceLocator Current => _current ??= new ServiceLocator();

        private readonly Dictionary<Type, IDisposable> _services = new();

        public static void Initialize() => _current = new ServiceLocator();

        public static void ClearAll()
        {
            if (_current != null)
            {
                _current.Clear();
            }
        }

        public void Clear()
        {
            foreach (var item in _services)
            {
                item.Value.Dispose();
            }
            _services.Clear();
        }

        public T Register<T>(T service, bool throwIfRegistered = true)
            where T : IDisposable
        {
            var type = typeof(T);

            if (throwIfRegistered && _services.ContainsKey(type))
            {
                throw new AppException($"Service of type {type} is already registered.");
            }
            _services[type] = service;
            return service;
        }

        public T Get<T>()
            where T : IDisposable
        {
            var type = typeof(T);
            if (!_services.TryGetValue(type, out var service))
            {
                throw new AppException($"Service of type {type} is not registered.");
            }
            return (T)service;
        }

        public T GetOrDefault<T>()
            where T : IDisposable
        {
            var type = typeof(T);
            if (!_services.TryGetValue(type, out var service)) { }
            return (T)_services.GetValueOrDefault(type);
        }

        public T GetOrRegister<T>(Func<T> creatorMethod)
            where T : IDisposable
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var existingService))
            {
                return (T)existingService;
            }

            if (creatorMethod == null)
            {
                throw new ArgumentNullException(
                    nameof(creatorMethod),
                    $"Service of type {type} is not registered, and no creator method was provided."
                );
            }

            T newService = creatorMethod();
            _services[type] = newService;

            return newService;
        }

        public void Unregister<T>()
            where T : IDisposable
        {
            if (_services.Remove(typeof(T), out var value))
            {
                value.Dispose();
            }
        }
    }
}
