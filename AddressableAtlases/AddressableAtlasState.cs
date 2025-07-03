namespace UniGame.AddressableAtlases
{
    using System;
    using UniGame.Runtime.DataFlow;
    using UnityEngine.U2D;

    [Serializable]
    public class AddressableAtlasState
    {
        public string tag;
        public bool isLoaded;
        public SpriteAtlas atlas;
        public LifeTime lifeTime = new();
        
        public AddressableAtlasData atlasData;
    }
}