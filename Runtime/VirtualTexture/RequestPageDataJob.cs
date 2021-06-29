using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public sealed class RequestPageDataJob
    {
        /// <summary>
        /// 渲染完成的事件回调.
        /// </summary>
        public event Action<RequestPageData> startRenderJob;

        /// <summary>
        /// 渲染取消的事件回调.
        /// </summary>
        public event Action<RequestPageData> cancelRenderJob;

        /// <summary>
        /// 一帧最多处理几个
        /// </summary>
        [SerializeField]
        public int limit = 2;

        /// <summary>
        /// 等待处理的请求.
        /// </summary>
        private List<RequestPageData> _pendingRequests = new List<RequestPageData>();

        public void Update()
        {
            if (startRenderJob != null && _pendingRequests.Count > 0)
            {
                _pendingRequests.Sort((x, y) => { return x.mipLevel.CompareTo(y.mipLevel); });

                for (int i = 0; i < limit && _pendingRequests.Count > 0; i++)
                {
                    var req = _pendingRequests[_pendingRequests.Count - 1];
                    _pendingRequests.RemoveAt(_pendingRequests.Count - 1);

                    startRenderJob.Invoke(req);
                }
            }
        }

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
        /// 新建渲染请求
        /// </summary>
        public RequestPageData Request(int x, int y, int mip)
        {
            // 是否已经在请求队列中
            if (this.Find(x, y, mip) == null)
			{
                // 加入待处理列表
                var request = new RequestPageData(x, y, mip);
                _pendingRequests.Add(request);

                return request;
            }

            return null;
        }

        public void ClearJob()
        {
            foreach (var r in _pendingRequests)
                cancelRenderJob?.Invoke(r);

            _pendingRequests.Clear();
        }
    }
}