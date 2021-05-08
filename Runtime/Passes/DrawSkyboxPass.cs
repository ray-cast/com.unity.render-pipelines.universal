namespace UnityEngine.Rendering.Universal
{
    public class DrawSkyboxPass : ScriptableRenderPass
    {
        static Mesh _icoskyboxMesh = null;
        static Material _hdriMaterial = null;

        Mesh icoskyboxMesh
		{
            get
			{
                if (_icoskyboxMesh == null)
                    _icoskyboxMesh = IcoSphereCreator.Create(4, 0.985f); // 0.015 is padding

                return _icoskyboxMesh;
            }
		}

        Material hdriMaterial
		{
            get
			{
                if (_hdriMaterial == null)
                    _hdriMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Skybox/Cubemap"));

                return _hdriMaterial;
            }
		}

        public DrawSkyboxPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            
            VolumeStack stack = VolumeManager.instance.stack;

            var env = stack.GetComponent<VisualEnvironment>();
            if (env.IsActive())
			{
                var skyLightingMode = env.sky.value;
                if (skyLightingMode == SkyMode.HDRISky)
                {
                    var hdriSky = stack.GetComponent<HDRISky>();
                    if (hdriSky.IsActive())
                    {
                        var camera = renderingData.cameraData.camera;
                        Matrix4x4 matrix = Matrix4x4.Scale(new Vector3(camera.farClipPlane, camera.farClipPlane, camera.farClipPlane));
                        matrix.SetColumn(3, new Vector4(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, 1));

                        this.hdriMaterial.SetTexture(ShaderConstants._Tex, hdriSky.HdriSky.value);
                        this.hdriMaterial.SetFloat(ShaderConstants._Exposure, hdriSky.exposure.value);
                        this.hdriMaterial.SetFloat(ShaderConstants._Rotation, hdriSky.rotation.value);
                        this.hdriMaterial.SetColor(ShaderConstants._Tint, hdriSky.color.value);

                        RenderSettings.skybox = this.hdriMaterial;

                        CommandBuffer cmd = CommandBufferPool.Get(ShaderConstants._renderTag);
                        cmd.Clear();
                        cmd.DrawMesh(this.icoskyboxMesh, matrix, this.hdriMaterial);

                        context.ExecuteCommandBuffer(cmd);

                        CommandBufferPool.Release(cmd);
                    }
                    else
                    {
                        RenderSettings.skybox = null;
                    }
                }
            }
            else
			{
                bool isRequireSkybox = RenderSettings.skybox != null;
                if (!isRequireSkybox)
                {
                    if (cameraData.camera.TryGetComponent<Skybox>(out var cameraSkybox))
                        isRequireSkybox |= cameraSkybox.material != null;
                }

                if (isRequireSkybox)
                    context.DrawSkybox(renderingData.cameraData.camera);
            }
        }

        static class ShaderConstants
        {
            public const string _renderTag = "Draw Skybox";

            public static readonly int _Tex = Shader.PropertyToID("_Tex");
            public static readonly int _Tint = Shader.PropertyToID("_Tint");
            public static readonly int _Exposure = Shader.PropertyToID("_Exposure");
            public static readonly int _Rotation = Shader.PropertyToID("_Rotation");
        }
    }
}