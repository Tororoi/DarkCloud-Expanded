using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The hit entries one ability has planted in the collision pool (<see cref="CollisionPool"/>), each with the ticks
    /// of the owner's loop it may stay: <see cref="Expire"/> once a tick withdraws those whose time is up (the engine withdraws its
    /// own swing spheres when the swing ends; a planted one that nothing consumed is withdrawn here), <see cref="WithdrawAll"/> takes
    /// every one back at once when the ability stops. Two options, fixed per instance:
    ///  · <c>clearMark</c> — the entries carry a no-drain or crush mark (+0x9C), zeroed as they go (Set__CCollisionData never
    ///    writes that word, so a mark left behind would ride into the entry's next use);
    ///  · <c>retireSpent</c> — an entry the engine has already consumed (no longer active) is done at once, mark and all, rather
    ///    than waiting out its ticks.</summary>
    internal sealed class PlantedHits
    {
        private readonly List<(int slot, int ticks)> _hits = new();
        private readonly bool _clearMark, _retireSpent;

        internal PlantedHits(bool clearMark = false, bool retireSpent = false) { _clearMark = clearMark; _retireSpent = retireSpent; }

        internal int Count => _hits.Count;

        /// <summary>Entry <paramref name="slot"/> just planted, to stay <paramref name="ticks"/> ticks.</summary>
        internal void Add(int slot, int ticks) => _hits.Add((slot, ticks));

        /// <summary>One tick: each entry's ticks counted down; at zero (or, with retireSpent, once the engine has consumed it) it
        /// is withdrawn and forgotten.</summary>
        internal void Expire()
        {
            if (_hits.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = _hits[i];
                bool spent = _retireSpent && pool != 0 && !CollisionPool.IsActive(pool, slot);
                if (--ticks > 0 && !spent) { _hits[i] = (slot, ticks); continue; }
                CollisionPool.Withdraw(pool, slot, _clearMark);
                _hits.RemoveAt(i);
            }
        }

        /// <summary>Every entry withdrawn now and the list emptied.</summary>
        internal void WithdrawAll()
        {
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in _hits) CollisionPool.Withdraw(pool, slot, _clearMark);
            _hits.Clear();
        }
    }
}
