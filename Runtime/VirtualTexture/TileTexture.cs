﻿using System;

namespace UnityEngine.Rendering.Universal
{
    public sealed class TiledTexture : IDisposable
    {
        /// <summary>
        /// 单个Tile的尺寸.
        /// </summary>
        [SerializeField]
        private int _tileSize = 256;

        /// <summary>
        /// 填充尺寸
        /// </summary>
        [SerializeField]
        private int _paddingSize = 4;

        /// <summary>
        /// 区域尺寸.
        /// </summary>
        [SerializeField]
        private Vector2Int _regionSize = new Vector2Int(15, 15);

        /// <summary>
        /// Tile缓存池.
        /// </summary>
        private LruCache _tilePool;

        /// <summary>
        /// Tile Target
        /// </summary>
        public RenderTexture[] tileTextures { get; private set; }

        /// <summary>
        /// Tile 颜色纹理
        /// </summary>
        public RenderTargetIdentifier[] tileBuffers { get; private set; }

        /// <summary>
        /// Tile 深度纹理
        /// </summary>
        public RenderTargetIdentifier tileDepthBuffer { get; private set; }

        /// <summary>
        /// 区域尺寸.
        /// 区域尺寸表示横竖两个方向上Tile的数量.
        /// </summary>
        public Vector2Int regionSize { get { return _regionSize; } }

        /// <summary>
        /// 填充尺寸
        /// </summary>
        public int paddingSize { get { return _paddingSize; } }

        /// <summary>
        /// 单个Tile的尺寸.
        /// Tile是宽高相等的正方形.
        /// </summary>
        public int tileSize { get { return _tileSize; } }

        /// <summary>
        /// 实际单个Tile + Padding的尺寸.
        /// 每个Tile上下左右四个方向都要进行填充，用来支持硬件纹理过滤.
        /// 所以Tile有效尺寸为(TileSize - PaddingSize * 2)
        /// </summary>
        public int tileSizeWithPadding { get { return tileSize + paddingSize * 2; } }

        /// <summary>
        /// Tile纹理的宽度.
        /// </summary>
        public int width { get { return regionSize.x * tileSizeWithPadding; } }

        /// <summary>
        /// Tile纹理的高度.
        /// </summary>
        public int height { get { return regionSize.y * tileSizeWithPadding; } }

        public TiledTexture()
        {
            _tilePool = new LruCache(regionSize.x * regionSize.y);

            tileTextures = new RenderTexture[3];
            tileBuffers = new RenderTargetIdentifier[tileTextures.Length];

            for (int i = 0; i < tileTextures.Length; i++)
			{
                tileTextures[i] = RenderTexture.GetTemporary(this.width, this.height, 0, Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
                tileTextures[i].name = "VirtualBufferTexture" + i;
                tileTextures[i].filterMode = FilterMode.Bilinear;
                tileTextures[i].wrapMode = TextureWrapMode.Clamp;

                tileBuffers[i] = tileTextures[i].colorBuffer;
            }
            
            tileDepthBuffer = tileTextures[0].depthBuffer;
        }

        ~TiledTexture()
        {
            this.Dispose();
        }

        public Vector2Int IdToPos(int id)
        {
            return new Vector2Int(id % regionSize.x, id / regionSize.x);
        }

        public int PosToId(Vector2Int tile)
        {
            return tile.y * regionSize.x + tile.x;
        }

        public int RequestTile()
        {
            return _tilePool.first;
        }

        public bool SetActive(int tile)
        {
            return _tilePool.SetActive(tile);
        }

        public RectInt TileToRect(Vector2Int tile)
		{
            return new RectInt(tile.x * tileSizeWithPadding, tile.y * tileSizeWithPadding,  tileSizeWithPadding, tileSizeWithPadding);
		}

        public Matrix4x4 GetMatrix(int id)
        {
            return GetMatrix(IdToPos(id));
        }

        public Matrix4x4 GetMatrix(Vector2Int tile)
		{
            var tileRect = TileToRect(tile);

            var tileX = tileRect.x / (float)width * 2 - 1;
            var tileY = 1 - (tileRect.y + tileRect.height) / (float)height * 2;
            var tileWidth = tileRect.width * 2 / (float)width;
            var tileHeight = tileRect.height * 2 / (float)height;

            return Matrix4x4.TRS(new Vector3(tileX, tileY, 0), Quaternion.identity, new Vector3(tileWidth, tileHeight, 0));
        }

        public void Clear()
		{
            _tilePool = new LruCache(regionSize.x * regionSize.y);

            if (tileTextures != null)
            {
                var cmd = CommandBufferPool.Get();
                cmd.SetRenderTarget(tileBuffers, tileDepthBuffer);
                cmd.ClearRenderTarget(true, true, Color.clear);
                Graphics.ExecuteCommandBuffer(cmd);
            }
        }

		public void Dispose()
		{
            if (tileTextures != null)
            {
                for (int i = 0; i < tileTextures.Length; i++)
                    RenderTexture.ReleaseTemporary(tileTextures[i]);

                tileTextures = null;
            }
        }
	}
}