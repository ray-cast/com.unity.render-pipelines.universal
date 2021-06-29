using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public sealed class VirtualTextureSystem
    {
        static readonly Lazy<VirtualTextureSystem> s_Instance = new Lazy<VirtualTextureSystem>(() => new VirtualTextureSystem());

        /// <summary>
        /// 系统单例
        /// </summary>
        public static VirtualTextureSystem instance => s_Instance.Value;

        /// <summary>
        /// 画Tile的事件.
        /// </summary>
        public static event Action<RequestPageData, TiledTexture, Vector2Int> beginTileRendering;

        /// <summary>
        /// 查找表生成 Shader
        /// </summary>
        private Material _drawLookupMat = null;

        /// <summary>
        /// 平面网格
        /// </summary>
        private Mesh _quad;

        /// <summary>
        /// 导出的页表寻址贴图
        /// </summary>
        private RenderTexture _lookupTexture;

        /// <summary>
        /// 页表
        /// </summary>
        private PageTable _pageTable;

        /// <summary>
        /// 页表
        /// </summary>
        public PageTable pageTable { get => _pageTable; }

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int pageSize { get { return _pageTable.pageSize; } }

        VirtualTextureSystem()
        {
            InitializeQuadMesh();

            _pageTable = new PageTable();
            _pageTable.ActivatePage(0, 0, _pageTable.maxMipLevel);

            _drawLookupMat = new Material(Shader.Find("VirtualTexture/DrawLookup"));
            _drawLookupMat.enableInstancing = true;

            _lookupTexture = RenderTexture.GetTemporary(pageSize, pageSize, 0, RenderTextureFormat.ARGBHalf);
            _lookupTexture.name = "LookupTexture";
            _lookupTexture.filterMode = FilterMode.Point;
            _lookupTexture.wrapMode = TextureWrapMode.Clamp;

            Shader.SetGlobalTexture("_VTLookupTex", _lookupTexture);
        }

        private void InitializeQuadMesh()
        {
            List<Vector3> quadVertexList = new List<Vector3>();
            List<int> quadTriangleList = new List<int>();
            List<Vector2> quadUVList = new List<Vector2>();

            quadVertexList.Add(new Vector3(0, 1, 0.1f));
            quadUVList.Add(new Vector2(0, 1));
            quadVertexList.Add(new Vector3(0, 0, 0.1f));
            quadUVList.Add(new Vector2(0, 0));
            quadVertexList.Add(new Vector3(1, 0, 0.1f));
            quadUVList.Add(new Vector2(1, 0));
            quadVertexList.Add(new Vector3(1, 1, 0.1f));
            quadUVList.Add(new Vector2(1, 1));

            quadTriangleList.Add(0);
            quadTriangleList.Add(1);
            quadTriangleList.Add(2);

            quadTriangleList.Add(2);
            quadTriangleList.Add(3);
            quadTriangleList.Add(0);

            _quad = new Mesh();
            _quad.SetVertices(quadVertexList);
            _quad.SetUVs(0, quadUVList);
            _quad.SetTriangles(quadTriangleList, 0);
        }

        public void LoadPages(NativeArray<Color32> pageData)
        {
            if (_pageTable.useFeed)
            {
                foreach (var c in pageData)
                    _pageTable.ActivatePage(c.r, c.g, c.b);

                _pageTable.UpdateLookup(_lookupTexture, _drawLookupMat, _quad);
            }
        }

        public void Update()
		{
            _pageTable._renderTextureJob.Update();
        }

        public void UpdatePage(Vector2Int center)
        {
            if (_pageTable.useFeed)
            {
                return;
            }

            for (int i = 0; i < pageSize; i++)
            {
                for (int j = 0; j < pageSize; j++)
                {
                    var thisPos = new Vector2Int(i, j);
                    Vector2Int ManhattanDistance = thisPos - center;
                    int absX = Mathf.Abs(ManhattanDistance.x);
                    int absY = Mathf.Abs(ManhattanDistance.y);
                    int absMax = Mathf.Max(absX, absY);

                    _pageTable.ActivatePage(i, j, Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(2 * absMax)), 0, _pageTable.maxMipLevel));
                }
            }

            _pageTable.UpdateLookup(_lookupTexture, _drawLookupMat, _quad);
        }

        internal static bool InvokeBeginTileRendering(RequestPageData request, TiledTexture tiledTexture, Vector2Int tile)
        {
            if (beginTileRendering != null)
            {
                beginTileRendering(request, tiledTexture, tile);
                return true;
            }

            return false;
        }
    }
}