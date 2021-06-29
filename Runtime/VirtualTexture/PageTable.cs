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
        public PageLevelTable[] pageLevelTable { get; private set; }

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int pageSize { get { return _pageSize; } }

        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        public int maxMipLevel { get { return (int)Mathf.Log(pageSize, 2); } }

        public PageTable(RequestPageDataJob renderTextureJob)
        {
            pageLevelTable = new PageLevelTable[maxMipLevel + 1];
            
            for (int i = 0; i <= maxMipLevel; i++)
                pageLevelTable[i] = new PageLevelTable(i, pageSize);
        }

        public TableNodeCell FindPage(int x, int y, int mip)
		{
            if (mip > maxMipLevel || mip < 0 || x < 0 || y < 0 || x >= pageSize || y >= pageSize)
                return null;

            return pageLevelTable[mip].Get(x, y);
        }
    }
}