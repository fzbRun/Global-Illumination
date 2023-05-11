using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

//只为RenderingLayerMaskFieldAttribute进行GUI绘制
[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    void Draw(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        bool isUint = property.type == "uint";
        if (isUint && mask == int.MaxValue)
        {
            mask = -1;
        }
        //在创建一个下拉框
        mask = EditorGUI.MaskField(position, label, mask, GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }
        EditorGUI.showMixedValue = false;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Draw(position, property, label);
    }

    public static void Draw(SerializedProperty property, GUIContent label)
    {
        new RenderingLayerMaskDrawer().Draw(EditorGUILayout.GetControlRect(), property, label);
    }

}
