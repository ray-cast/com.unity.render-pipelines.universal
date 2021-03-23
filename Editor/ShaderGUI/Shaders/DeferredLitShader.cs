using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    internal class DeferredLitShader : UnityEditor.ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);

            if (GUI.changed)
            {
                Material material = materialEditor.target as Material;

                float depthPrepass = material.GetFloat("_DepthPrepass");
                material.SetShaderPassEnabled("PrepassDepth", depthPrepass > 0.0f ? true : false);
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            float depthPrepass = material.GetFloat("_DepthPrepass");
            material.SetShaderPassEnabled("PrepassDepth", depthPrepass > 0.0f ? true : false);
        }
    }
}