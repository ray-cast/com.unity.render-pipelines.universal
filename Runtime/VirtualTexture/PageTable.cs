using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class PageTable
    {
        /// <summary>
        /// 页表尺寸.
        /// </summary>
        [SerializeField]
        private int m_TableSize = 256;

        /// <summary>
        /// 页表层级结构
        /// </summary>
        private PageLevelTable[] _pageLevelTable;

        /// <summary>
        /// 当前活跃的页表
        /// </summary>
        private Dictionary<Vector2Int, TableNodeCell> _activePages = new Dictionary<Vector2Int, TableNodeCell>();

        /// <summary>
        /// 导出的页表寻址贴图
        /// </summary>
        private RenderTexture _lookupTexture;

        /// <summary>
        /// RT Job对象
        /// </summary>
        public RenderTextureJob _renderTextureJob;

        /// <summary>
        /// 平铺贴图对象
        /// </summary>
        private TiledTexture _tileTexture;

        /// <summary>
        /// 调试贴图
        /// </summary>
        public RenderTexture DebugTexture { get; private set; }

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int tableSize { get { return m_TableSize; } }

        public bool UseFeed { get; set; } = true;
        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        public int maxMipLevel { get { return (int)Mathf.Log(tableSize, 2); } }

        private Material drawLookupMat = null;
        private Mesh mQuad;

        public PageTable()
        {
            _renderTextureJob = new RenderTextureJob();
            _renderTextureJob.startRenderJob += OnRenderJob;
            _renderTextureJob.cancelRenderJob += OnRenderJobCancel;

            _lookupTexture = new RenderTexture(tableSize, tableSize, 0);
            _lookupTexture.filterMode = FilterMode.Point;
            _lookupTexture.wrapMode = TextureWrapMode.Clamp;

            _tileTexture = new TiledTexture();
            _tileTexture.Init();
            _tileTexture.OnTileUpdateComplete += InvalidatePage;

            _pageLevelTable = new PageLevelTable[maxMipLevel + 1];
            
            for (int i = 0; i <= maxMipLevel; i++)
                _pageLevelTable[i] = new PageLevelTable(i, tableSize);

            drawLookupMat = new Material(Shader.Find("VirtualTexture/VTDrawLookup"));
            drawLookupMat.enableInstancing = true;

            Shader.SetGlobalTexture("_VTLookupTex", _lookupTexture);
            Shader.SetGlobalVector("_VTPageParam", new Vector4(tableSize, 1.0f / tableSize, maxMipLevel, 0));

            InitializeQuadMesh();
        }

        public void Reset()
        {
            for (int i = 0; i <= maxMipLevel; i++)
            {
                for (int j = 0; j < _pageLevelTable[i].NodeCellCount; j++) 
                {
                    for (int k = 0; k < _pageLevelTable[i].NodeCellCount; k++)
                    {
                        InvalidatePage(_pageLevelTable[i].Cell[j, k].Payload.TileIndex);
                    }
                }
            }

            _activePages.Clear();
        }

        public void ChangeViewRect(Vector2Int offset)
        {
            for (int i = 0; i <= maxMipLevel; i++)
                _pageLevelTable[i].ChangeViewRect(offset, this.InvalidatePage);

            ActivatePage(0, 0, maxMipLevel);
        }

        public void UpdatePage(Vector2Int center)
        {
            if (this.UseFeed)
            {
                return;
            }

            for (int i = 0; i < tableSize; i++)
			{
                for (int j = 0; j < tableSize; j++)
                {
                    var thisPos = new Vector2Int(i, j);
                    Vector2Int ManhattanDistance = thisPos - center;
                    int absX = Mathf.Abs(ManhattanDistance.x);
                    int absY = Mathf.Abs(ManhattanDistance.y);
                    int absMax = Mathf.Max(absX, absY);
                    int tempMipLevel = (int)Mathf.Floor(Mathf.Sqrt(2 * absMax));
                    tempMipLevel = Mathf.Clamp(tempMipLevel, 0, maxMipLevel);
                    ActivatePage(i, j, tempMipLevel);
                }
            }

            this.UpdateLookup();
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

            mQuad = new Mesh();
            mQuad.SetVertices(quadVertexList);
            mQuad.SetUVs(0, quadUVList);
            mQuad.SetTriangles(quadTriangleList, 0);
        }

        /// <summary>
        /// 处理回读数据
        /// </summary>
        public void ProcessFeedback(NativeArray<Color32> textureData)
        {
            if (this.UseFeed)
            {
                foreach (var c in textureData)
                    ActivatePage(c.r, c.g, c.b);

                this.UpdateLookup();
            }
        }

        private class DrawPageInfo
        {
            public Rect rect;
            public int mip;
            public Vector2 drawPos;
        }

        private void UpdateLookup()
        {
            // 将页表数据写入页表贴图
            var currentFrame = (byte)Time.frameCount;
            var drawList = new List<DrawPageInfo>();

            foreach (var kv in _activePages)
            {
                var page = kv.Value;
                // 只写入当前帧活跃的页表
                if (page.Payload.ActiveFrame != Time.frameCount)
                    continue;

                var table = _pageLevelTable[page.MipLevel];
                var offset = table.pageOffset;
                var perSize = table.PerCellSize;
                var lb = new Vector2Int((page.Rect.xMin - offset.x * perSize),(page.Rect.yMin - offset.y * perSize));

                while (lb.x < 0)
                    lb.x += tableSize;

                while (lb.y < 0)
                    lb.y += tableSize;

                drawList.Add(new DrawPageInfo()
                {
                    rect = new Rect(lb.x, lb.y, page.Rect.width, page.Rect.height),
                    mip = page.MipLevel,
                    drawPos = new Vector2((float)page.Payload.TileIndex.x / 255,
                    (float)page.Payload.TileIndex.y / 255),
                });
            }
            drawList.Sort((a, b) => {
                return -(a.mip.CompareTo(b.mip));
            });
            if (drawList.Count == 0)
            {
                return;
            }

            var mats = new Matrix4x4[drawList.Count];
            var pageInfos = new Vector4[drawList.Count];

            for (int i = 0; i < drawList.Count; i++)
            {
                float size = drawList[i].rect.width / tableSize;
                mats[i] = Matrix4x4.TRS(
                    new Vector3(drawList[i].rect.x / tableSize, drawList[i].rect.y / tableSize),
                    Quaternion.identity,
                    new Vector3(size, size, size));

                pageInfos[i] = new Vector4(drawList[i].drawPos.x, drawList[i].drawPos.y, drawList[i].mip / 255f, 0);
            }

            if (drawLookupMat != null)
			{
                Graphics.SetRenderTarget(_lookupTexture);
                var tempCB = new CommandBuffer();
                var block = new MaterialPropertyBlock();
                block.SetVectorArray("_PageInfo", pageInfos);
                block.SetMatrixArray("_ImageMVP", mats);
                tempCB.DrawMeshInstanced(mQuad, 0, drawLookupMat, 0, mats, mats.Length, block);
                Graphics.ExecuteCommandBuffer(tempCB);
            }
        }

        /// <summary>
        /// 激活页表
        /// </summary>
        public TableNodeCell ActivatePage(int x, int y, int mip)
        {
            if (mip > maxMipLevel || mip < 0 || x < 0 || y < 0 || x >= tableSize || y >= tableSize)
                return null;

            // 找到当前页表
            var page = _pageLevelTable[mip].Get(x, y);
            if (page == null)
            {
                return null;
            }

            if (!page.Payload.IsReady)
            {
                LoadPage(x, y, page);

                //向上找到最近的父节点
                while (mip < maxMipLevel && !page.Payload.IsReady)
                {
                    mip++;
                    page = _pageLevelTable[mip].Get(x, y);
                }
            }

            if (page.Payload.IsReady)
            {
                // 激活对应的平铺贴图块
                _tileTexture.SetActive(page.Payload.TileIndex);
                page.Payload.ActiveFrame = Time.frameCount;
                return page;
            }

            return null;
        }

        /// <summary>
        /// 加载页表
        /// </summary>
        private void LoadPage(int x, int y, TableNodeCell node)
        {
            if (node == null)
                return;

            // 正在加载中,不需要重复请求
            if (node.Payload.LoadRequest != null)
                return;

            // 新建加载请求
            node.Payload.LoadRequest = _renderTextureJob.Request(x, y, node.MipLevel);
        }

        /// <summary>
        /// 开始渲染
        /// </summary>
        private void OnRenderJob(RenderTextureRequest request)
        {
            // 找到对应页表
            var node = _pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node == null || node.Payload.LoadRequest != request)
                return;

            node.Payload.LoadRequest = null;

            var id = _tileTexture.RequestTile();
            _tileTexture.UpdateTile(id, request);

            node.Payload.TileIndex = id;
            _activePages[id] = node;
        }

        /// <summary>
        /// 取消渲染
        /// </summary>
        private void OnRenderJobCancel(RenderTextureRequest request)
        {
            // 找到对应页表
            var node = _pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node == null || node.Payload.LoadRequest != request)
                return;

            node.Payload.LoadRequest = null;
        }

        /// <summary>
        /// 将页表置为非活跃状态
        /// </summary>
		private void InvalidatePage(Vector2Int id)
        {
            if (!_activePages.TryGetValue(id, out var node))
                return;

            node.Payload.ResetTileIndex();
            _activePages.Remove(id);
        }
    }
}