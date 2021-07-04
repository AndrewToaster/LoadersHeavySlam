using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace HeavySlam
{
    internal static class Helpers
    {
        internal static float MapRange(this float value, float baseStart, float baseEnd, float targetStart, float targetEnd)
        {
            return ((value - baseStart) / (baseEnd - baseStart) * (targetEnd - targetStart)) + targetStart;
        }

        internal static bool TryPopValue<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, out TValue value) where TKey : class where TValue : class
        {
            if (table.TryGetValue(key, out TValue val))
            {
                value = val;
                table.Remove(key);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        internal class WeakTable<TKey, TValue> where TKey : class where TValue : struct
        {
            private readonly ConditionalWeakTable<TKey, Tuple<TValue>> _table;

            public WeakTable()
            {
                _table = new ConditionalWeakTable<TKey, Tuple<TValue>>();
            }

            public bool TryGetValue(TKey key, out TValue value)
            {
                if (_table.TryGetValue(key, out Tuple<TValue> val))
                {
                    value = val.Item1;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }

            public bool TryGetValue(TKey key, out TValue value, TValue defaultValue)
            {
                if (_table.TryGetValue(key, out Tuple<TValue> val))
                {
                    value = val.Item1;
                    return true;
                }
                else
                {
                    value = defaultValue;
                    return false;
                }
            }

            public bool TryPopValue(TKey key, out TValue value)
            {
                if (_table.TryPopValue(key, out Tuple<TValue> val))
                {
                    value = val.Item1;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }

            public void Add(TKey key, TValue value)
            {
                _table.Add(key, Tuple.Create(value));
            }

            public bool Remove(TKey key)
            {
                return _table.Remove(key);
            }

            public bool Contains(TKey key)
            {
                return _table.TryGetValue(key, out Tuple<TValue> _);
            }

            public static implicit operator ConditionalWeakTable<TKey, Tuple<TValue>>(WeakTable<TKey, TValue> table)
            {
                return table._table;
            }
        }
    }
}
