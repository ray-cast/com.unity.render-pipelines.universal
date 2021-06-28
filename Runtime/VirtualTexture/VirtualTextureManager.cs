using System;

namespace UnityEngine.Rendering.Universal
{
	//
	// 摘要:
	//     Virtual Texture manager.
	public static class VirtualTextureManager
	{
		/// <summary>
		/// 画Tile的事件.
		/// </summary>
		public static event Action<RenderTextureRequest, TiledTexture, Vector2Int> beginTileRendering;

		internal static bool InvokeBeginTileRendering(RenderTextureRequest request, TiledTexture tiledTexture, Vector2Int tile)
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