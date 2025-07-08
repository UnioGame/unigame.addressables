namespace UniGame.AddressableTools.Runtime
{
    using System;
    using UnityEngine.AddressableAssets;

    [Serializable]
    public class AssetReferenceGameObjectValue 
    {
        public bool makeImmortal;
        public AssetReferenceGameObject assetReference;
    }
}