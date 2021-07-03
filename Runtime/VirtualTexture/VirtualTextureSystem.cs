using System;
using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    public sealed class VirtualTextureSystem : IDisposable
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
        /// Mip的偏移量
        /// </summary>
        private int _mipmapBias = 0;

        /// <summary>
        /// 覆盖区域大小.
        /// </summary>
        [SerializeField]
        private int _regionSize = 256;

        /// <summary>
        /// 覆盖的区域.
        /// </summary>
        private Rect _regionRange = new Rect(-128, -128, 128, 128);

        /// <summary>
        /// 覆盖的区域.
        /// </summary>
        private ScaleFactor _regionChangeDistance = ScaleFactor.Eighth;

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        [SerializeField]
        private int _pageSize = 256;

        /// <summary>
        /// 页表
        /// </summary>
        private PageTable _pageTable;

        /// <summary>
        /// 平面网格
        /// </summary>
        private Mesh _quad;

        /// <summary>
        /// 页表对应的世界区域.
        /// </summary>
        public Rect regionRange { get => _regionRange; }

        /// <summary>
        /// 页表对应的世界刷新距离.
        /// </summary>
        public float regionChangeDistance { get => _regionSize * ScaleModeExtensions.ToFloat(_regionChangeDistance); }

        /// <summary>
        /// 单个页表对应的世界尺寸.
        /// </summary>
        public float regionCellSize
        {
            get
            {
                return _regionSize / (float)_pageSize;
            }
        }

        /// <summary>
        /// 平面网格
        /// </summary>
        public Mesh quad
        {
            get
            {
                if (_quad == null)
                    InitializeQuadMesh();

                return _quad;
            }
        }

        /// <summary>
        /// 导出的页表寻址贴图
        /// </summary>
        public RenderTexture lookupTexture { get; private set; }

        /// <summary>
        /// 平铺贴图对象
        /// </summary>
        public TiledTexture tileTexture { get; private set; }

        /// <summary>
        /// 尺寸缩放.
        /// </summary>
        public ScaleFactor scale = ScaleFactor.Half;

        /// <summary>
        /// Tile尺寸.
        /// </summary>
        public int tileSize { get { return tileTexture.tileSize; } }

        /// <summary>
        /// 页表
        /// </summary>
        public PageTable pageTable { get => _pageTable; }

        /// <summary>
        /// 页表大小.
        /// </summary>
        public int pageSize { get { return _pageSize; } }

        /// <summary>
        /// 每帧的请求数量.
        /// </summary>
        public int requestLimits { get => _requestPageJob.limit; set => _requestPageJob.limit = value; }

        /// <summary>
        /// 画Tile的事件.
        /// </summary>
        public static event Action<RequestPageData, TiledTexture, Vector2Int> beginTileRendering;

        /// <summary>
        /// 重置Page事件.
        /// </summary>
        public static event Action resetPageTable;

        private class DrawPageInfo
        {
            public Rect rect;
            public int mip;
            public Vector2 drawPos;
        }

        public void Init()
        {
            InitializeQuadMesh();

            _pageTable = new PageTable(_pageSize);
            
            _regionRange = new Rect(-_regionSize / 2, -_regionSize / 2, _regionSize, _regionSize);

            _requestPageJob = new RequestPageDataJob();
            _requestPageJob.startRequestPageJob += OnStartRequestPageJob;
            _requestPageJob.cancelRequestPageJob += OnCancelRequestPageJob;

            if (lookupTexture)
                RenderTexture.ReleaseTemporary(lookupTexture);

            lookupTexture = RenderTexture.GetTemporary(pageSize, pageSize, 0, RenderTextureFormat.ARGBHalf);
            lookupTexture.name = "LookupTexture";
            lookupTexture.filterMode = FilterMode.Point;
            lookupTexture.wrapMode = TextureWrapMode.Clamp;

            tileTexture?.Dispose();
            tileTexture = new TiledTexture();

            Shader.SetGlobalTexture("_VirtualLookupTexture", lookupTexture);
            Shader.SetGlobalTexture("_VirtualBufferTexture0", tileTexture.tileTextures[0]);
            Shader.SetGlobalTexture("_VirtualBufferTexture1", tileTexture.tileTextures[1]);
            Shader.SetGlobalTexture("_VirtualBufferTexture2", tileTexture.tileTextures[2]);
            Shader.SetGlobalTexture("_VirtualBufferTexture3", tileTexture.tileTextures[3]);

            Shader.SetGlobalVector("_VirtualPage_Params", new Vector4(pageSize, 1.0f / pageSize, _pageTable.maxMipLevel, 0));
            Shader.SetGlobalVector("_VirtualRegion_Params", new Vector4(_regionRange.x, _regionRange.y, 1.0f / _regionRange.width, 1.0f / _regionRange.height));
            Shader.SetGlobalVector("_VirtualTile_Params", new Vector4(tileTexture.paddingSize, tileTexture.tileSize, tileTexture.width, tileTexture.height));
            Shader.SetGlobalVector("_VirtualFeedback_Params", new Vector4(pageSize, pageSize * tileSize * scale.ToFloat(), pageTable.maxMipLevel - 1, _mipmapBias));

            this.LoadPage(0, 0, _pageTable.maxMipLevel);
        }

        ~VirtualTextureSystem()
        {
            this.Dispose();
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
                        page = _pageTable.GetPage(x, y, mip);

                        if (!page.payload.isReady &&  page.payload.loadRequest == null)
                            page.payload.loadRequest = _requestPageJob.Request(x, y, page.mipLevel);
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

        public void LoadPages(Color32[] pageData)
        {
            foreach (var data in pageData)
			{
                if (data.a == 0)
                    continue;

                var page = LoadPage(data.r, data.g, data.b);
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

        public void SetRegion(Rect region)
        {
            if (!_regionRange.Equals(region))
            {
                _regionRange = region;
                Shader.SetGlobalVector("_VirtualRegion_Params", new Vector4(region.x, region.y, 1.0f / region.width, 1.0f / region.height));

                this.Reset();
            }
        }

        public bool UpdateRegion(Vector3 position)
        {
            var fixedPos = GetFixedPos(position);
            var xDiff = fixedPos.x - _regionRange.center.x;
            var yDiff = fixedPos.y - _regionRange.center.y;

            if (Mathf.Abs(xDiff) > regionChangeDistance || Mathf.Abs(yDiff) > regionChangeDistance)
            {
                var fixedCenter = GetFixedCenter(fixedPos);
                if (fixedCenter != _regionRange.center)
                {
                    this.ClearJob();

                    var oldCenter = new Vector2Int((int)_regionRange.center.x, (int)_regionRange.center.y);
                    _regionRange = new Rect(fixedCenter.x - _regionSize / 2, fixedCenter.y - _regionSize / 2, _regionSize, _regionSize);
                    
                    Shader.SetGlobalVector("_VirtualRegion_Params", new Vector4(_regionRange.x, _regionRange.y, 1.0f / _regionRange.width, 1.0f / _regionRange.height));
                    
                    Vector2Int offset = (fixedCenter - oldCenter) / (int)regionCellSize;

                    for (int i = 0; i <= _pageTable.maxMipLevel; i++)
                        _pageTable.pageLevelTable[i].ChangeViewRect(offset, this.InvalidatePage);

                    return true;
                }
            }

            return false;
        }

        public void Reset()
        {
            _pageTable?.ResetTileIndex();
            _activePages?.Clear();
            _requestPageJob?.Clear();

            tileTexture?.Clear();

            Shader.SetGlobalTexture("_VirtualLookupTexture", lookupTexture);
            Shader.SetGlobalTexture("_VirtualBufferTexture0", tileTexture.tileTextures[0]);
            Shader.SetGlobalTexture("_VirtualBufferTexture1", tileTexture.tileTextures[1]);
            Shader.SetGlobalTexture("_VirtualBufferTexture2", tileTexture.tileTextures[2]);
            Shader.SetGlobalTexture("_VirtualBufferTexture3", tileTexture.tileTextures[3]);

            Shader.SetGlobalVector("_VirtualPage_Params", new Vector4(pageSize, 1.0f / pageSize, _pageTable.maxMipLevel, 0));
            Shader.SetGlobalVector("_VirtualRegion_Params", new Vector4(_regionRange.x, _regionRange.y, 1.0f / _regionRange.width, 1.0f / _regionRange.height));
            Shader.SetGlobalVector("_VirtualTile_Params", new Vector4(tileTexture.paddingSize, tileTexture.tileSize, tileTexture.width, tileTexture.height));
            Shader.SetGlobalVector("_VirtualFeedback_Params", new Vector4(pageSize, pageSize * tileSize * scale.ToFloat(), pageTable.maxMipLevel - 1, _mipmapBias));

            LoadPage(0, 0, _pageTable.maxMipLevel);

            resetPageTable.Invoke();
        }

        public void ClearJob()
        {
            Debug.Assert(_requestPageJob != null);
            _requestPageJob.Clear();
        }

        public void UpdateJob(CommandBuffer cmd)
        {
            Debug.Assert(_requestPageJob != null);

            _requestPageJob.Update(cmd);
        }

        public void UpdateLookup(CommandBuffer cmd, Material drawLookupMat, int pass = 0)
        {
            Debug.Assert(drawLookupMat != null);

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

                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetVectorArray("_PageInfo", pageInfos);
                propertyBlock.SetMatrixArray("_ImageMVP", mats);

                cmd.SetRenderTarget(lookupTexture);
                cmd.DrawMeshInstanced(this.quad, 0, drawLookupMat, pass, mats, mats.Length, propertyBlock);
            }
        }

        private Vector2Int GetFixedCenter(Vector2Int pos)
        {
            return new Vector2Int((int)Mathf.Floor(pos.x / regionChangeDistance + 0.5f) * (int)regionChangeDistance,
                                  (int)Mathf.Floor(pos.y / regionChangeDistance + 0.5f) * (int)regionChangeDistance);
        }

        private Vector2Int GetFixedPos(Vector3 pos)
        {
            return new Vector2Int((int)(Mathf.Floor(pos.x / regionCellSize + 0.5f) * regionCellSize),
                                  (int)(Mathf.Floor(pos.z / regionCellSize + 0.5f) * regionCellSize));
        }

        /// <summary>
        /// 将页表置为活跃状态
        /// </summary>
        private void ActivatePage(Vector2Int tile, TableNodeCell page)
        {
            if (_activePages.TryGetValue(tile, out var node))
            {
                node.payload.ResetTileIndex();
                _activePages.Remove(tile);
            }

            page.payload.tileIndex = tile;

            _activePages[tile] = page;
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
        private bool OnStartRequestPageJob(RequestPageData request)
        {
            if (beginTileRendering == null || beginTileRendering.GetInvocationList().Length == 0)
                return false;

            var page = _pageTable.GetPage(request.pageX, request.pageY, request.mipLevel);
            if (page != null && page.payload.loadRequest == request)
            {
                var tile = tileTexture.RequestTile();
                if (tileTexture.SetActive(tile))
                {
                    ActivatePage(tile, page);

                    page.payload.loadRequest = null;
                    page.payload.activeFrame = Time.frameCount;

                    beginTileRendering.Invoke(request, tileTexture, tile);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 取消页面加载
        /// </summary>
        private void OnCancelRequestPageJob(RequestPageData request)
		{
            var page = _pageTable.GetPage(request.pageX, request.pageY, request.mipLevel);
            if (page != null)
            {
                if (page.payload.loadRequest == request)
                    page.payload.loadRequest = null;
            }
        }

		public void Dispose()
		{
            if (lookupTexture)
            {
                RenderTexture.ReleaseTemporary(lookupTexture);
                lookupTexture = null;
            }
        }
	}
}