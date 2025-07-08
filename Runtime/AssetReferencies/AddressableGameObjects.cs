namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using UniGame.Core.Runtime;
    using Object = UnityEngine.Object;

    [Serializable]
    public class AddressableGameObjects
    {
        public List<AssetReferenceGameObjectValue> items = new();
        
        public async UniTask LoadAssets(ILifeTime lifeTime)
        {
            var tasks = items.Select(x => LoadContextItem(x, lifeTime));
            var result = await UniTask.WhenAll(tasks);
        }

        public async UniTask<Object> LoadContextItem(AssetReferenceGameObjectValue source,ILifeTime lifeTime)
        {
            var value = source.assetReference;
            var gameObject = await value.LoadAssetTaskAsync(lifeTime);
            var result = Object.Instantiate(gameObject);
            if (source.makeImmortal)
                Object.DontDestroyOnLoad(result);

            return result;
        }
    }
}