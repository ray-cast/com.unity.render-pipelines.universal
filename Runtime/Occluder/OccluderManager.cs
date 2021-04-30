using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering.Universal
{
    public sealed class OccluderManager
    {
        internal static bool needIsolationFilteredByRenderer = false;

        static readonly Lazy<OccluderManager> s_Instance = new Lazy<OccluderManager>(() => new OccluderManager());

        public static OccluderManager instance => s_Instance.Value;

        public IEnumerable<Type> baseComponentTypes { get; private set; }

        // Max amount of layers available in Unity
        const int k_MaxLayerCount = 32;

        // Cached lists of all occluders (sorted by priority) by layer mask
        readonly Dictionary<int, List<Occluder>> m_SortedOccluders;

        // Holds all the registered occluders
        readonly List<Occluder> m_Occluders;

        // Keep track of sorting states for layer masks
        readonly Dictionary<int, bool> m_SortNeeded;

        public List<Occluder> occluders
		{
            get
			{
                return m_Occluders;
			}
		}

         OccluderManager()
        {
            m_SortedOccluders = new Dictionary<int, List<Occluder>>();
            m_Occluders = new List<Occluder>();
            m_SortNeeded = new Dictionary<int, bool>();
        }

        /// <summary>
        /// Registers a new Occluder in the manager. Unity does this automatically when a new Occluder is
        /// enabled, or its layer changes, but you can use this function to force-register a Occluder
        /// that is currently disabled.
        /// </summary>
        /// <param name="occluder">The occluder to register.</param>
        /// <param name="layer">The LayerMask that this occluder is in.</param>
        /// <seealso cref="Unregister"/>
        public void Register(Occluder occluder, int layer)
        {
            m_Occluders.Add(occluder);

            // Look for existing cached layer masks and add it there if needed
            foreach (var kvp in m_SortedOccluders)
            {
                // We add the occluder to sorted lists only if the layer match and if it doesn't contain the occluder already.
                if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(occluder))
                    kvp.Value.Add(occluder);
            }

            SetLayerDirty(layer);
        }

        /// <summary>
        /// Unregisters a Occluder from the manager. Unity does this automatically when a Occluder is
        /// disabled or goes out of scope, but you can use this function to force-unregister a Occluder
        /// that you added manually while it was disabled.
        /// </summary>
        /// <param name="occluder">The Occluder to unregister.</param>
        /// <param name="layer">The LayerMask that this Occluder is in.</param>
        /// <seealso cref="Register"/>
        public void Unregister(Occluder occluder, int layer)
        {
            m_Occluders.Remove(occluder);

            foreach (var kvp in m_SortedOccluders)
            {
                // Skip layer masks this occluder doesn't belong to
                if ((kvp.Key & (1 << layer)) == 0)
                    continue;

                kvp.Value.Remove(occluder);
            }
        }


        internal void SetLayerDirty(int layer)
        {
            Assert.IsTrue(layer >= 0 && layer <= k_MaxLayerCount, "Invalid layer bit");

            foreach (var kvp in m_SortedOccluders)
            {
                var mask = kvp.Key;

                if ((mask & (1 << layer)) != 0)
                    m_SortNeeded[mask] = true;
            }
        }

        internal void UpdateOccluderLayer(Occluder occluder, int prevLayer, int newLayer)
        {
            Assert.IsTrue(prevLayer >= 0 && prevLayer <= k_MaxLayerCount, "Invalid layer bit");
            Unregister(occluder, prevLayer);
            Register(occluder, newLayer);
        }

        List<Occluder> GrabOccluders(LayerMask mask)
        {
            List<Occluder> list;

            if (!m_SortedOccluders.TryGetValue(mask, out list))
            {
                // New layer mask detected, create a new list and cache all the occluders that belong
                // to this mask in it
                list = new List<Occluder>();

                foreach (var occluder in m_Occluders)
                {
                    if ((mask & (1 << occluder.gameObject.layer)) == 0)
                        continue;

                    list.Add(occluder);
                    m_SortNeeded[mask] = true;
                }

                m_SortedOccluders.Add(mask, list);
            }

            // Check sorting state
            bool sortNeeded;
            if (m_SortNeeded.TryGetValue(mask, out sortNeeded) && sortNeeded)
            {
                m_SortNeeded[mask] = false;
            }

            return list;
        }
    }

    /// <summary>
    /// A scope in which a Camera filters a Occluder.
    /// </summary>
    public struct OccluderIsolationScope : IDisposable
    {
        /// <summary>
        /// Constructs a scope in which a Camera filters a Occluder.
        /// </summary>
        /// <param name="unused">Unused parameter.</param>
        public OccluderIsolationScope(bool unused) => OccluderManager.needIsolationFilteredByRenderer = true;

        /// <summary>
        /// Stops the Camera from filtering a Occluder.
        /// </summary>
        void IDisposable.Dispose() => OccluderManager.needIsolationFilteredByRenderer = false;
    }
}