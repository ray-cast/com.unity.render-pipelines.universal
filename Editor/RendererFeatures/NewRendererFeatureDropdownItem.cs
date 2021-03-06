using UnityEditor;

namespace UnityEngine.Rendering.Universal
{
    internal static class NewRendererFeatureDropdownItem
    {
        [MenuItem("Assets/Create/Rendering/Universal Render Pipeline/Renderer Feature", priority = EditorUtils.lwrpAssetCreateMenuPriorityGroup2)]
        internal static void CreateNewRendererFeature()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(ResourceGuid.rendererTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, ResourceGuid.defaultNewClassName);
        }

        public static class ResourceGuid
        {
            public static readonly string defaultNewClassName = "CustomRenderPassFeature.cs";
            public static readonly string rendererTemplate = "816ca0f127702564a9df40654a069f25";
        }
    }
}