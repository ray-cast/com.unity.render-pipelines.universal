using System;

namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineEditorResources : ScriptableObject
    {
        [Serializable, ReloadGroup]
        public sealed class ShaderResources
        {
            [Reload("Shaders/Autodesk Interactive/Autodesk Interactive.shadergraph")]
            public Shader autodeskInteractivePS;

            [Reload("Shaders/Autodesk Interactive/Autodesk Interactive Transparent.shadergraph")]
            public Shader autodeskInteractiveTransparentPS;

            [Reload("Shaders/Autodesk Interactive/Autodesk Interactive Masked.shadergraph")]
            public Shader autodeskInteractiveMaskedPS;

            [Reload("Shaders/Terrain/TerrainDetailLit.shader")]
            public Shader terrainDetailLitPS;

            [Reload("Shaders/Terrain/WavingGrass.shader")]
            public Shader terrainDetailGrassPS;

            [Reload("Shaders/Terrain/WavingGrassBillboard.shader")]
            public Shader terrainDetailGrassBillboardPS;

            [Reload("Shaders/Nature/SpeedTree7.shader")]
            public Shader defaultSpeedTree7PS;

            [Reload("Shaders/Nature/SpeedTree8.shader")]
            public Shader defaultSpeedTree8PS;
        }

        [Serializable, ReloadGroup]
        public sealed class MaterialResources
        {
            [Reload("Runtime/Materials/Lit.mat")]
            public Material lit;

            [Reload("Runtime/Materials/ParticlesLit.mat")]
            public Material particleLit;

            [Reload("Runtime/Materials/TerrainLit.mat")]
            public Material terrainLit;

            [Reload("Runtime/Materials/TreeLit.mat")]
            public Material treeLit;

            [Reload("Runtime/Materials/NatureLit.mat")]
            public Material natureLit;
        }

        public ShaderResources shaders;
        public MaterialResources materials;
    }
}