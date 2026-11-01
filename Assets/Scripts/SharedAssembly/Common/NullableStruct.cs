// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using UnityEngine;

namespace Imma.Common
{
    [Serializable]
    public struct NullableStruct<T>
        where T : struct
    {
        [SerializeField]
        private bool hasValue;

        [SerializeField]
        private T value;

        public NullableStruct(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        // public static NullableStruct<T> WithoutValue()
        // {
        //     return new NullableStruct<T>();
        // }

        public bool HasValue
        {
            get { return hasValue; }
        }

        public T Value
        {
            get
            {
                if (!hasValue)
                {
                    throw new AppException("Nullable object must have a value");
                }
                return value;
            }
        }

        public T? NullableValue => hasValue ? value : null;

        public T GetValueOrDefault()
        {
            return value;
        }

        public T GetValueOrDefault(T defaultValue)
        {
            return hasValue ? value : defaultValue;
        }

        public override bool Equals(object other)
        {
            if (!hasValue)
                return other == null;
            if (other == null)
                return false;
            return value.Equals(other);
        }

        public override int GetHashCode()
        {
            return hasValue ? value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return hasValue ? value.ToString() : "";
        }

        public static implicit operator NullableStruct<T>(T value)
        {
            return new NullableStruct<T>(value);
        }

        public static explicit operator T(NullableStruct<T> value)
        {
            return value.Value;
        }

        public static implicit operator NullableStruct<T>(T? value)
        {
            if (value.HasValue)
                return new NullableStruct<T>(value.Value);
            return new NullableStruct<T>();
        }
    }
}
