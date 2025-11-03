using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UniGame.AddressableTools.Runtime
{
#if UNITY_EDITOR
    using UnityEditor;
#endif
    
    [Serializable]
    public class AssetReferenceComponentValue<TApi> : 
        AssetReferenceComponent<Component, TApi> where TApi : class
    {
        public AssetReferenceComponentValue(string guid) : base(guid)
        {
        }
    }
    
    [Serializable]
    public class AssetReferenceComponent<TAsset> : 
        AssetReferenceComponent<TAsset, TAsset> where TAsset : Component
    {
        public AssetReferenceComponent(string guid) : base(guid)
        {
        }
    }

    [Serializable]
    public class AssetReferenceComponent<TAsset,TApi> : AssetReferenceT<TAsset> 
        where TAsset : Component
        where TApi : class
    {
#if UNITY_EDITOR
        /// <summary>
        /// Type-specific override of parent editorAsset.  Used by the editor to represent the main asset referenced.
        /// </summary>
        /// <returns>Editor Asset as type TObject, else null</returns>
        public new TAsset editorAsset
        {
            get
            {
                if (CachedAsset != null || string.IsNullOrEmpty(AssetGUID) && CachedAsset is GameObject)
                {
                    var gameObject = CachedAsset as GameObject;
                    var componentObject = CachedAsset as TAsset;
                    return componentObject != null ? componentObject : gameObject?.GetComponent<TAsset>();
                }
                
                var assetPath = AssetDatabase.GUIDToAssetPath(AssetGUID);
                var mainType  = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                
                CachedAsset = AssetDatabase.LoadAssetAtPath(assetPath, mainType);
                
                var baseAsset = CachedAsset as GameObject;
                var componentAsset = CachedAsset as TAsset;

                return componentAsset != null 
                    ? componentAsset 
                    : baseAsset?.GetComponent<TAsset>();
            }
        }
#endif
        
        public AssetReferenceComponent(string guid) : base(guid)
        {
        }
        
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
            var componentValue = prefab?.GetComponent<TApi>();
            return componentValue != null;
#else
            return false;
#endif
        }
    }
}