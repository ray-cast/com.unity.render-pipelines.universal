using System;
using UnityEngine;

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
}