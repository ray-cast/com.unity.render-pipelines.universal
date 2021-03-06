using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
	public struct AABB
	{
		Vector4 min;
		Vector4 max;
	}

	public struct ClusterData
	{
		public int width;
		public int height;

		public float zNear;
		public float zFar;
		public float fieldOfView;
		public float fieldOfViewY;

		public float sD;
		public float nearK;
		public float logDimY;
		public float logDepth;

		public int clusterDimX;
		public int clusterDimY;
		public int clusterDimZ;
		public int clusterDimXYZ;

		public int clusterThreadGroup;
	}

	public class ClusterSettingPass : ScriptableRenderPass
	{
		private bool _drawMainCamera;

		private ComputeShader _clusterCompute;

		private int _clusterAABBKernel;
		private int _clusterFlagsKernel;
		private int _clusterLightKernel;
		private int _clusterLightClampedKernel;
		private int _clusterLightIndirectKernel;
		private int _clearClusterBuffersKernel;
		private int _clearLightIndexCounterKernel;
		private int _clearLightIndexListKernel;
		private int _computeClusterCountKernel;
		private int _updateIndirectArgumentBuffersKernel;

		private int _clusterAdditionalLightsCount;

		private int _maxComputeWorkGroupUV;
		private int _maxComputeWorkGroupSize;

		private ClusterData _clusterData;

		private RenderTargetHandle _depthAttachment;

		private ProfilingSampler _profilingSampler;

		public ClusterData clusterData
		{
			get
			{
				return _clusterData;
			}
		}

		public ClusterSettingPass(RenderPassEvent evt, ComputeShader clusterCompute)
		{
			this.renderPassEvent = evt;

			this._profilingSampler = new ProfilingSampler(ShaderConstants._profilerTag);

			this._clusterCompute = clusterCompute;
			this._clearClusterBuffersKernel = _clusterCompute.FindKernel("ClearClusterBuffers");
			this._clearLightIndexCounterKernel = _clusterCompute.FindKernel("ClearLightIndexCounter");
			this._clearLightIndexListKernel = _clusterCompute.FindKernel("ClearLightIndexList");
			this._clusterAABBKernel = _clusterCompute.FindKernel("SetupClusterAABBs");
			this._clusterFlagsKernel = _clusterCompute.FindKernel("SetupClusterFlags");
			this._clusterLightKernel = _clusterCompute.FindKernel("AssignLightsToClusters");
			this._computeClusterCountKernel = _clusterCompute.FindKernel("ComputeClusterCount");
			this._updateIndirectArgumentBuffersKernel = _clusterCompute.FindKernel("UpdateIndirectArgumentBuffers");
			this._clusterLightClampedKernel = _clusterCompute.FindKernel("AssignLightsToClustersClamped");

#if UNITY_ANDROID || UNITY_WEBGL || UNITY_IOS || UNITY_STANDALONE_OSX
			this._clusterLightIndirectKernel = _clusterCompute.FindKernel("AssignLightsToClustersIndirect");
#else
			this._clusterLightIndirectKernel = _clusterCompute.FindKernel("AssignLightsToClustersIndirectNoLimit");
#endif

			this._clusterAdditionalLightsCount = 0;

			this._clusterData = new ClusterData();
			this._clusterData.width = 0;
			this._clusterData.height = 0;
			this._clusterData.fieldOfViewY = 0;

			if (SystemInfo.maxComputeWorkGroupSize >= 1024)
				this._maxComputeWorkGroupSize = 1024;
			else if (SystemInfo.maxComputeWorkGroupSize >= 512)
				this._maxComputeWorkGroupSize = 512;
			else if (SystemInfo.maxComputeWorkGroupSize >= 256)
				this._maxComputeWorkGroupSize = 256;
			else if (SystemInfo.maxComputeWorkGroupSize >= 128)
				this._maxComputeWorkGroupSize = 128;

			if (SystemInfo.maxComputeWorkGroupSize >= 1024)
				this._maxComputeWorkGroupUV = 32;
			else if (SystemInfo.maxComputeWorkGroupSize >= 512)
				this._maxComputeWorkGroupUV = 16;
			else if (SystemInfo.maxComputeWorkGroupSize >= 256)
				this._maxComputeWorkGroupUV = 16;
			else if (SystemInfo.maxComputeWorkGroupSize >= 128)
				this._maxComputeWorkGroupUV = 8;
		}

		public void Setup(RenderTargetHandle depthAttachment, bool drawMainCamera = false)
		{
			this._depthAttachment = depthAttachment;
			this._drawMainCamera = drawMainCamera;
		}

		public void Cleanup() => ShaderData.instance.Dispose();

		private void SetupClusterData(ref Camera camera, ref RenderingData renderingData)
		{
			int clusterDimX = Mathf.CeilToInt(camera.pixelWidth / (float)ShaderConstants.blockSizeX);
			int clusterDimY = Mathf.CeilToInt(camera.pixelHeight / (float)ShaderConstants.blockSizeY);
			int clusterNumZ = Mathf.Min(clusterDimX, clusterDimY);

			_clusterData.width = camera.pixelWidth;
			_clusterData.height = camera.pixelHeight;
			_clusterData.zNear = camera.nearClipPlane;
			_clusterData.zFar = Mathf.Min(renderingData.lightData.maxLightingDistance, camera.farClipPlane);
			_clusterData.fieldOfView = Mathf.Max(1, camera.fieldOfView);
			_clusterData.fieldOfViewY = _clusterData.fieldOfView * Mathf.Deg2Rad * 0.5f;
			_clusterData.sD = 2.0f * Mathf.Tan(_clusterData.fieldOfViewY) / (float)clusterNumZ;
			_clusterData.nearK = 1 + _clusterData.sD;
			_clusterData.logDepth = Mathf.Log(_clusterData.zFar / Mathf.Max(1e-5f, _clusterData.zNear));
			_clusterData.logDimY = 1.0f / Mathf.Log(_clusterData.nearK);
			_clusterData.clusterDimX = clusterDimX;
			_clusterData.clusterDimY = clusterDimY;
			_clusterData.clusterDimZ = Mathf.FloorToInt(_clusterData.logDepth * _clusterData.logDimY);
			_clusterData.clusterDimXYZ = _clusterData.clusterDimX * _clusterData.clusterDimY * _clusterData.clusterDimZ;
			_clusterData.clusterThreadGroup = Mathf.CeilToInt(_clusterData.clusterDimXYZ / (float)this._maxComputeWorkGroupSize);

			ShaderData.instance.CreateAABBBuffer(_clusterData.clusterThreadGroup * this._maxComputeWorkGroupSize);
			ShaderData.instance.CreateFlagBuffer(_clusterData.clusterThreadGroup * this._maxComputeWorkGroupSize);
			ShaderData.instance.CreateUniqueBuffer(_clusterData.clusterThreadGroup * this._maxComputeWorkGroupSize);
			ShaderData.instance.CreateLightGridBuffer(_clusterData.clusterThreadGroup * this._maxComputeWorkGroupSize);
		}

		void SetupClusterAABB(ref CommandBuffer cmd, ref Camera camera)
		{
			var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
			var projectionMatrixInvers = projectionMatrix.inverse;

			var sizeParams = new Vector2(ShaderConstants.blockSizeX, ShaderConstants.blockSizeY);
			var dimensionParams = new Vector4(_clusterData.clusterDimX, _clusterData.clusterDimY, _clusterData.clusterDimZ, 0.0f);
			var projectionParams = new Vector4(_clusterData.zNear, _clusterData.zFar, _clusterData.nearK, _clusterData.logDimY);
			var screenDim = new Vector4(_clusterData.width, _clusterData.height, 1.0f / _clusterData.width, 1.0f / _clusterData.height);

			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterSizeParams, sizeParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterDimensionParams, dimensionParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterProjectionParams, projectionParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterScreenDimensionParams, screenDim);
			cmd.SetComputeMatrixParam(_clusterCompute, ShaderConstants._TransformToViewMatrix, projectionMatrixInvers);
			cmd.SetComputeBufferParam(_clusterCompute, _clusterAABBKernel, ShaderConstants._RWClusterBoxBuffer, ShaderData.instance.AABBBuffer);
			cmd.DispatchCompute(_clusterCompute, _clusterAABBKernel, _clusterData.clusterThreadGroup, 1, 1);
		}

		void ClearLightGirdIndexCounter(ref CommandBuffer cmd, ref RenderingData renderingData)
		{
			cmd.SetComputeBufferParam(_clusterCompute, _clearLightIndexCounterKernel, ShaderConstants._RWClusterUniqueCounterBuffer, ShaderData.instance.uniquesCounterBuffer);
			cmd.SetComputeBufferParam(_clusterCompute, _clearLightIndexCounterKernel, ShaderConstants._RWClusterLightIndexCounterBuffer, ShaderData.instance.lightIndexCounterBuffer);
			cmd.SetComputeBufferParam(_clusterCompute, _clearLightIndexListKernel, ShaderConstants._RWClusterLightIndexListBuffer, ShaderData.instance.lightIndexListBuffer);
			cmd.SetComputeBufferParam(_clusterCompute, _clearClusterBuffersKernel, ShaderConstants._RWClusterLightGridBuffer, ShaderData.instance.lightGridBuffer);
			cmd.SetComputeBufferParam(_clusterCompute, _clearClusterBuffersKernel, ShaderConstants._RWClusterFlagBuffer, ShaderData.instance.flagBuffer);
			cmd.SetComputeBufferParam(_clusterCompute, _clearClusterBuffersKernel, ShaderConstants._RWClusterUniqueBuffer, ShaderData.instance.uniquesBuffer);

			cmd.DispatchCompute(_clusterCompute, _clearLightIndexCounterKernel, 1, 1, 1);
			cmd.DispatchCompute(_clusterCompute, _clearClusterBuffersKernel, _clusterData.clusterThreadGroup, 1, 1);
			cmd.DispatchCompute(_clusterCompute, _clearLightIndexListKernel, _clusterData.clusterThreadGroup * renderingData.lightData.maxPerClusterAdditionalLightsCount, 1, 1);
		}

		void InitializeLightConstants(NativeArray<VisibleLight> lights, int lightIndex, out Vector4 lightPos, out Vector4 lightColor, out Vector4 lightAttenuation, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel)
		{
			lightPos = ShaderConstants._DefaultLightPosition;
			lightColor = ShaderConstants._DefaultLightColor;
			lightAttenuation = ShaderConstants._DefaultLightAttenuation;
			lightSpotDir = ShaderConstants._DefaultLightSpotDirection;
			lightOcclusionProbeChannel = ShaderConstants._DefaultLightsProbeChannel;

			if (lightIndex < 0)
				return;

			VisibleLight lightData = lights[lightIndex];
			if (lightData.lightType == LightType.Directional)
			{
				Vector4 dir = -lightData.localToWorldMatrix.GetColumn(2);
				lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
				lightColor = new Vector4(lightData.finalColor.r, lightData.finalColor.g, lightData.finalColor.b, 0.0f);
			}
			else
			{
				Vector4 pos = lightData.localToWorldMatrix.GetColumn(3);
				lightPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
				lightColor = new Vector4(lightData.finalColor.r, lightData.finalColor.g, lightData.finalColor.b, lightData.range);
			}

			if (lightData.lightType != LightType.Directional)
			{
				float lightRangeSqr = lightData.range * lightData.range;
				float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
				float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
				float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
				float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
				float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightData.range * lightData.range);

				lightAttenuation.x = Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch ? oneOverFadeRangeSqr : oneOverLightRangeSqr;
				lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;
			}
			
			if (lightData.lightType == LightType.Spot)
			{
				Vector4 dir = lightData.localToWorldMatrix.GetColumn(2);
				lightSpotDir = new Vector4(-dir.x, -dir.y, -dir.z, Mathf.Deg2Rad * lightData.spotAngle * 0.5f);

				float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * lightData.spotAngle * 0.5f);
				float cosInnerAngle;
				if (lightData.light != null)
					cosInnerAngle = Mathf.Cos(lightData.light.innerSpotAngle * Mathf.Deg2Rad * 0.5f);
				else
					cosInnerAngle = Mathf.Cos((2.0f * Mathf.Atan(Mathf.Tan(lightData.spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f)) * 0.5f);
				float smoothAngleRange = Mathf.Max(0.001f, cosInnerAngle - cosOuterAngle);
				float invAngleRange = 1.0f / smoothAngleRange;
				float add = -cosOuterAngle * invAngleRange;
				lightAttenuation.z = invAngleRange;
				lightAttenuation.w = add;
			}

			Light light = lightData.light;

			int occlusionProbeChannel = light != null ? light.bakingOutput.occlusionMaskChannel : -1;
			lightOcclusionProbeChannel.x = occlusionProbeChannel == -1 ? 0f : occlusionProbeChannel;
			lightOcclusionProbeChannel.y = occlusionProbeChannel == -1 ? 1f : 0f;
		}

		void SetupAdditionalLightConstants(ref CommandBuffer cmd, ref RenderingData renderingData)
		{
			ref LightData lightData = ref renderingData.lightData;
			var lights = lightData.visibleLights;
			var additionalLightsCount = 0;
			var maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;

			var additionalLightsData = new NativeArray<ShaderInput.LightData>(lightData.additionalLightsCount, Allocator.Temp);

			for (int i = 0; i < lights.Length && additionalLightsCount < maxAdditionalLightsCount; ++i)
			{
				VisibleLight it = lights[i];
				if (lightData.mainLightIndex != i)
				{
					ShaderInput.LightData data;
					InitializeLightConstants(lights, i,
									out data.position, out data.color, out data.attenuation,
									out data.spotDirection, out data.occlusionProbeChannels);
					additionalLightsData[additionalLightsCount] = data;
					additionalLightsCount++;
				}
			}

			_clusterAdditionalLightsCount = additionalLightsCount;

			ShaderData.instance.additionalLightsBuffer.SetData(additionalLightsData);
			
			additionalLightsData.Dispose();
		}

		void SetupClusterFlags(ref CommandBuffer cmd, ref Camera camera)
		{
			var width = camera.pixelWidth;
			var height = camera.pixelHeight;

			var sizeParams = new Vector2(ShaderConstants.blockSizeX, ShaderConstants.blockSizeY);
			var screenDimParams = new Vector4((float)width, (float)height, 1.0f / width, 1.0f / height);
			var dimensionParams = new Vector4(_clusterData.clusterDimX, _clusterData.clusterDimY, _clusterData.clusterDimZ, 0.0f);
			var projectionParams = new Vector4(_clusterData.zNear, _clusterData.zFar, _clusterData.nearK, _clusterData.logDimY);

			float near = camera.nearClipPlane;
			float far = camera.farClipPlane;
			float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
			float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
			float isOrthographic = camera.orthographic ? 1.0f : 0.0f;

			// From http://www.humus.name/temp/Linearize%20depth.txt
			// But as depth component textures on OpenGL always return in 0..1 range (as in D3D), we have to use
			// the same constants for both D3D and OpenGL here.
			// OpenGL would be this:
			// zc0 = (1.0 - far / near) / 2.0;
			// zc1 = (1.0 + far / near) / 2.0;
			// D3D is this:
			float zc0 = 1.0f - far * invNear;
			float zc1 = far * invNear;

			Vector4 zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

			if (SystemInfo.usesReversedZBuffer)
			{
				zBufferParams.y += zBufferParams.x;
				zBufferParams.x = -zBufferParams.x;
				zBufferParams.w += zBufferParams.z;
				zBufferParams.z = -zBufferParams.z;
			}

			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterSizeParams, sizeParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterDimensionParams, dimensionParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterProjectionParams, projectionParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterScreenDimensionParams, screenDimParams);
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterZBufferParams, zBufferParams); 
			cmd.SetComputeBufferParam(_clusterCompute, _clusterFlagsKernel, ShaderConstants._RWClusterFlagBuffer, ShaderData.instance.flagBuffer);

			cmd.DispatchCompute(_clusterCompute, _clusterFlagsKernel, Mathf.CeilToInt(width / (float)_maxComputeWorkGroupUV), Mathf.CeilToInt(height / (float)_maxComputeWorkGroupUV), 1);
		}

		void SetupAssignLightsToClusts(ref CommandBuffer cmd, ref RenderingData renderingData, ref Camera camera)
		{
			cmd.SetComputeVectorParam(_clusterCompute, ShaderConstants._ClusterLightParams, new Vector4(_clusterAdditionalLightsCount, renderingData.lightData.maxPerClusterAdditionalLightsCount, 0.0f, 0.0f));
			cmd.SetComputeMatrixParam(_clusterCompute, ShaderConstants._InverseViewMatrix, camera.transform.worldToLocalMatrix);

			if (this._drawMainCamera)
			{
				cmd.SetComputeBufferParam(_clusterCompute, _clusterLightKernel, ShaderConstants._ClusterLightBuffer, ShaderData.instance.additionalLightsBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _clusterLightKernel, ShaderConstants._ClusterAABBBuffer, ShaderData.instance.AABBBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _clusterLightKernel, ShaderConstants._RWClusterLightGridBuffer, ShaderData.instance.lightGridBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _clusterLightKernel, ShaderConstants._RWClusterLightIndexCounterBuffer, ShaderData.instance.lightIndexCounterBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _clusterLightKernel, ShaderConstants._RWClusterLightIndexListBuffer, ShaderData.instance.lightIndexListBuffer);

				cmd.DispatchCompute(_clusterCompute, _clusterLightKernel, _clusterData.clusterThreadGroup, 1, 1);
			}
			else
			{
				cmd.SetComputeBufferParam(_clusterCompute, _computeClusterCountKernel, ShaderConstants._ClusterFlagBuffer, ShaderData.instance.flagBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _computeClusterCountKernel, ShaderConstants._RWClusterUniqueBuffer, ShaderData.instance.uniquesBuffer);
				cmd.SetComputeBufferParam(_clusterCompute, _computeClusterCountKernel, ShaderConstants._RWClusterUniqueCounterBuffer, ShaderData.instance.uniquesCounterBuffer);
				cmd.DispatchCompute(_clusterCompute, _computeClusterCountKernel, _clusterData.clusterThreadGroup, 1, 1);

				if (this._maxComputeWorkGroupSize < 1024 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
				{
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._ClusterLightBuffer, ShaderData.instance.additionalLightsBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._ClusterAABBBuffer, ShaderData.instance.AABBBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._ClusterUniquesBuffer, ShaderData.instance.uniquesBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._ClusterUniqueCounterBuffer, ShaderData.instance.uniquesCounterBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._RWClusterLightGridBuffer, ShaderData.instance.lightGridBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._RWClusterLightIndexCounterBuffer, ShaderData.instance.lightIndexCounterBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightClampedKernel, ShaderConstants._RWClusterLightIndexListBuffer, ShaderData.instance.lightIndexListBuffer);
					cmd.DispatchCompute(_clusterCompute, _clusterLightClampedKernel, _clusterData.clusterThreadGroup, 1, 1);
				}
				else
				{
					var indirectArgumentBuffer = ShaderData.instance.CreateIndirectArgumentBuffer(1);
					cmd.SetComputeBufferParam(_clusterCompute, _updateIndirectArgumentBuffersKernel, ShaderConstants._ClusterUniqueCounterBuffer, ShaderData.instance.uniquesCounterBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _updateIndirectArgumentBuffersKernel, ShaderConstants._RWClusterIndirectArgumentBuffer, indirectArgumentBuffer);
					cmd.DispatchCompute(_clusterCompute, _updateIndirectArgumentBuffersKernel, 1, 1, 1);

					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._ClusterLightBuffer, ShaderData.instance.additionalLightsBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._ClusterAABBBuffer, ShaderData.instance.AABBBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._ClusterUniquesBuffer, ShaderData.instance.uniquesBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._ClusterUniqueCounterBuffer, ShaderData.instance.uniquesCounterBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._RWClusterLightGridBuffer, ShaderData.instance.lightGridBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._RWClusterLightIndexCounterBuffer, ShaderData.instance.lightIndexCounterBuffer);
					cmd.SetComputeBufferParam(_clusterCompute, _clusterLightIndirectKernel, ShaderConstants._RWClusterLightIndexListBuffer, ShaderData.instance.lightIndexListBuffer);
					cmd.DispatchCompute(_clusterCompute, _clusterLightIndirectKernel, indirectArgumentBuffer, 0);
				}
			}
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(ShaderConstants._profilerTag);
			var camera = this._drawMainCamera ? Camera.main : renderingData.cameraData.camera;

			if (this._clusterData.width != camera.pixelWidth || this._clusterData.height != camera.pixelHeight ||
				this._clusterData.zNear != camera.nearClipPlane ||
				this._clusterData.zFar != Mathf.Min(renderingData.lightData.maxLightingDistance, camera.farClipPlane) ||
				this._clusterData.fieldOfView != Mathf.Max(1, camera.fieldOfView))
			{
				this.SetupClusterData(ref camera, ref renderingData);
				this.SetupClusterAABB(ref cmd, ref camera);
			}

			using (new ProfilingScope(cmd, _profilingSampler))
			{
				context.ExecuteCommandBuffer(cmd);

				cmd.Clear();

				ShaderData.instance.CreateUniqueCounterBuffer(1);
				ShaderData.instance.CreateLightDataBuffer(UniversalRenderPipeline.maxVisibleAdditionalLights);
				ShaderData.instance.CreateLightIndexCountBuffer(1);
				ShaderData.instance.CreateLightIndexBuffer(_clusterData.clusterThreadGroup * this._maxComputeWorkGroupSize * renderingData.lightData.maxPerClusterAdditionalLightsCount);

				var sizeParams = new Vector2(ShaderConstants.blockSizeX, ShaderConstants.blockSizeY);
				var dimensionParams = new Vector4(_clusterData.clusterDimX, _clusterData.clusterDimY, _clusterData.clusterDimZ, 0.0f);
				var projectionParams = new Vector4(_clusterData.zNear, _clusterData.zFar, _clusterData.nearK, _clusterData.logDimY);
				var lightParams = new Vector4(_clusterAdditionalLightsCount, renderingData.lightData.maxPerClusterAdditionalLightsCount, 0.0f, 0.0f);
				var screenDimParams = new Vector4(_clusterData.width, _clusterData.height, 1.0f / _clusterData.width, 1.0f / _clusterData.height);

				cmd.SetGlobalVector(ShaderConstants._ClusterSizeParams, sizeParams);
				cmd.SetGlobalVector(ShaderConstants._ClusterDimensionParams, dimensionParams);
				cmd.SetGlobalVector(ShaderConstants._ClusterProjectionParams, projectionParams);
				cmd.SetGlobalVector(ShaderConstants._ClusterScreenDimensionParams, screenDimParams);
				cmd.SetGlobalVector(ShaderConstants._ClusterLightParams, lightParams);
				cmd.SetGlobalBuffer(ShaderConstants._ClusterAABBBuffer, ShaderData.instance.AABBBuffer);
				cmd.SetGlobalBuffer(ShaderConstants._ClusterFlagBuffer, ShaderData.instance.flagBuffer);
				cmd.SetGlobalBuffer(ShaderConstants._ClusterLightGridBuffer, ShaderData.instance.lightGridBuffer);
				cmd.SetGlobalBuffer(ShaderConstants._ClusterLightIndexBuffer, ShaderData.instance.lightIndexListBuffer);
				cmd.SetGlobalBuffer(ShaderConstants._ClusterLightBuffer, ShaderData.instance.additionalLightsBuffer);

				this.ClearLightGirdIndexCounter(ref cmd, ref renderingData);

				this.SetupAdditionalLightConstants(ref cmd, ref renderingData);
				this.SetupClusterFlags(ref cmd, ref renderingData.cameraData.camera);
				this.SetupAssignLightsToClusts(ref cmd, ref renderingData, ref camera);
			}

			context.ExecuteCommandBuffer(cmd);

			CommandBufferPool.Release(cmd);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			if (cmd == null)
				throw new ArgumentNullException("cmd");
		}

		static class ShaderConstants
		{
			public const string _profilerTag = "Cluster Setting Constants";

			public const int blockSizeX = 128;
			public const int blockSizeY = 64;

			public static readonly Vector4 _DefaultLightPosition = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
			public static readonly Vector4 _DefaultLightColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
			public static readonly Vector4 _DefaultLightAttenuation = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
			public static readonly Vector4 _DefaultLightSpotDirection = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
			public static readonly Vector4 _DefaultLightsProbeChannel = new Vector4(-1.0f, 1.0f, -1.0f, -1.0f);

			public static readonly int _InverseViewMatrix = Shader.PropertyToID("_InverseViewMatrix");
			public static readonly int _TransformToViewMatrix = Shader.PropertyToID("_TransformToViewMatrix");

			public static readonly int _ClusterSizeParams = Shader.PropertyToID("_ClusterSizeParams");
			public static readonly int _ClusterDimensionParams = Shader.PropertyToID("_ClusterDimensionParams");
			public static readonly int _ClusterProjectionParams = Shader.PropertyToID("_ClusterProjectionParams");
			public static readonly int _ClusterScreenDimensionParams = Shader.PropertyToID("_ClusterScreenDimensionParams");
			public static readonly int _ClusterLightParams = Shader.PropertyToID("_ClusterLightParams");
			public static readonly int _ClusterZBufferParams = Shader.PropertyToID("_ClusterZBufferParams");

			public static readonly int _ClusterAABBBuffer = Shader.PropertyToID("_ClusterBoxBuffer");
			public static readonly int _ClusterFlagBuffer = Shader.PropertyToID("_ClusterFlagBuffer");
			public static readonly int _ClusterUniquesBuffer = Shader.PropertyToID("_ClusterUniqueBuffer");
			public static readonly int _ClusterUniqueCounterBuffer = Shader.PropertyToID("_ClusterUniqueCounterBuffer");
			public static readonly int _ClusterLightBuffer = Shader.PropertyToID("_ClusterLightBuffer");
			public static readonly int _ClusterLightGridBuffer = Shader.PropertyToID("_ClusterLightGridBuffer");
			public static readonly int _ClusterLightIndexBuffer = Shader.PropertyToID("_ClusterLightIndexBuffer");

			public static readonly int _RWClusterBoxBuffer = Shader.PropertyToID("_RWClusterBoxBuffer");
			public static readonly int _RWClusterFlagBuffer = Shader.PropertyToID("_RWClusterFlagBuffer");
			public static readonly int _RWClusterUniqueBuffer = Shader.PropertyToID("_RWClusterUniqueBuffer");
			public static readonly int _RWClusterUniqueCounterBuffer = Shader.PropertyToID("_RWClusterUniqueCounterBuffer");
			public static readonly int _RWClusterLightGridBuffer = Shader.PropertyToID("_RWClusterLightGridBuffer");
			public static readonly int _RWClusterLightIndexCounterBuffer = Shader.PropertyToID("_RWClusterLightIndexCounterBuffer");
			public static readonly int _RWClusterLightIndexListBuffer = Shader.PropertyToID("_RWClusterLightIndexListBuffer");
			public static readonly int _RWClusterIndirectArgumentBuffer = Shader.PropertyToID("_RWClusterIndirectArgumentBuffer");
		}

		public class ShaderData : IDisposable
		{
			static ShaderData _instance = null;

			private ComputeBuffer _clusterUniques;
			private ComputeBuffer _clusterUniquesCounter;
			private ComputeBuffer _clusterFlags;
			private ComputeBuffer _clusterAABBs;

			private ComputeBuffer _clusterLightGrid;
			private ComputeBuffer _clusterLightIndexList;
			private ComputeBuffer _clusterLightIndexCounter;
			private ComputeBuffer _clusterAdditionalLightsBuffer;

			private ComputeBuffer _clusterIndirectArgumentBuffer;

			public ComputeBuffer uniquesBuffer { get => _clusterUniques; }
			public ComputeBuffer uniquesCounterBuffer { get => _clusterUniquesCounter; }
			public ComputeBuffer flagBuffer { get => _clusterFlags; }
			public ComputeBuffer AABBBuffer { get => _clusterAABBs; }
			public ComputeBuffer lightGridBuffer { get => _clusterLightGrid; }
			public ComputeBuffer lightIndexListBuffer { get => _clusterLightIndexList; }
			public ComputeBuffer lightIndexCounterBuffer { get => _clusterLightIndexCounter; }
			public ComputeBuffer additionalLightsBuffer { get => _clusterAdditionalLightsBuffer; }

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
				DisposeBuffer(ref _clusterFlags);
				DisposeBuffer(ref _clusterAABBs);
				DisposeBuffer(ref _clusterUniques);
				DisposeBuffer(ref _clusterUniquesCounter);
				DisposeBuffer(ref _clusterIndirectArgumentBuffer);
				DisposeBuffer(ref _clusterLightGrid);
				DisposeBuffer(ref _clusterLightIndexList);
				DisposeBuffer(ref _clusterLightIndexCounter);
				DisposeBuffer(ref _clusterAdditionalLightsBuffer);
			}

			internal ComputeBuffer CreateAABBBuffer(int size)
			{
				return GetOrUpdateBuffer<AABB>(ref _clusterAABBs, size);
			}

			internal ComputeBuffer CreateFlagBuffer(int size)
			{
				return GetOrUpdateBuffer<uint>(ref _clusterFlags, size);
			}

			internal ComputeBuffer CreateUniqueBuffer(int size)
			{
				return GetOrUpdateBuffer<uint>(ref _clusterUniques, size);
			}

			internal ComputeBuffer CreateUniqueCounterBuffer(int size)
			{
				return GetOrUpdateBuffer<uint>(ref _clusterUniquesCounter, size);
			}

			internal ComputeBuffer CreateLightGridBuffer(int size)
			{
				return GetOrUpdateBuffer(ref _clusterLightGrid, size, sizeof(uint) * 2);
			}

			internal ComputeBuffer CreateLightIndexBuffer(int size)
			{
				return GetOrUpdateBuffer<uint>(ref _clusterLightIndexList, size);
			}

			internal ComputeBuffer CreateLightIndexCountBuffer(int size)
			{
				return GetOrUpdateBuffer<uint>(ref _clusterLightIndexCounter, size);
			}

			internal ComputeBuffer CreateLightDataBuffer(int size)
			{
				return GetOrUpdateBuffer<ShaderInput.LightData>(ref _clusterAdditionalLightsBuffer, size);
			}

			internal ComputeBuffer CreateIndirectArgumentBuffer(int size)
			{
				return GetOrUpdateBuffer(ref _clusterIndirectArgumentBuffer, size, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
			}

			ComputeBuffer GetOrUpdateBuffer(ref ComputeBuffer buffer, int size, int stride, ComputeBufferType type = ComputeBufferType.Default)
			{
				if (buffer == null)
				{
					buffer = new ComputeBuffer(size, stride, type);
				}
				else if (size > buffer.count)
				{
					buffer.Dispose();
					buffer = new ComputeBuffer(size, stride, type);
				}

				return buffer;
			}

			ComputeBuffer GetOrUpdateBuffer<T>(ref ComputeBuffer buffer, int size, ComputeBufferType type = ComputeBufferType.Default) where T : struct
			{
				return GetOrUpdateBuffer(ref buffer, size, Marshal.SizeOf<T>(), type);
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
}