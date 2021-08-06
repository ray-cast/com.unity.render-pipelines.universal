using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    public class MinLimitDrawer : MaterialPropertyDrawer
    {
        float limit;

        public MinLimitDrawer(float value)
        {
            this.limit = value;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            EditorGUI.BeginChangeCheck();

            editor.DefaultShaderProperty(prop, label);

            if (EditorGUI.EndChangeCheck())
                prop.floatValue = Mathf.Max(this.limit, prop.floatValue);
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0;
        }
    }

    public class TexToggleDrawer : MaterialPropertyDrawer
    {
        string keywordName;

        public TexToggleDrawer(string toggleName)
        {
            keywordName = toggleName;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;

            Texture value = editor.TextureProperty(prop, label);

            if (value != null)
                mat.EnableKeyword(keywordName);
            else
                mat.DisableKeyword(keywordName);

            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
            {
                prop.textureValue = value;
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0;
        }
    }

    public class TogglePassDrawer : MaterialPropertyDrawer
    {
        string passName;

        public TogglePassDrawer(string toggleName)
        {
            passName = toggleName;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            bool value = (prop.floatValue != 0.0f);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;

            value = EditorGUI.Toggle(pos, label, value);

            EditorGUI.showMixedValue = false;

            var material = prop.targets[0] as Material;
            var passEnabled = material.GetShaderPassEnabled(passName);

            if (EditorGUI.EndChangeCheck() || passEnabled != (prop.floatValue > 0 ? true : false))
            {
                // Set the new value if it has changed
                prop.floatValue = value ? 1.0f : 0.0f;
                material.SetShaderPassEnabled(passName, prop.floatValue > 0 ? true : false);
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return base.GetPropertyHeight(prop, label, editor);
        }
    }

    public class EqualIfDrawer : MaterialPropertyDrawer
    {
        string propertyName;
        float[] propertyValue = new float[2];

        public EqualIfDrawer(string toggleName, float value)
        {
            propertyName = toggleName;
            propertyValue = new float[1] { value  };
        }

        public EqualIfDrawer(string toggleName, float value1, float value2)
        {
            propertyName = toggleName;
            propertyValue = new float[2] { value1, value2 };
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;

            for (var i = 0; i < propertyValue.Length; i++)
			{
                var condition = mat.GetFloat(propertyName) == propertyValue[i];
                if (condition)
				{
                    editor.DefaultShaderProperty(prop, label);
                    break;
                }
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0;
        }
    }

    public class ShowIfDrawer : MaterialPropertyDrawer
    {
        string propertyName;
        bool condition;

        public ShowIfDrawer(string toggleName)
        {
            propertyName = toggleName;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;
            condition = mat.GetFloat(propertyName) > 0;
            if (condition)
                editor.DefaultShaderProperty(prop, label);
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0;
        }
    }

    public class HideIfDrawer : MaterialPropertyDrawer
    {
        string propertyName;
        bool condition;

        public HideIfDrawer(string toggleName)
        {
            propertyName = toggleName;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;
            condition = !(mat.GetFloat(propertyName) > 0);
            if (condition)
                editor.DefaultShaderProperty(prop, label);
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0;
        }
    }

    public class DisableDrawer : MaterialPropertyDrawer
    {

        public override void OnGUI(Rect position, MaterialProperty prop, String label, MaterialEditor editor)
        {
            EditorGUI.BeginDisabledGroup(true);
            editor.DefaultShaderProperty(position, prop, label);
            EditorGUI.EndDisabledGroup();
        }
        public override float GetPropertyHeight(MaterialProperty prop, String label, MaterialEditor editor)
        {
            return MaterialEditor.GetDefaultPropertyHeight(prop);
        }
    }

    public class PickerDrawer : MaterialPropertyDrawer
    {
        float height = 16;
        float width = 65;

        public PickerDrawer()
        {
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            if (!(prop.type == MaterialProperty.PropType.Color))
            {
                GUIContent c = EditorGUIUtility.TrTextContent(label.text + " used on a non-Vector property: " + prop.name, EditorGUIUtility.IconContent("console.erroricon").image);
                EditorGUI.LabelField(pos, c, EditorStyles.helpBox);
                return;
            }

            float oldLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUIUtility.labelWidth = 0f;

            Rect VectorRect = new Rect(pos) { x = pos.x, width = pos.width - this.width - 3 };
            Rect ColorRect = new Rect(pos) { x = pos.xMax - this.width, y = pos.y, width = this.width, height = this.height };

            var r = Math.Round(prop.colorValue.r * 255);
            var g = Math.Round(prop.colorValue.g * 255);
            var b = Math.Round(prop.colorValue.b * 255);
            var a = prop.colorValue.a;

            label.tooltip = string.Format("RGBA({0},{1},{2},{3})", r, g, b, a);

            var colorLabel = EditorGUI.TextField(VectorRect, label, label.tooltip);
            var colorValue = EditorGUI.ColorField(ColorRect, new GUIContent(), prop.colorValue, true, true, prop.flags == MaterialProperty.PropFlags.HDR);

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
            {
                prop.colorValue = colorValue;
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            height = base.GetPropertyHeight(prop, label, editor);
            return height;
        }
    }

    public class QueueDrawer : MaterialPropertyDrawer
	{
        float height = 16;
        bool alphaClip = false;

        public enum SurfaceType
        {
            Opaque,
            Transparent
        }

        public QueueDrawer()
        {
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUIUtility.labelWidth = 0f;

            float oldLabelWidth = EditorGUIUtility.labelWidth;

            var material = prop.targets[0] as Material;
            var surfaceType = (SurfaceType)prop.floatValue;
            var value = (SurfaceType)EditorGUI.EnumPopup(pos, label, surfaceType);

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck() || alphaClip != material.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                prop.floatValue = (float)value;
                alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON");

                switch (value)
                {
                    case SurfaceType.Opaque:
                        if (alphaClip)
                        {
                            material.renderQueue = (int)RenderQueue.AlphaTest;
                            material.SetOverrideTag("RenderType", "TransparentCutout");
                        }
                        else
                        {
                            material.renderQueue = (int)RenderQueue.Geometry;
                            material.SetOverrideTag("RenderType", "Opaque");
                        }
                        material.SetInt("_ZWrite", 1);
                        material.renderQueue += material.HasProperty("_QueueOffset") ? (int)material.GetFloat("_QueueOffset") : 0;
                        break;
                    case SurfaceType.Transparent:
                        material.SetInt("_ZWrite", 0);
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.renderQueue = (int)RenderQueue.Transparent;
                        material.renderQueue += material.HasProperty("_QueueOffset") ? (int)material.GetFloat("_QueueOffset") : 0;
                        break;
                }
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            height = base.GetPropertyHeight(prop, label, editor);
            return height;
        }
    }

    public class BlendSwitcherDrawer : MaterialPropertyDrawer
    {
        bool _alphaClip = false;
        float _surface = -1.0f;
        string propertyName = null;

        public enum BlendMode
        {
            None,
            Alpha,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
            Premultiply, // Physically plausible transparency mode, implemented as alpha pre-multiply
            Additive,
            Multiply
        }

        public BlendSwitcherDrawer()
        {
        }

        public BlendSwitcherDrawer(string toggleName)
        {
            propertyName = toggleName;
        }

        public override void OnGUI(Rect pos, MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;

            if (propertyName != null)
			{
                var condition = mat.GetFloat(propertyName) > 0;
                if (!condition) return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUIUtility.labelWidth = 0f;

            float oldLabelWidth = EditorGUIUtility.labelWidth;

            var material = prop.targets[0] as Material;
            var blendMode = (BlendMode)prop.floatValue;

            var value = (BlendMode)EditorGUI.EnumPopup(pos, label, blendMode);

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.showMixedValue = false;

            var surface = 0.0f;
            if (material.HasProperty("_Surface"))
                surface = material.GetFloat("_Surface");

            if (EditorGUI.EndChangeCheck() || _alphaClip != material.IsKeywordEnabled("_ALPHATEST_ON") || _surface != surface)
            {
                _surface = surface;
                _alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") || value != BlendMode.None;

                prop.floatValue = (float)value;

                if (surface > 0)
				{
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetInt("_ZWrite", 0);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.renderQueue += material.HasProperty("_QueueOffset") ? (int)material.GetFloat("_QueueOffset") : 0;
                }
                else
				{
                    if (_alphaClip)
                    {
                        material.renderQueue = (int)RenderQueue.AlphaTest;
                        material.renderQueue += material.HasProperty("_QueueOffset") ? (int)material.GetFloat("_QueueOffset") : 0;
                        material.SetOverrideTag("RenderType", "TransparentCutout");
                    }
                    else
                    {
                        material.renderQueue = (int)RenderQueue.Geometry;
                        material.renderQueue += material.HasProperty("_QueueOffset") ? (int)material.GetFloat("_QueueOffset") : 0;
                        material.SetOverrideTag("RenderType", "Opaque");
                    }

                    material.DisableKeyword("_ALPHAMODULATE_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.SetInt("_ZWrite", 1);
                }

                switch (value)
                {
                    case BlendMode.None:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.SetShaderPassEnabled("PrepassDepth", true);
                        break;
                    case BlendMode.Alpha:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.SetShaderPassEnabled("PrepassDepth", false);
                        break;
                    case BlendMode.Premultiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.SetShaderPassEnabled("PrepassDepth", false);
                        break;
                    case BlendMode.Additive:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.SetShaderPassEnabled("PrepassDepth", false);
                        break;
                    case BlendMode.Multiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.EnableKeyword("_ALPHAMODULATE_ON");
                        material.SetShaderPassEnabled("PrepassDepth", false);
                        break;
                }
            }
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            var mat = prop.targets[0] as Material;
            var condition = propertyName == null || mat.GetFloat(propertyName) > 0;
            return condition ? base.GetPropertyHeight(prop, label, editor) : 0;
        }
    }
}