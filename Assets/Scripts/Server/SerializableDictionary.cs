// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Imma.Foobbuteo.Server
{
#if UNITY_EDITOR

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private bool m_refresh = true;
        private readonly bool m_editorReadonly;

        public SerializableDictionary(bool editorReadonly = true)
        {
            m_editorReadonly = editorReadonly;
        }

        // [SerializeField]
        // private List<TKey> m_keys = new List<TKey>();

        // [SerializeField]
        // private List<TValue> m_values = new List<TValue>();

        [SerializeField]
        private List<SerializableKeyValuePair<TKey, TValue>> m_items = new();

        // private Dictionary<TKey, TValue> m_dictionary;
        // private Dictionary<TKey, TValue> dictionary => new Dictionary<TKey, TValue>();

        public void OnBeforeSerialize()
        {
            if (!m_refresh)
                return;
            m_refresh = false;
            // Debug.Log("OnBeforeSerialize");
            // m_keys.Clear();
            // m_values.Clear();
            m_items.Clear();

            foreach (var kvp in this)
            {
                // m_keys.Add(kvp.Key);
                // m_values.Add(kvp.Value);
                m_items.Add(kvp);
            }
        }

        public void OnAfterDeserialize()
        {
            // Debug.Log("OnAfterDeserialize");
            if (m_editorReadonly)
                return;
            // m_dictionary = new Dictionary<TKey, TValue>();

            for (int i = 0; i < m_items.Count; i++)
            {
                this[m_items[i].Key] = m_items[i].Value;
            }

            // for (int i = 0; i < m_keys.Count && i < m_values.Count; i++)
            // {
            //     this[m_keys[i]] = m_values[i];
            // }
        }
    }

    [Serializable]
    public struct SerializableKeyValuePair<TKey, TValue>
    {
        [SerializeField]
        private TKey key;

        [SerializeField]
        private TValue value;

        public SerializableKeyValuePair(TKey key, TValue value)
        {
            this.key = key;
            this.value = value;
        }

        public TKey Key
        {
            get { return key; }
        }

        public TValue Value
        {
            get { return value; }
        }

        public static implicit operator SerializableKeyValuePair<TKey, TValue>(KeyValuePair<TKey, TValue> pair)
        {
            return new(pair.Key, pair.Value);
        }

        // public override string ToString()
        // {
        //     StringBuilder s = StringBuilderCache.Acquire();
        //     s.Append('[');
        //     if (Key != null)
        //     {
        //         s.Append(Key.ToString());
        //     }
        //     s.Append(", ");
        //     if (Value != null)
        //     {
        //         s.Append(Value.ToString());
        //     }
        //     s.Append(']');
        //     return StringBuilderCache.GetStringAndRelease(s);
        // }
    }
#else

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue> { }

#endif
}
