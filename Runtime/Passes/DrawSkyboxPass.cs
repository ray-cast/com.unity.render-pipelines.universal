namespace UnityEngine.Rendering.Universal
{
    public class DrawSkyboxPass : ScriptableRenderPass
    {
        static Mesh _icoskyboxMesh = null;

        public DrawSkyboxPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var name = RenderSettings.skybox.name;
            if (name == "Default-Skybox")
			{
                context.DrawSkybox(renderingData.cameraData.camera);
			}
            else
			{
                if (_icoskyboxMesh == null)
                    _icoskyboxMesh = IcoSphereCreator.Create(10, 0.98f); // 0.01 is padding

                CommandBuffer cmd = CommandBufferPool.Get("Draw Skybox");

                var camera = renderingData.cameraData.camera;
                Matrix4x4 matrix = Matrix4x4.Scale(new Vector3(camera.farClipPlane, camera.farClipPlane, camera.farClipPlane));
                matrix.SetColumn(3, new Vector4(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, 1));


                cmd.Clear();
                cmd.DrawMesh(_icoskyboxMesh, matrix, RenderSettings.skybox);

                context.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }
    }
}