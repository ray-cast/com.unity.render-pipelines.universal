using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A global manager that tracks all the Switchers in the currently loaded Scenes and does all the
    /// interpolation work.
    /// </summary>
    public sealed class SwitcherManager
    {
        internal static bool needIsolationFilteredByRenderer = false;

        static readonly Lazy<SwitcherManager> s_Instance = new Lazy<SwitcherManager>(() => new SwitcherManager());

        /// <summary>
        /// The current singleton instance of <see cref="SwitcherManager"/>.
        /// </summary>
        public static SwitcherManager instance => s_Instance.Value;

        /// <summary>
        /// The current list of all available types that derive from <see cref="SwitcherComponent"/>.
        /// </summary>
        public IEnumerable<Type> baseComponentTypes { get; private set; }

        // Max amount of layers available in Unity
        const int k_MaxLayerCount = 32;

        // Cached lists of all switchers (sorted by priority) by layer mask
        readonly Dictionary<int, List<Switcher>> m_SortedSwitchers;

        // Holds all the registered switchers
        readonly List<Switcher> m_Switchers;

        // Keep track of sorting states for layer masks
        readonly Dictionary<int, bool> m_SortNeeded;

        // Recycled list used for volume traversal
        readonly List<Collider> m_TempColliders;

        SwitcherManager()
        {
            m_SortedSwitchers = new Dictionary<int, List<Switcher>>();
            m_Switchers = new List<Switcher>();
            m_SortNeeded = new Dictionary<int, bool>();
            m_TempColliders = new List<Collider>(8);
        }

        /// <summary>
        /// Registers a new Switcher in the manager. Unity does this automatically when a new Switcher is
        /// enabled, or its layer changes, but you can use this function to force-register a Switcher
        /// that is currently disabled.
        /// </summary>
        /// <param name="volume">The volume to register.</param>
        /// <param name="layer">The LayerMask that this volume is in.</param>
        /// <seealso cref="Unregister"/>
        public void Register(Switcher volume, int layer)
        {
            m_Switchers.Add(volume);

            // Look for existing cached layer masks and add it there if needed
            foreach (var kvp in m_SortedSwitchers)
            {
                // We add the volume to sorted lists only if the layer match and if it doesn't contain the volume already.
                if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(volume))
                    kvp.Value.Add(volume);
            }

            SetLayerDirty(layer);
        }

        /// <summary>
        /// Unregisters a Switcher from the manager. Unity does this automatically when a Switcher is
        /// disabled or goes out of scope, but you can use this function to force-unregister a Switcher
        /// that you added manually while it was disabled.
        /// </summary>
        /// <param name="volume">The Switcher to unregister.</param>
        /// <param name="layer">The LayerMask that this Switcher is in.</param>
        /// <seealso cref="Register"/>
        public void Unregister(Switcher volume, int layer)
        {
            m_Switchers.Remove(volume);

            foreach (var kvp in m_SortedSwitchers)
            {
                // Skip layer masks this volume doesn't belong to
                if ((kvp.Key & (1 << layer)) == 0)
                    continue;

                kvp.Value.Remove(volume);
            }
        }


        internal void SetLayerDirty(int layer)
        {
            Assert.IsTrue(layer >= 0 && layer <= k_MaxLayerCount, "Invalid layer bit");

            foreach (var kvp in m_SortedSwitchers)
            {
                var mask = kvp.Key;

                if ((mask & (1 << layer)) != 0)
                    m_SortNeeded[mask] = true;
            }
        }

        internal void UpdateSwitcherLayer(Switcher volume, int prevLayer, int newLayer)
        {
            Assert.IsTrue(prevLayer >= 0 && prevLayer <= k_MaxLayerCount, "Invalid layer bit");
            Unregister(volume, prevLayer);
            Register(volume, newLayer);
        }

        void OverrideData(List<SwitcherComponent> components, float interpFactor)
        {
            foreach (var component in components)
			{
                if (!component.active)
                    continue;

                component.Override(interpFactor);
            }
        }

        /// <summary>
        /// Updates the Switcher manager and stores the result in a custom <see cref="SwitcherStack"/>.
        /// </summary>
        /// <param name="stack">The stack to store the blending result into.</param>
        /// <param name="trigger">A reference Transform to consider for positional Switcher blending.
        /// </param>
        /// <param name="layerMask">The LayerMask that Unity uses to filter Switchers that it should consider
        /// for blending.</param>
        /// <seealso cref="SwitcherStack"/>
        public void Update(Transform trigger, LayerMask layerMask)
        {
            bool onlyGlobal = trigger == null;
            var triggerPos = onlyGlobal ? Vector3.zero : trigger.position;

            // Sort the cached volume list(s) for the given layer mask if needed and return it
            var switchers = GrabSwitchers(layerMask);

            Camera camera = null;
            // Behavior should be fine even if camera is null
            if (!onlyGlobal)
                trigger.TryGetComponent<Camera>(out camera);

#if UNITY_EDITOR
            // requested or prefab isolation mode.
            bool needIsolation = needIsolationFilteredByRenderer || (UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle() != UnityEditor.SceneManagement.StageUtility.GetMainStageHandle());
#endif

            // Traverse all switchers
            foreach (var switcher in switchers)
            {
#if UNITY_EDITOR
                // Skip switchers that aren't in the scene currently displayed in the scene view
                if (needIsolation
                    && !IsSwitcherRenderedByCamera(switcher, camera))
                    continue;
#endif

                // Skip disabled switchers and switchers without any data or weight
                if (!switcher.enabled || switcher.weight <= 0f)
                    continue;

                // Global switchers always have influence
                if (switcher.isGlobal)
                {
                    OverrideData(switcher.components, Mathf.Clamp01(switcher.weight));
                    continue;
                }

                if (onlyGlobal)
                    continue;

                // If volume isn't global and has no collider, skip it as it's useless
                var colliders = m_TempColliders;
                switcher.GetComponents(colliders);
                if (colliders.Count == 0)
                    continue;

                // Find closest distance to volume, 0 means it's inside it
                float closestDistanceSqr = float.PositiveInfinity;

                foreach (var collider in colliders)
                {
                    if (!collider.enabled)
                        continue;

                    var closestPoint = collider.ClosestPoint(triggerPos);
                    var d = (closestPoint - triggerPos).sqrMagnitude;

                    if (d < closestDistanceSqr)
                        closestDistanceSqr = d;
                }

                colliders.Clear();
                float blendDistSqr = switcher.blendDistance * switcher.blendDistance;

                // Switcher has no influence, ignore it
                // Note: Switcher doesn't do anything when `closestDistanceSqr = blendDistSqr` but we
                //       can't use a >= comparison as blendDistSqr could be set to 0 in which case
                //       volume would have total influence
                if (closestDistanceSqr > blendDistSqr)
                    continue;

                // Switcher has influence
                float interpFactor = 1f;
                if (blendDistSqr > 0f)
                    interpFactor = 1f - (closestDistanceSqr / blendDistSqr);

                OverrideData(switcher.components, interpFactor * Mathf.Clamp01(switcher.weight));
            }
        }

        List<Switcher> GrabSwitchers(LayerMask mask)
        {
            List<Switcher> list;

            if (!m_SortedSwitchers.TryGetValue(mask, out list))
            {
                // New layer mask detected, create a new list and cache all the switchers that belong
                // to this mask in it
                list = new List<Switcher>();

                foreach (var volume in m_Switchers)
                {
                    if ((mask & (1 << volume.gameObject.layer)) == 0)
                        continue;

                    list.Add(volume);
                    m_SortNeeded[mask] = true;
                }

                m_SortedSwitchers.Add(mask, list);
            }

            // Check sorting state
            bool sortNeeded;
            if (m_SortNeeded.TryGetValue(mask, out sortNeeded) && sortNeeded)
            {
                m_SortNeeded[mask] = false;
                SortByPriority(list);
            }

            return list;
        }

        // Stable insertion sort. Faster than List<T>.Sort() for our needs.
        static void SortByPriority(List<Switcher> switchers)
        {
            Assert.IsNotNull(switchers, "Trying to sort switchers of non-initialized layer");

            for (int i = 1; i < switchers.Count; i++)
            {
                var temp = switchers[i];
                int j = i - 1;

                // Sort order is ascending
                while (j >= 0 && switchers[j].priority > temp.priority)
                {
                    switchers[j + 1] = switchers[j];
                    j--;
                }

                switchers[j + 1] = temp;
            }
        }

        static bool IsSwitcherRenderedByCamera(Switcher volume, Camera camera)
        {
#if UNITY_2018_3_OR_NEWER && UNITY_EDITOR
            // IsGameObjectRenderedByCamera does not behave correctly when camera is null so we have to catch it here.
            return camera == null ? true : UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(volume.gameObject, camera);
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// A scope in which a Camera filters a Switcher.
    /// </summary>
    public struct SwitcherIsolationScope : IDisposable
    {
        /// <summary>
        /// Constructs a scope in which a Camera filters a Switcher.
        /// </summary>
        /// <param name="unused">Unused parameter.</param>
        public SwitcherIsolationScope(bool unused) => SwitcherManager.needIsolationFilteredByRenderer = true;

        /// <summary>
        /// Stops the Camera from filtering a Switcher.
        /// </summary>
        void IDisposable.Dispose() => SwitcherManager.needIsolationFilteredByRenderer = false;
    }
}