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
        /// 当前活跃的页表
        /// </summary>
        private Dictionary<Vector2Int, TableNodeCell> _activePages = new Dictionary<Vector2Int, TableNodeCell>();

        /// <summary>
        /// RT Job对象
        /// </summary>
        private RequestPageDataJob _requestPageJob;

        /// <summary>
        /// 页表
        /// </summary>
        private PageTable _pageTable;

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
        public RenderTexture lookupTexture { get; private set; }

        /// <summary>
        /// 平铺贴图对象
        /// </summary>
        public TiledTexture tileTexture { get; private set; }

        /// <summary>
        /// Tile尺寸.
        /// </summary>
        public int tileSize { get { return tileTexture.tileSize; } }

        /// <summary>
        /// 页表
        /// </summary>
        public PageTable pageTable { get => _pageTable; }

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int pageSize { get { return _pageTable.pageSize; } }

        /// <summary>
        /// 画Tile的事件.
        /// </summary>
        public static event Action<RequestPageData, TiledTexture, Vector2Int> beginTileRendering;

        private class DrawPageInfo
        {
            public Rect rect;
            public int mip;
            public Vector2 drawPos;
        }

        VirtualTextureSystem()
        {
            InitializeQuadMesh();

            _requestPageJob = new RequestPageDataJob();
            _requestPageJob.startRenderJob += OnRequestPageJob;
            _requestPageJob.cancelRenderJob += OnCancelPageJob;

            _pageTable = new PageTable(_requestPageJob);

            _drawLookupMat = new Material(Shader.Find("VirtualTexture/DrawLookup"));
            _drawLookupMat.enableInstancing = true;

            lookupTexture = RenderTexture.GetTemporary(pageSize, pageSize, 0, RenderTextureFormat.ARGBHalf);
            lookupTexture.name = "LookupTexture";
            lookupTexture.filterMode = FilterMode.Point;
            lookupTexture.wrapMode = TextureWrapMode.Clamp;

            tileTexture = new TiledTexture();

            Shader.SetGlobalTexture("_VTDiffuse", tileTexture.tileTextures[0]);
            Shader.SetGlobalTexture("_VTNormal", tileTexture.tileTextures[1]);
            Shader.SetGlobalVector("_VTPageParam", new Vector4(pageSize, 1.0f / pageSize, _pageTable.maxMipLevel, 0));
            Shader.SetGlobalVector("_VTTileParam", new Vector4(tileTexture.paddingSize, tileTexture.tileSize, tileTexture.width, tileTexture.height));

            Shader.SetGlobalTexture("_VTLookupTex", lookupTexture);

            this.LoadPage(0, 0, _pageTable.maxMipLevel);
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

        public void Reset()
        {
            for (int i = 0; i <= _pageTable.maxMipLevel; i++)
            {
                for (int j = 0; j < _pageTable.pageLevelTable[i].nodeCellCount; j++)
                {
                    for (int k = 0; k < _pageTable.pageLevelTable[i].nodeCellCount; k++)
                    {
                        InvalidatePage(_pageTable.pageLevelTable[i].cell[j, k].payload.tileIndex);
                    }
                }
            }

            _activePages.Clear();
        }

        public void UpdateLookup()
        {
            // 将页表数据写入页表贴图
            var currentFrame = (byte)Time.frameCount;
            var drawList = new List<DrawPageInfo>();

            foreach (var kv in _activePages)
            {
                var page = kv.Value;

                // 只写入当前帧活跃的页表
                if (page.payload.activeFrame != Time.frameCount)
                    continue;

                var table = _pageTable.pageLevelTable[page.mipLevel];
                var offset = table.pageOffset;
                var perSize = table.perCellSize;
                var lb = new Vector2Int((page.rect.xMin - offset.x * perSize), (page.rect.yMin - offset.y * perSize));

                while (lb.x < 0) lb.x += pageSize;
                while (lb.y < 0) lb.y += pageSize;

                drawList.Add(new DrawPageInfo()
                {
                    rect = new Rect(lb.x, lb.y, page.rect.width, page.rect.height),
                    mip = page.mipLevel,
                    drawPos = new Vector2((float)page.payload.tileIndex.x / 255,
                    (float)page.payload.tileIndex.y / 255),
                });
            }

            if (drawList.Count > 0)
            {
                drawList.Sort((a, b) => { return -(a.mip.CompareTo(b.mip)); });

                var mats = new Matrix4x4[drawList.Count];
                var pageInfos = new Vector4[drawList.Count];

                for (int i = 0; i < drawList.Count; i++)
                {
                    var size = drawList[i].rect.width / pageSize;
                    var position = new Vector3(drawList[i].rect.x / pageSize, drawList[i].rect.y / pageSize);

                    mats[i] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(size, size, size));
                    pageInfos[i] = new Vector4(drawList[i].drawPos.x, drawList[i].drawPos.y, drawList[i].mip / 255f, 0);
                }

                if (_drawLookupMat != null)
                {
                    var propertyBlock = new MaterialPropertyBlock();
                    propertyBlock.SetVectorArray("_PageInfo", pageInfos);
                    propertyBlock.SetMatrixArray("_ImageMVP", mats);

                    var cmd = CommandBufferPool.Get();
                    cmd.SetRenderTarget(lookupTexture);
                    cmd.DrawMeshInstanced(_quad, 0, _drawLookupMat, 0, mats, mats.Length, propertyBlock);

                    Graphics.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        /// <summary>
        /// 激活页表
        /// </summary>
        public TableNodeCell LoadPage(int x, int y, int mip)
        {
            var page = _pageTable.FindPage(x, y, mip);
            if (page != null)
            {
                if (!page.payload.isReady)
                {
                    if (page.payload.loadRequest == null)
                        page.payload.loadRequest = _requestPageJob.Request(x, y, page.mipLevel);

                    //向上找到最近的父节点
                    while (mip < _pageTable.maxMipLevel && !page.payload.isReady)
                    {
                        mip++;
                        page = _pageTable.pageLevelTable[mip].Get(x, y);
                    }
                }

                if (page.payload.isReady)
                {
                    tileTexture.SetActive(page.payload.tileIndex);

                    page.payload.activeFrame = Time.frameCount;
                }

                return page;
            }

            return null;
        }

        public void LoadPages(NativeArray<Color32> pageData)
        {
            foreach (var c in pageData)
			{
                var page = LoadPage(c.r, c.g, c.b);
                if (page != null)
				{
                    if (page.payload.isReady)
                    {
                        tileTexture.SetActive(page.payload.tileIndex);

                        page.payload.activeFrame = Time.frameCount;
                    }
                }
            }
        }

        public void UpdatePage(Vector2Int center)
        {
            for (int i = 0; i < pageSize; i++)
            {
                for (int j = 0; j < pageSize; j++)
                {
                    var thisPos = new Vector2Int(i, j);
                    Vector2Int ManhattanDistance = thisPos - center;
                    int absX = Mathf.Abs(ManhattanDistance.x);
                    int absY = Mathf.Abs(ManhattanDistance.y);
                    int absMax = Mathf.Max(absX, absY);

                    LoadPage(i, j, Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(2 * absMax)), 0, _pageTable.maxMipLevel));
                }
            }
        }

        public void ChangeViewRect(Vector2Int offset)
        {
            for (int i = 0; i <= _pageTable.maxMipLevel; i++)
               _pageTable.pageLevelTable[i].ChangeViewRect(offset, this.InvalidatePage);

            LoadPage(0, 0, _pageTable.maxMipLevel);
        }

        public void Update()
        {
            _requestPageJob.Update();
        }

        /// <summary>
        /// 将页表置为非活跃状态
        /// </summary>
        private void InvalidatePage(Vector2Int tile)
        {
            if (_activePages.TryGetValue(tile, out var node))
            {
                node.payload.ResetTileIndex();
                _activePages.Remove(tile);
            }
        }

        /// <summary>
        /// 请求页面加载
        /// </summary>
        private void OnRequestPageJob(RequestPageData request)
        {
            var node = _pageTable.pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node != null && node.payload.loadRequest == request)
            {
                var tile = tileTexture.RequestTile();
                node.payload.tileIndex = tile;
                node.payload.loadRequest = null;

                InvalidatePage(tile);

                if (beginTileRendering != null)
                {
                    if (tileTexture.SetActive(tile))
                    {
                        beginTileRendering(request, tileTexture, tile);
                    }
                }

                _activePages[tile] = node;
            }
        }

        /// <summary>
        /// 取消页面加载
        /// </summary>
        private void OnCancelPageJob(RequestPageData request)
		{
            var node = _pageTable.pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node != null)
            {
                if (node.payload.loadRequest == request)
                    node.payload.loadRequest = null;
            }
        }
    }
}