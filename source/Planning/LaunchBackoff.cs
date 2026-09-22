using System;
using System.Collections.Generic;

namespace HungryPathing.Planning
{
    // Storages that recently failed to launch a trip, each left alone until its own deadline. A beaver keeps one of
    // these. It is only ever asked whether a storage is in it, and entries are kept in the order they failed, so no
    // answer depends on a hash order. Keys are compared by reference, as the game's components are. When it is full
    // the oldest failure is forgotten first, which costs no more than one repeat attempt at that storage.
    public sealed class LaunchBackoff<T> where T : class
    {
        private readonly List<T> _keys = new List<T>();
        private readonly List<float> _untilHours = new List<float>();
        private readonly int _capacity;

        public LaunchBackoff(int capacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        public int Count => _keys.Count;

        // key failed at hour now: leave it alone until untilHours. A key already in the list moves to the newest end
        // and keeps the later of its two deadlines.
        public void Add(T key, float now, float untilHours)
        {
            Prune(now);
            int at = IndexOf(key);
            if (at >= 0)
            {
                untilHours = Math.Max(untilHours, _untilHours[at]);
                RemoveAt(at);
            }
            if (_keys.Count >= _capacity)
            {
                RemoveAt(0);
            }
            _keys.Add(key);
            _untilHours.Add(untilHours);
        }

        public bool IsBackedOff(T key, float now)
        {
            int at = IndexOf(key);
            return at >= 0 && FuelPlanner.StillBackedOff(now, _untilHours[at]);
        }

        // Forgets every entry whose deadline has passed and keeps the rest in order.
        public void Prune(float now)
        {
            int kept = 0;
            for (int i = 0; i < _keys.Count; i++)
            {
                if (!FuelPlanner.StillBackedOff(now, _untilHours[i]))
                {
                    continue;
                }
                _keys[kept] = _keys[i];
                _untilHours[kept] = _untilHours[i];
                kept++;
            }
            int expired = _keys.Count - kept;
            if (expired > 0)
            {
                _keys.RemoveRange(kept, expired);
                _untilHours.RemoveRange(kept, expired);
            }
        }

        private int IndexOf(T key)
        {
            for (int i = 0; i < _keys.Count; i++)
            {
                if (ReferenceEquals(_keys[i], key))
                {
                    return i;
                }
            }
            return -1;
        }

        private void RemoveAt(int at)
        {
            _keys.RemoveAt(at);
            _untilHours.RemoveAt(at);
        }
    }
}
