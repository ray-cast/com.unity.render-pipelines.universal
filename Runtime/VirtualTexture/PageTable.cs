using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public class PageTable
    {
        /// <summary>
        /// 页表尺寸.
        /// </summary>
        [SerializeField]
        private int _pageSize = 256;

        /// <summary>
        /// 页表层级结构
        /// </summary>
        private PageLevelTable[] _pageLevelTable;

        /// <summary>
        /// 当前活跃的页表
        /// </summary>
        private Dictionary<Vector2Int, TableNodeCell> _activePages = new Dictionary<Vector2Int, TableNodeCell>();

        /// <summary>
        /// RT Job对象
        /// </summary>
        public RequestPageDataJob _renderTextureJob;

        /// <summary>
        /// 平铺贴图对象
        /// </summary>
        private TiledTexture _tileTexture;

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int pageSize { get { return _pageSize; } }

        /// <summary>
        /// Tile尺寸.
        /// </summary>
        public int tileSize { get { return _tileTexture.tileSize; } }

        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        public int maxMipLevel { get { return (int)Mathf.Log(pageSize, 2); } }

        public bool useFeed { get; set; } = true;

        private class DrawPageInfo
        {
            public Rect rect;
            public int mip;
            public Vector2 drawPos;
        }

        public PageTable()
        {
            _renderTextureJob = new RequestPageDataJob();
            _renderTextureJob.startRenderJob += OnRenderJob;
            _renderTextureJob.cancelRenderJob += UnloadPage;

            _tileTexture = new TiledTexture();
            _pageLevelTable = new PageLevelTable[maxMipLevel + 1];
            
            for (int i = 0; i <= maxMipLevel; i++)
                _pageLevelTable[i] = new PageLevelTable(i, pageSize);

            Shader.SetGlobalTexture("_VTDiffuse", _tileTexture.tileTextures[0]);
            Shader.SetGlobalTexture("_VTNormal", _tileTexture.tileTextures[1]);
            Shader.SetGlobalVector("_VTPageParam", new Vector4(pageSize, 1.0f / pageSize, maxMipLevel, 0));
            Shader.SetGlobalVector("_VTTileParam", new Vector4(_tileTexture.paddingSize, _tileTexture.tileSize, _tileTexture.width, _tileTexture.height));
        }

        public void Reset()
        {
            for (int i = 0; i <= maxMipLevel; i++)
            {
                for (int j = 0; j < _pageLevelTable[i].nodeCellCount; j++) 
                {
                    for (int k = 0; k < _pageLevelTable[i].nodeCellCount; k++)
                    {
                        InvalidatePage(_pageLevelTable[i].cell[j, k].payload.tileIndex);
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

        public void UpdateLookup(RenderTexture renderTexture, Material material, Mesh mesh)
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

                var table = _pageLevelTable[page.mipLevel];
                var offset = table.pageOffset;
                var perSize = table.perCellSize;
                var lb = new Vector2Int((page.rect.xMin - offset.x * perSize),(page.rect.yMin - offset.y * perSize));

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
                
                if (material != null)
                {
                    var propertyBlock = new MaterialPropertyBlock();
                    propertyBlock.SetVectorArray("_PageInfo", pageInfos);
                    propertyBlock.SetMatrixArray("_ImageMVP", mats);

                    var cmd = CommandBufferPool.Get();
                    cmd.SetRenderTarget(renderTexture);
                    cmd.DrawMeshInstanced(mesh, 0, material, 0, mats, mats.Length, propertyBlock);

                    Graphics.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        /// <summary>
        /// 激活页表
        /// </summary>
        public TableNodeCell ActivatePage(int x, int y, int mip)
        {
            if (mip > maxMipLevel || mip < 0 || x < 0 || y < 0 || x >= pageSize || y >= pageSize)
                return null;

            var page = _pageLevelTable[mip].Get(x, y);
            if (page != null)
            {
                if (!page.payload.isReady)
                {
                    LoadPage(x, y, page);

                    //向上找到最近的父节点
                    while (mip < maxMipLevel && !page.payload.isReady)
                    {
                        mip++;
                        page = _pageLevelTable[mip].Get(x, y);
                    }
                }

                if (page.payload.isReady)
                {
                    // 激活对应的平铺贴图块
                    _tileTexture.SetActive(page.payload.tileIndex);
                    page.payload.activeFrame = Time.frameCount;
                    return page;
                }
            }

            return null;
        }

        /// <summary>
        /// 将页表置为非活跃状态
        /// </summary>
        private void InvalidatePage(Vector2Int id)
        {
            if (_activePages.TryGetValue(id, out var node))
            {
                node.payload.ResetTileIndex();
                _activePages.Remove(id);
            }
        }

        /// <summary>
        /// 加载页表
        /// </summary>
        private void LoadPage(int x, int y, TableNodeCell node)
        {
            if (node != null)
			{
                if (node.payload.loadRequest == null)
                    node.payload.loadRequest = _renderTextureJob.Request(x, y, node.mipLevel);
            }
        }

        /// <summary>
        /// 取消渲染
        /// </summary>
        private void UnloadPage(RequestPageData request)
        {
            var node = _pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node != null)
            {
                if (node.payload.loadRequest == request)
                    node.payload.loadRequest = null;
            }
        }

        /// <summary>
        /// 开始渲染
        /// </summary>
        private void OnRenderJob(RequestPageData request)
        {
            var node = _pageLevelTable[request.mipLevel].Get(request.pageX, request.pageY);
            if (node != null && node.payload.loadRequest == request)
			{
                var tile = _tileTexture.RequestTile();
                node.payload.tileIndex = tile;
                node.payload.loadRequest = null;

                if (_tileTexture.SetActive(tile))
                {
                    if (VirtualTextureSystem.InvokeBeginTileRendering(request, _tileTexture, tile))
                        InvalidatePage(tile);
                }

                _activePages[tile] = node;
            }
        }
    }
}