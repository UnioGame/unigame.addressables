using UniGame.GameFlow.Runtime;

namespace UniGame.AddressableAtlases
{
    using Cysharp.Threading.Tasks;
    using UnityEngine.U2D;

    public interface IAddressableAtlasService : IGameService
    {
        UniTask<SpriteAtlas> LoadAtlasAsync(string tag);
        void RegisterSpriteAtlas(SpriteAtlas atlas);
    }
}