using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering.Universal
{
    public sealed class CapsuleOccluderManager
    {
        const int k_MaxLayerCount = 32;

        internal static bool needIsolationFilteredByRenderer = false;

        static readonly Lazy<CapsuleOccluderManager> s_Instance = new Lazy<CapsuleOccluderManager>(() => new CapsuleOccluderManager());

        public static CapsuleOccluderManager instance => s_Instance.Value;

        public IEnumerable<Type> baseComponentTypes { get; private set; }

        readonly Dictionary<int, List<CapsuleOccluder>> _sortedOccluders;
        readonly List<CapsuleOccluder> _occluders;
        readonly Dictionary<int, bool> _sortNeeded;

        public List<CapsuleOccluder> occluders
		{
            get
			{
                return _occluders;
			}
		}

        CapsuleOccluderManager()
        {
            _sortedOccluders = new Dictionary<int, List<CapsuleOccluder>>();
            _occluders = new List<CapsuleOccluder>();
            _sortNeeded = new Dictionary<int, bool>();
        }

        public void Register(CapsuleOccluder occluder, int layer)
        {
            _occluders.Add(occluder);

            foreach (var kvp in _sortedOccluders)
            {
                if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(occluder))
                    kvp.Value.Add(occluder);
            }

            SetLayerDirty(layer);
        }

        public void Unregister(CapsuleOccluder occluder, int layer)
        {
            _occluders.Remove(occluder);

            foreach (var kvp in _sortedOccluders)
            {
                if ((kvp.Key & (1 << layer)) == 0)
                    continue;

                kvp.Value.Remove(occluder);
            }
        }


        internal void SetLayerDirty(int layer)
        {
            Assert.IsTrue(layer >= 0 && layer <= k_MaxLayerCount, "Invalid layer bit");

            foreach (var kvp in _sortedOccluders)
            {
                var mask = kvp.Key;

                if ((mask & (1 << layer)) != 0)
                    _sortNeeded[mask] = true;
            }
        }

        internal void UpdateOccluderLayer(CapsuleOccluder occluder, int prevLayer, int newLayer)
        {
            Assert.IsTrue(prevLayer >= 0 && prevLayer <= k_MaxLayerCount, "Invalid layer bit");
            Unregister(occluder, prevLayer);
            Register(occluder, newLayer);
        }
    }
}