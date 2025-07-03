namespace UniGame.AddressableAtlases
{
    using System;
    using UnityEngine.AddressableAssets;
    using UnityEngine.U2D;

    [Serializable]
    public class AddressableAtlasData
    {
        public string tag = string.Empty;
        public bool enable = true;
        public bool preload;

        public string guid = string.Empty;
        public bool isVariant;
        public int spriteCount;
        public AssetReferenceT<SpriteAtlas> reference;
    }
}