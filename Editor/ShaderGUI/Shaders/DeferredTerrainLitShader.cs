using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    internal class DeferredTerrainLitShader : UnityEditor.ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);

            if (GUI.changed)
            {
                Material material = materialEditor.target as Material;
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
        }
    }
}