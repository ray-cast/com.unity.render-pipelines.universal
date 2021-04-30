using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class MeshBatchData
    {
        public delegate void onUploadMeshDataEvent();

        public int brushSensity = 2;

        public List<BatchData> instanceData = new List<BatchData>();

        public event onUploadMeshDataEvent onUploadMeshData;

        public void Append(Vector3 worldPos, Vector3 worldScale)
        {
            worldPos.x = Mathf.Round(worldPos.x * 100) / 100;
            worldPos.y = Mathf.Round(worldPos.y * 100) / 100;
            worldPos.z = Mathf.Round(worldPos.z * 100) / 100;

            bool isAlreadyExist = false;

            for (int i = 0; i < instanceData.Count; i++)
            {
                if (Vector3.Distance(instanceData[i].worldPos, worldPos) * 100 < brushSensity)
                {
                    isAlreadyExist = true;
                    break;
                }
            }

            if (!isAlreadyExist)
            {
                instanceData.Add(new BatchData() { worldPos = worldPos, worldScale = worldScale });
            }
        }

        public void Remove(Vector3 center, float radius)
        {
            for (int i = instanceData.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(instanceData[i].worldPos, center) < radius)
                    instanceData.RemoveAt(i);
            }
        }

        public void Clear()
        {
            instanceData.Clear();
        }

        public void UploadMeshData()
        {
            if (onUploadMeshData != null)
                onUploadMeshData();
        }

        public void RandomGroup(Transform transform, int instanceCount)
        {
            instanceData.Clear();
            UnityEngine.Random.InitState(123);

            float scale = Mathf.Sqrt(instanceCount) / 40f;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);

            for (int i = 0; i < instanceCount; i++)
            {
                Vector3 pos = Vector3.zero;

                pos.x = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.x;
                pos.z = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.z;

                pos += transform.position;

                instanceData.Add(new BatchData() { worldPos = new Vector3(pos.x, pos.y, pos.z), worldScale = Vector3.one });
            }

            this.UploadMeshData();
        }

        public void RandomGroupBySensity(Transform transform, int instanceCount)
        {
            instanceData.Clear();

            int wide = Mathf.RoundToInt(Mathf.Sqrt(instanceCount));
            int count = 0;
            float ss = brushSensity / 100f;
            Vector3 offset = transform.position + new Vector3(-wide / 2f * ss, 0, -wide / 2f * ss);
            for (int i = 0; i < wide && count < instanceCount; i++)
            {
                for (int j = 0; j < wide && count < instanceCount; j++)
                {
                    Vector3 pos = new Vector3(j* ss, 0, i* ss);
                    pos += offset;
                    instanceData.Add(new BatchData() { worldPos = new Vector3(pos.x, pos.y, pos.z), worldScale = Vector3.one });
                    count++;
                }
            }

            this.UploadMeshData();
        }
    }
}

