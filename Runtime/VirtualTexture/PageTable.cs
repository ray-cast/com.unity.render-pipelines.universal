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
        /// 页表尺寸.
        /// </summary>
        [SerializeField]
        private int _maxMipLevel = 8;

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
        public int maxMipLevel { get { return _maxMipLevel; } }

        public PageTable(int pageSize)
        {
            _pageSize = pageSize;
            _maxMipLevel = (int)Mathf.Log(pageSize, 2);

            pageLevelTable = new PageLevelTable[maxMipLevel + 1];
            
            for (int i = 0; i <= maxMipLevel; i++)
                pageLevelTable[i] = new PageLevelTable(i, pageSize);
        }

        public TableNodeCell GetPage(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && y >= 0 && mip >= 0);
            Debug.Assert(x < pageSize && y < pageSize && mip <= maxMipLevel);

            return pageLevelTable[mip].Get(x, y);
        }

        public TableNodeCell FindPage(int x, int y, int mip)
		{
            if (mip > maxMipLevel || mip < 0 || x < 0 || y < 0 || x >= pageSize || y >= pageSize)
                return null;

            return pageLevelTable[mip].Get(x, y);
        }

        public void Reset()
        {
            for (int i = 0; i <= maxMipLevel; i++)
            {
                for (int j = 0; j < pageLevelTable[i].nodeCellCount; j++)
                {
                    for (int k = 0; k < pageLevelTable[i].nodeCellCount; k++)
                    {
                        pageLevelTable[i].cell[j, k].payload.ResetTileIndex();
                    }
                }
            }
        }
    }
}