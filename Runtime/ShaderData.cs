using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    public class ShaderData : IDisposable
    {
        static ShaderData _instance = null;
        ComputeBuffer _lightDataBuffer = null;
        ComputeBuffer _lightIndicesBuffer = null;

        ComputeBuffer _shadowDataBuffer = null;
        ComputeBuffer _shadowIndicesBuffer = null;

        ShaderData()
        {
        }

        internal static ShaderData instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ShaderData();

                return _instance;
            }
        }

        public void Dispose()
        {
            DisposeBuffer(ref _lightDataBuffer);
            DisposeBuffer(ref _lightIndicesBuffer);
            DisposeBuffer(ref _shadowDataBuffer);
            DisposeBuffer(ref _shadowIndicesBuffer);
        }

        internal ComputeBuffer GetLightDataBuffer(int size)
        {
            return GetOrUpdateBuffer<ShaderInput.LightData>(ref _lightDataBuffer, size);
        }

        internal ComputeBuffer GetLightIndicesBuffer(int size)
        {
            return GetOrUpdateBuffer<int>(ref _lightIndicesBuffer, size);
        }

        internal ComputeBuffer GetShadowDataBuffer(int size)
        {
            return GetOrUpdateBuffer<ShaderInput.ShadowData>(ref _shadowDataBuffer, size);
        }

        internal ComputeBuffer GetShadowIndicesBuffer(int size)
        {
            return GetOrUpdateBuffer<int>(ref _shadowIndicesBuffer, size);
        }

        ComputeBuffer GetOrUpdateBuffer<T>(ref ComputeBuffer buffer, int size) where T : struct
        {
            if (buffer == null)
            {
                buffer = new ComputeBuffer(size, Marshal.SizeOf<T>());
            }
            else if (size > buffer.count)
            {
                buffer.Dispose();
                buffer = new ComputeBuffer(size, Marshal.SizeOf<T>());
            }

            return buffer;
        }

        void DisposeBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Dispose();
                buffer = null;
            }
        }
    }
}
