﻿using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    public class RenderTextureJob
    {
        /// <summary>
        /// 渲染完成的事件回调.
        /// </summary>
        public event Action<RenderTextureRequest> startRenderJob;

        /// <summary>
        /// 渲染取消的事件回调.
        /// </summary>
        public event Action<RenderTextureRequest> cancelRenderJob;

        /// <summary>
        /// 一帧最多处理几个
        /// </summary>
        [SerializeField]
        public int limit = 2;

        /// <summary>
        /// 等待处理的请求.
        /// </summary>
        private List<RenderTextureRequest> _pendingRequests = new List<RenderTextureRequest>();

        public void Update()
        {
            if (_pendingRequests.Count <= 0)
                return;

            // 优先处理mipmap等级高的请求
            _pendingRequests.Sort((x, y) => { return x.mipLevel.CompareTo(y.mipLevel); });

            int count = limit;

            while (count > 0 && _pendingRequests.Count > 0)
            {
                count--;
                // 将第一个请求从等待队列移到运行队列
                var req = _pendingRequests[_pendingRequests.Count - 1];
                _pendingRequests.RemoveAt(_pendingRequests.Count - 1);

                // 开始渲染
                startRenderJob?.Invoke(req);
            }
        }

        /// <summary>
        /// 新建渲染请求
        /// </summary>
        public RenderTextureRequest Request(int x, int y, int mip)
        {
            // 是否已经在请求队列中
            foreach (var r in _pendingRequests)
            {
                if (r.pageX == x && r.pageY == y && r.mipLevel == mip)
                    return null;
            }

            // 加入待处理列表
            var request = new RenderTextureRequest(x, y, mip);
            _pendingRequests.Add(request);

            return request;
        }

        public void ClearJob()
        {
            foreach (var r in _pendingRequests)
                cancelRenderJob?.Invoke(r);

            _pendingRequests.Clear();
        }
    }
}