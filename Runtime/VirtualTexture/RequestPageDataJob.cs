using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public sealed class RequestPageDataJob
    {
        /// <summary>
        /// 一帧最多处理几个
        /// </summary>
        [SerializeField]
        public int limit = 2;

        /// <summary>
        /// 渲染完成的事件回调.
        /// </summary>
        public event Func<CommandBuffer, RequestPageData, bool> startRequestPageJob;

        /// <summary>
        /// 渲染取消的事件回调.
        /// </summary>
        public event Action<RequestPageData> cancelRequestPageJob;

        /// <summary>
        /// 请求池.
        /// </summary>
        private static readonly ObjectPool<RequestPageData> _pendingRequestPool = new ObjectPool<RequestPageData>(null, null);

        /// <summary>
        /// 等待处理的请求.
        /// </summary>
        private List<RequestPageData> _pendingRequests = new List<RequestPageData>();

        /// <summary>
        /// 搜索页面请求
        /// </summary>
        public RequestPageData Find(int x, int y, int mip)
        {
            foreach (var req in _pendingRequests)
            {
                if (req.pageX == x && req.pageY == y && req.mipLevel == mip)
                    return req;
            }

            return null;
        }

        /// <summary>
        /// 新建页面请求
        /// </summary>
        public RequestPageData Request(int x, int y, int mip)
        {
            // 是否已经在请求队列中
            if (this.Find(x, y, mip) == null)
			{
                // 加入待处理列表
                var request = _pendingRequestPool.Get();
                request.pageX = x;
                request.pageY = y;
                request.mipLevel = mip;

                _pendingRequests.Add(request);

                return request;
            }

            return null;
        }

        /// <summary>
        /// 清除所有的页面请求作业
        /// </summary>
        public void Clear()
        {
            if (cancelRequestPageJob != null)
			{
                foreach (var r in _pendingRequests)
                    cancelRequestPageJob?.Invoke(r);
            }

            _pendingRequests.Clear();
        }

        public void Update(CommandBuffer cmd)
        {
            if (startRequestPageJob != null && _pendingRequests.Count > 0)
            {
                _pendingRequests.Sort((x, y) => { return x.mipLevel.CompareTo(y.mipLevel); });

                for (int i = 0; i < limit && _pendingRequests.Count > 0; i++)
                {
                    var req = _pendingRequests[_pendingRequests.Count - 1];

                    if (startRequestPageJob(cmd, req))
					{
                        _pendingRequests.RemoveAt(_pendingRequests.Count - 1);
                        _pendingRequestPool.Release(req);
                    }
                }
            }
        }
    }
}