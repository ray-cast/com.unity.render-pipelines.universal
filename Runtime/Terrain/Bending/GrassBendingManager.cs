using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Manages <see cref="MonoBehaviourGrassBender"/> objects and provides bending data to the shader.
    /// </summary>
    public static class GrassBendingManager
    {
        private class ProxyBehaviour : MonoBehaviour
        {
            public System.Action OnUpdate { get; set; }
            private void Update() => OnUpdate?.Invoke();
        }

        private const int sourcesLimit = 16;
        private static readonly HashSet<MonoBehaviourGrassBender> benders = new HashSet<MonoBehaviourGrassBender>();
        private static readonly Vector4[] bendData = new Vector4[sourcesLimit];
        private static readonly int bendDataPropertyId = Shader.PropertyToID("_BendData");
        private static readonly int bendCountPropertyId = Shader.PropertyToID("_BendCount");

        public static void AddBender(MonoBehaviourGrassBender bender)
        {
            if (!benders.Add(bender)) return;
            bender.SaveLastPosition();
            // SortedSet generates garbage on enumeration, so hacking with linq here.
            var sortedBenders = benders.OrderBy(b => b.Priority).ToList();

            benders.Clear();
            benders.UnionWith(sortedBenders);
        }

        public static void RemoveBender(MonoBehaviourGrassBender bender) => benders.Remove(bender);



        private static float ProcessOneBender(MonoBehaviourGrassBender bender, float deltaTime)
        {
            //计算速度
            float vec = Vector3.Distance(bender.Position, bender.LastPosition) / deltaTime;
            //Debug.Log("See vec:" + vec);
            //if (Time.frameCount%20 ==0 && bender.showVec)
            //{
            //   Debug.Log("See vec:" + vec);
            //}
            float targetRadius = bender.BendRadius;
            if (vec < 2)//速度为2以上 radius拉满，以下递减
            {
                targetRadius = bender.BendRadius * vec / 2;
            }

            targetRadius = Mathf.Max(targetRadius, bender.BendRadius * bender.minBend);

            bender.LastRadius = Mathf.SmoothDamp(bender.LastRadius, targetRadius, ref bender.changeVelocity, bender.targetTime);

            bender.SaveLastPosition();

            return bender.LastRadius;
        }


        private static void ProcessBenders()
        {
            var time = Time.time;
            var deltaTime = Time.deltaTime;
            float ap = Mathf.Sin(time * 2) + 1 / 2;

            var sourceIndex = 0;
            foreach (var bender in benders)
            {
                ProcessOneBender(bender, deltaTime);
                if (sourceIndex >= sourcesLimit) break;
                bendData[sourceIndex] = new Vector4(bender.Position.x, bender.Position.y, bender.Position.z, bender.LastRadius);
                sourceIndex++;
            }

            for (int i = sourceIndex; i < bendData.Length; i++)
                bendData[i] = Vector4.zero;

            Shader.SetGlobalFloat(bendCountPropertyId, benders.Count);
            Shader.SetGlobalVectorArray(bendDataPropertyId, bendData);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var objectName = nameof(GrassBendingManager);
            var gameObject = new GameObject(objectName);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(gameObject);

            var proxyBehaviour = gameObject.AddComponent<ProxyBehaviour>();
            proxyBehaviour.OnUpdate = ProcessBenders;
        }
    }
}
