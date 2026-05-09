using UniGame.Runtime.ObjectPool.Extensions;
using UniGame.Core.Runtime;

namespace UniGame.AddressableTools.Runtime
{
    using System;
    using Cysharp.Threading.Tasks;
    using UniGame.Runtime.DataFlow;
    using UniGame.Runtime.ObjectPool;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    public static class AddressablePoolingExtensions
    {
        public static readonly Vector3 WarmupPosition = new Vector3(10000,10000,10000);
       
        
        public static async UniTask<TComponent> CreatePoolAsync<TComponent>(this AssetReferenceT<TComponent> reference, int preload = 0) 
            where TComponent : Component
        {
            var asset = await CreatePoolAsync(reference as AssetReference, preload);
            var component = asset?.GetComponent<TComponent>();
            return component;
        }

        public static void DestroyPool(this AssetReference reference)
        {
            ObjectPool.DestroyPoolByTag(reference.AssetGUID);
        }
        
        public static bool HasPool(this AssetReference reference)
        {
            var pool = ObjectPool.GetPoolByTag(reference.AssetGUID);
            return pool != null;
        }
        
        public static async UniTask<GameObject> CreatePoolAsync(this AssetReferenceT<GameObject> reference, int preload = 0)
        {
            var asset = await CreatePoolAsync(reference as AssetReference, preload);
            return asset;
        }
        
        public static async UniTask<GameObject> CreatePoolAsync(this AssetReference reference, int preload = 0)
        {
            if (!reference.RuntimeKeyIsValid()) return null;
            
            var guid = reference.AssetGUID;
            
            return await CreatePoolAsync(guid, preload);
        }
        
        public static async UniTask<GameObject> CreatePoolAsync(this string reference, int preload = 0)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            
            var guid = reference;
            var pool = ObjectPool.GetPoolByTag(guid);
            if (pool != null) return pool.asset;

            var poolLifeTime = new LifeTime();
            var asset = await reference.LoadAssetTaskAsync<GameObject>(poolLifeTime);

            if (asset == null)
            {
                poolLifeTime.Terminate();
                return null;
            }

            pool = ObjectPool.GetPoolOrCreate(asset,preload);
            pool.lifeTime.AddDispose(poolLifeTime);
            
            ObjectPool.LinkPoolTag(asset, guid);
            
            return asset;
        }

        public static async UniTask<GameObject> WarmUpReference(this AssetReferenceGameObject view,
            ILifeTime lifeTime,
            int count = 0,
            bool activate = false,
            float killDelay = 0.5f)
        {
            return await WarmUp(view,lifeTime, count, activate, killDelay);
        }

        public static async UniTask<GameObject> WarmUp(this AssetReferenceT<GameObject> view,
            ILifeTime lifeTime,
            int count = 0,
            bool activate = false,
            float killDelay = 0.5f)
        {
            var asset = await view.CreatePoolAsync(count);
            if (activate && asset !=null)
            {
                WarmUpSource(asset,lifeTime,killDelay)
                    .AttachExternalCancellation(lifeTime.Token)
                    .Forget();
            }
            
            return asset;
        }
        
        public static async UniTask WarmUpSource(GameObject asset,ILifeTime lifeTime,float delay)
        {
            var pawn = asset.Spawn(WarmupPosition, Quaternion.identity);
            pawn.SetActive(true);
            
            await UniTask.Delay(TimeSpan.FromSeconds(delay))
                .AttachExternalCancellation(lifeTime.Token);
            
            if(pawn == null) return;
            
            pawn.Despawn();
        }
        
        public static async UniTask<GameObject> Spawn(this AssetReferenceT<GameObject> reference)
        {
            return await reference.SpawnByReference<GameObject>();
        }
    
        public static async UniTask<TComponent> Spawn<TComponent>(this AssetReferenceT<TComponent> reference)
            where TComponent : Component
        {
            return await reference.SpawnByReference<TComponent>();
        }
        
        public static async UniTask<GameObject> Spawn(
            this AssetReferenceT<GameObject> objectSource,
            Vector3 position,
            Quaternion rotation, 
            Transform parent = null, 
            bool stayPosition = false)
        {
            var source = await objectSource.SpawnByReference(position,rotation,parent,stayPosition);
            return source;
        }

        public static async UniTask<GameObject> Spawn(this AssetReferenceT<GameObject> objectSource, Transform parent, 
            bool stayPosition = false)
        {
            var source = await objectSource.SpawnByReference(Vector3.zero,Quaternion.identity,parent,stayPosition);
            return source;
        }
    
        public static async UniTask<T> Spawn<T>(this AssetReferenceT<T> objectSource, Transform parent, bool stayPosition)
            where T : Component
        {
            var source = await objectSource.SpawnByReference(Vector3.zero,Quaternion.identity,parent,stayPosition,true);
            return source;
        }
        
        public static async UniTask<T> Spawn<T>(this AssetReferenceT<T> objectSource,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null, 
            bool stayPosition = false)
            where T : Component
        {
            var asset = await objectSource.SpawnByReference(position,rotation,parent,stayPosition);
            return asset;
        }
        
    }
    
}
