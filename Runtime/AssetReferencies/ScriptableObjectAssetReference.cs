using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UniGame.AddressableTools.Runtime
{
    using Object = UnityEngine.Object;
#if ODIN_INSPECTOR

#endif

    [Serializable]
    public class AssetReferenceScriptableObject<T> : AssetReferenceScriptableObject<ScriptableObject,T>
    {
        public AssetReferenceScriptableObject(string guid) : base(guid) {}
    }
    
    [Serializable]
    public class AssetReferenceScriptableObject<T,TApi> : AssetReferenceT<T> 
        where T : ScriptableObject
    {
        public AssetReferenceScriptableObject(string guid) : base(guid) {}

        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            return asset is T && asset is TApi;
#else
            return false;
#endif
        }

        public override bool ValidateAsset(Object obj)
        {
            return obj is T && obj is TApi;
        }
    }

    [Serializable]
    public class AssetReferenceScriptableObject : AssetReferenceT<ScriptableObject> 
    {
        public AssetReferenceScriptableObject(string guid) : base(guid) {}
    }
}
