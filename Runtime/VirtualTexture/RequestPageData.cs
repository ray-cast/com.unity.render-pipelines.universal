namespace UnityEngine.Rendering.Universal
{
	/// <summary>
	/// 渲染请求类.
	/// </summary>
	public sealed class RequestPageData
	{
		/// <summary>
		/// 页表X坐标
		/// </summary>
		public int pageX { get; }

		/// <summary>
		/// 页表Y坐标
		/// </summary>
		public int pageY { get; }

		/// <summary>
		/// mipmap等级
		/// </summary>
		public int mipLevel { get; }

		/// <summary>
		/// 构造函数
		/// </summary>
		public RequestPageData(int x, int y, int mip)
		{
			pageX = x;
			pageY = y;
			mipLevel = mip;
		}
	}
}