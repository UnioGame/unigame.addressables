using Object = UnityEngine.Object;

namespace UniGame.AddressableTools.Editor
{
    using System.Collections.Generic;
    using System.Reflection;
    using UniModules.Editor;
    using UniModules.UniCore.EditorTools.Editor.PropertiesDrawers;
    using UniGame.Runtime.Utils;
    using Runtime;
    using UniModules.UniGame.Editor.DrawersTools;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using Object = Object;

#if ODIN_INSPECTOR
#endif
    
    [CustomPropertyDrawer(typeof(DrawAssetReferenceAttribute),true)]
    public class AddressableAssetInspector : PropertyDrawer
    {
        private static MemorizeItem<string, Object> AssetsCache = MemorizeTool
            .Memorize<string, Object>(guid =>
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, mainType);
                return asset;
            });

        private static MemorizeItem<FieldInfo,PropertyDrawer> DrawersCache = MemorizeTool
            .Memorize<FieldInfo,PropertyDrawer>(fieldInfo =>
            {
                var drawerType = typeof(AssetReference);
                var drawer     = fieldInfo.GetDrawer(drawerType);
                return drawer;
            });

        private const float FieldHeight = 0;
        private const string assetLabel = "Asset";
        private const string guiPropertyName = "m_AssetGUID";

        private static Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();

#if ODIN_INSPECTOR
        private bool odinSupport = true;  
#else 
        private bool odinSupport = false;
#endif

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => FieldHeight + base.GetPropertyHeight(property, label);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, new GUIContent(), property);

            if (property.isArray) {
                var targetRect = position;
                for (int i = 0; i < property.arraySize; i++) {
                    var item = property.GetArrayElementAtIndex(i);
                    DrawAssetReferenceInspector(targetRect,item,i,label);
                    targetRect.x += GetPropertyHeight(item, label);
                    position.height += targetRect.height;
                    EditorGUILayout.Separator();
                }
            }
            else {
                DrawAssetReferenceInspector(position, property, 0,label);
            }


            EditorGUI.EndProperty();
        }

        public void DrawAssetReferenceInspector(Rect rect, SerializedProperty property,int index, GUIContent label)
        {
            _foldouts.TryGetValue(index, out var foldout);
            
            foldout = DrawAssetReferenceDrawer(rect, property,foldout, label);
            
            DrawOnGuiAssetReferenceInspector(rect,property,foldout);

            _foldouts[index] = foldout;
        }

        private bool DrawAssetReferenceDrawer(Rect rect, SerializedProperty property,bool foldout, GUIContent label)
        {
            if (fieldInfo == null) return foldout;

            var foldoutRect = rect;
            foldoutRect.x -= 12;
            foldout = EditorGUI.Foldout(foldoutRect, foldout, string.Empty);
            
            var drawer = DrawersCache[fieldInfo];
            
            drawer.OnGUI(rect,property,label);

            return foldout;
        }

        public void DrawOnGuiAssetReferenceInspector(Rect rect, SerializedProperty property,bool foldout)
        {
            if (!foldout) return;
            
            var guidProperty = property.FindPropertyRelative(guiPropertyName);
            var assetGuid    = guidProperty.stringValue;
            
            if (string.IsNullOrEmpty(assetGuid))
                return;

            var asset = AssetsCache[assetGuid];

            if (!odinSupport)
            {
                EditorDrawerUtils.DrawDisabled(() => 
                    EditorGUI.ObjectField(rect,assetLabel, asset, asset != null 
                        ? asset.GetType() 
                        : null,false));
            }

            asset.DrawOdinPropertyField(typeof(Object));
        }

    }
}
