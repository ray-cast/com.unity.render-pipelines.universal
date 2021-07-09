using System;

namespace UnityEngine.Rendering.Universal
{
    public class TableNodeCell
    {
        public RectInt rect { get; set; }

        public PagePayload payload { get; set; }

        public int mipLevel { get; }

        public TableNodeCell(int x, int y, int width, int height, int mip)
        {
            rect = new RectInt(x, y, width, height);
            mipLevel = mip;
            payload = new PagePayload();
        }
    }

    public class PageLevelTable
    {
        public TableNodeCell[,] cell { get; set; }

        public Vector2Int pageOffset;

        public int mipLevel { get; }
        public int nodeCellCount;
        public int perCellSize;

        public PageLevelTable(int mip, int tableSize)
        {
            pageOffset = Vector2Int.zero;
            mipLevel = mip;
            perCellSize = (int)Mathf.Pow(2, mip);
            nodeCellCount = tableSize / perCellSize;
            cell = new TableNodeCell[nodeCellCount, nodeCellCount];

            for (int i = 0; i < nodeCellCount; i++)
            {
                for(int j = 0; j < nodeCellCount; j++)
                {
                    cell[i,j] = new TableNodeCell(i * perCellSize, j * perCellSize,  perCellSize, perCellSize, mipLevel);
                }
            }
        }

        public void ChangeViewRect(Vector2Int offset, Action<int> InvalidatePage)
        {
            if (Mathf.Abs(offset.x) >= nodeCellCount || Mathf.Abs(offset.y) > nodeCellCount || offset.x % perCellSize != 0 || offset.y % perCellSize != 0)
            {
                for (int i = 0; i < nodeCellCount; i++)
				{
                    for (int j = 0; j < nodeCellCount; j++)
                    {
                        var transXY = GetTransXY(i, j);
                        ref var page = ref cell[transXY.x, transXY.y];
                        page.payload.loadRequest = null;

                        if (page.payload.isReady)
                        {
                            InvalidatePage(page.payload.tileIndex);
                        }
                    }
                }

                pageOffset = Vector2Int.zero;
                return;
            }

            offset.x /= perCellSize;
            offset.y /= perCellSize;
            #region clip map
            if (offset.x > 0)
            {
                for (int i = 0;i < offset.x; i++)
                {
                    for (int j = 0;j < nodeCellCount;j++)
                    {
                        var transXY = GetTransXY(i, j);
                        cell[transXY.x, transXY.y].payload.loadRequest = null;
                        InvalidatePage(cell[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            else if (offset.x < 0)
            {
                for (int i = 1; i <= -offset.x; i++)
                {
                    for (int j = 0; j < nodeCellCount; j++)
                    {
                        var transXY = GetTransXY(nodeCellCount - i, j);
                        ref var page = ref cell[transXY.x, transXY.y];
                        page.payload.loadRequest = null;
                        
                        if (page.payload.isReady)
						{
                            InvalidatePage(page.payload.tileIndex);
                        }
                    }
                }
            }
            if (offset.y > 0)
            {
                for (int i = 0; i < offset.y; i++)
                {
                    for (int j = 0; j < nodeCellCount; j++)
                    {
                        var transXY = GetTransXY(j, i);
                        cell[transXY.x, transXY.y].payload.loadRequest = null;
                        InvalidatePage(cell[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            else if (offset.y < 0)
            {
                for (int i = 1; i <= -offset.y; i++)
                {
                    for (int j = 0; j < nodeCellCount; j++)
                    {
                        var transXY = GetTransXY(j, nodeCellCount - i);
                        cell[transXY.x, transXY.y].payload.loadRequest = null;
                        InvalidatePage(cell[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            #endregion
            pageOffset += offset;
            
            while(pageOffset.x < 0) pageOffset.x += nodeCellCount;
            while (pageOffset.y < 0) pageOffset.y += nodeCellCount;

            pageOffset.x %= nodeCellCount;
            pageOffset.y %= nodeCellCount;
        }

        // 取x/y/mip完全一致的node，没有就返回null
        public TableNodeCell Get(int x, int y)
        {
            x /= perCellSize;
            y /= perCellSize;

            x = (x + pageOffset.x) % nodeCellCount;
            y = (y + pageOffset.y) % nodeCellCount;

            return cell[x, y];
        }

        public RectInt GetInverRect(RectInt rect)
        {
            return new RectInt( rect.xMin - pageOffset.x,
                                rect.yMin - pageOffset.y,
                                rect.width,
                                rect.height);
        }

        private Vector2Int GetTransXY(int x, int y)
        {
            return new Vector2Int((x + pageOffset.x) % nodeCellCount,
                                  (y + pageOffset.y) % nodeCellCount);
        }
    }
}