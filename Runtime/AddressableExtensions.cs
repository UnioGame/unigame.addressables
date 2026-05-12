namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UniGame.Runtime.ObjectPool;
    using UniGame.Runtime.ObjectPool.Extensions;
    using Core.Runtime;
    using Core.Runtime.Extension;
    using UniCore.Runtime.ProfilerTools;
    using UniGame.Runtime.DataFlow;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.Pool;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceLocations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    public static class AddressableExtensions
    {
        private static Dictionary<string, AddressableLoadState> _assetTaskCache = new();
        private static Dictionary<string, UniTaskCompletionSource<AddressableLoadState>> _dependenciesTaskCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAddressableData()
        {
            _assetTaskCache.Clear();
            _dependenciesTaskCache.Clear();
            _resourceLocations.Clear();
        }

        private static object EvaluateKey(object obj)
        {
            if (obj is IKeyEvaluator evaluator)
                return evaluator.RuntimeKey;
            return obj;
        }

        private static HashSet<IResourceLocation> _resourceLocations = new();

        public static bool GetResourceLocations(object key, List<IResourceLocation> locations)
        {
            var resourceLocators = Addressables.ResourceLocators;
            var requiredType = typeof(Object);

            key = EvaluateKey(key);

            _resourceLocations.Clear();

            foreach (var locator in resourceLocators)
            {
                if (!locator.Locate(key, requiredType, out var locs))
                    continue;
                _resourceLocations.UnionWith(locs);
            }

            locations.AddRange(_resourceLocations);
            _resourceLocations.Clear();

            return true;
        }

        public static async UniTask<SceneInstance> LoadSceneTaskAsync(
            this AssetReference sceneReference,
            ILifeTime lifeTime,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            bool activateOnLoad = true,
            int priority = 100,
            IProgress<float> progress = null)
        {
            if (sceneReference.RuntimeKeyIsValid() == false)
            {
                GameLog.LogError($"AssetReference key is NULL {sceneReference}");
                return default;
            }

            return await LoadSceneTaskAsync(sceneReference.AssetGUID,
                lifeTime, loadSceneMode, activateOnLoad,
                priority, progress);
        }

        public static async UniTask<SceneInstance> LoadSceneTaskAsync(
            this string sceneReference,
            ILifeTime lifeTime,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            bool activateOnLoad = true,
            int priority = 100,
            IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(sceneReference))
            {
                GameLog.LogError($"AssetReference key is NULL {sceneReference}");
                return default;
            }

            var sceneHandle = Addressables
                .LoadSceneAsync(sceneReference, loadSceneMode, activateOnLoad, priority);

            //add to resource unloading
            sceneHandle.AddTo(lifeTime);

            // await sceneHandle.ToUniTask(progress,cancellationToken:lifeTime.Token);

            while (!sceneHandle.IsDone && !lifeTime.IsTerminated)
            {
                progress?.Report(sceneHandle.PercentComplete);
                await UniTask.Yield(lifeTime.Token);
            }

            if (sceneHandle.Status == AsyncOperationStatus.Succeeded)
            {
                lifeTime.AddCleanUpAction(() => Addressables
                    .UnloadSceneAsync(sceneHandle, true)
                    .ToUniTask()
                    .Forget());
            }

            return sceneHandle.Status == AsyncOperationStatus.Succeeded
                ? sceneHandle.Result
                : default;
        }

        public static void UnloadReference(this AssetReference reference)
        {
            // if(reference.Asset is IDisposable disposable)
            //     disposable.Dispose();
            //
            reference.ReleaseAsset();
        }

        public static async UniTask<IEnumerable<TSource>> LoadAssetsTaskAsync<TSource, TAsset>(
            this IEnumerable<TAsset> assetReference,
            List<TSource> resultContainer,
            ILifeTime lifeTime)
            where TAsset : AssetReference
            where TSource : Object
        {
            return await assetReference.LoadAssetsTaskAsync<TSource, TSource, TAsset>(resultContainer, lifeTime);
        }

        public static async UniTask<IList<T>> LoadAssetsTaskAsync<T>(
            this string resource,
            ILifeTime lifeTime,
            IProgress<float> progress = null)
        {
            var handle = Addressables.LoadAssetsAsync<T>(resource, null);
            handle.AddTo(lifeTime);

            return await handle.ToUniTask(progress, cancellationToken: lifeTime.Token);
        }

        public static async UniTask<IList<T>> LoadAssetsTaskAsync<T>(
            this IEnumerable resources,
            ILifeTime lifeTime,
            Addressables.MergeMode mode = Addressables.MergeMode.Union,
            IProgress<float> progress = null)
        {
            var handle = Addressables.LoadAssetsAsync<T>(resources, null, mode, true);
            handle.AddTo(lifeTime);
            var result = await handle.ToUniTask(progress, cancellationToken: lifeTime.Token);
            return result;
        }

        public static async UniTask<IEnumerable<TResult>> LoadAssetsTaskAsync<TSource, TResult, TAsset>(
            this IEnumerable<TAsset> assetReference,
            IList<TResult> resultContainer,
            ILifeTime lifeTime)
            where TResult : class
            where TAsset : AssetReference
            where TSource : Object
        {
            var taskList = ClassPool.Spawn<List<UniTask<TSource>>>();

            foreach (var asset in assetReference)
            {
                var assetTask = asset.LoadAssetTaskAsync<TSource>(lifeTime);
                taskList.Add(assetTask);
            }

            try
            {
                var result = await UniTask.WhenAll(taskList)
                    .AttachExternalCancellation<TSource[]>(lifeTime.Token);

                for (var j = 0; j < result.Length; j++)
                {
                    if (result[j] is TResult item) resultContainer.Add(item);
                }
            }
            finally
            {
                taskList.Despawn();
            }

            return resultContainer;
        }

        public static async UniTask<IReadOnlyList<TSource>> LoadAssetsTaskAsync<TSource, TAsset>(
            this IReadOnlyList<TAsset> assetReference,
            List<TSource> resultContainer, ILifeTime lifeTime)
            where TAsset : AssetReference
            where TSource : Object
        {
            return await assetReference.LoadAssetsTaskAsync<TSource, TSource, TAsset>(resultContainer, lifeTime);
        }

        public static async UniTask<IReadOnlyList<TResult>> LoadAssetsTaskAsync<TSource, TResult, TAsset>(
            this IReadOnlyList<TAsset> assetReference,
            List<TResult> resultContainer, ILifeTime lifeTime)
            where TResult : class
            where TAsset : AssetReference
            where TSource : Object
        {
            var taskList = ClassPool.Spawn<List<UniTask<TSource>>>();

            for (var i = 0; i < assetReference.Count; i++)
            {
                var asset = assetReference[i];
                var assetTask = asset.LoadAssetTaskAsync<TSource>(lifeTime);
                taskList.Add(assetTask);
            }

            try
            {
                var result = await UniTask.WhenAll(taskList)
                    .AttachExternalCancellation<TSource[]>(lifeTime.Token);

                for (var j = 0; j < result.Length; j++)
                {
                    if (result[j] is TResult item) resultContainer.Add(item);
                }
            }
            finally
            {
                taskList.Despawn();
            }

            return resultContainer;
        }

        /// <summary>
        /// request local cache update by actual catalog
        /// </summary>
        /// <returns>list of updated ids</returns>
        public static async UniTask<List<string>> ResetAddressablesCacheForUpdatedContent(this object _)
        {
            var handle = Addressables.CheckForCatalogUpdates();
            var updatedIds = await handle.ToUniTask();
            if (updatedIds == null || updatedIds.Count == 0)
                return updatedIds;
            Addressables.ClearDependencyCacheAsync(updatedIds);
            return updatedIds;
        }

        #region instance spawn

        public static UniTask<T> SpawnByReference<T>(
            this AssetReferenceGameObject assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Component
        {
            return SpawnByReference<T>((AssetReference)assetReference,
                lifeTime,
                destroyInstanceWithLifetime,
                downloadDependencies,
                activateOnSpawn,
                progress);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<GameObject> SpawnByReference<T>(this AssetReferenceT<GameObject> assetReference,CancellationToken token = default)
        {
            var asset = await SpawnObjectByReference(
                assetReference,
                Vector3.zero,
                Quaternion.identity,
                null,
                false,
                true,
                false,
                token,
                null);

            return asset;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> SpawnByReference<T>(this AssetReferenceT<T> assetReference,Vector3 position,CancellationToken token = default)
            where T : Component
        {
            var asset = await SpawnObjectByReference(
                assetReference,
                position,
                Quaternion.identity,
                null,
                false,
                true,
                false,
                token,
                null);

            return asset;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> SpawnByReference<T>(this AssetReferenceT<T> assetReference,CancellationToken token = default)
            where T : Component
        {
            var asset = await SpawnObjectByReference(
                assetReference,
                Vector3.zero,
                Quaternion.identity,
                null,
                false,
                true,
                false,
                token,
                null);

            return asset;
        }
        
        public static async UniTask<GameObject> SpawnByReference(
            this AssetReferenceT<GameObject> reference,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            var asset = await SpawnObjectByReference(
                reference,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);

            return asset;
        }
        
        public static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> reference,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null) where T : Component
        {
            var asset = await SpawnObjectByReference(
                reference,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);

            return asset;
        }
        
        public static async UniTask<GameObject> SpawnByReference(
            this AssetReferenceT<GameObject> reference,
            Vector3 position,
            Transform parent = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            var asset = await SpawnObjectByReference(
                reference,
                position,
                Quaternion.identity,
                parent,
                false,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);

            return asset;
        }
        
        private static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> reference,
            Vector3 position,
            Transform parent = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Component
        {
            var asset = await SpawnObjectByReference(
                reference,
                position,
                Quaternion.identity,
                parent,
                false,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);

            return asset;
        }
        
        private static async UniTask<T> SpawnObjectByReference<T>(
            this AssetReferenceT<T> reference,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (!reference.RuntimeKeyIsValid())
                return default;

            var asset = await SpawnByReference<T>(
                reference.AssetGUID,
                null,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);
            
            return asset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> SpawnByReference<T>(this AssetReferenceT<T> assetReference,ILifeTime lifeTime)
            where T : Object
        {
            var asset = await SpawnByReference<T>(
                assetReference,
                lifeTime,
                Vector3.zero,
                Quaternion.identity,
                null,
                false,
                true,
                false,
                false);

            return asset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            Transform parent)
            where T : Object
        {
            var asset = await SpawnByReference<T>(
                assetReference,
                lifeTime,
                Vector3.zero,
                Quaternion.identity,
                parent,
                false,
                true,
                false,
                false);

            return asset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            Vector3 position, Quaternion rotation, 
            Transform parent, 
            bool stayWorldPosition)
            where T : Object
        {
            var asset = await SpawnByReference<T>(
                assetReference,
                lifeTime,
                position,
                rotation,
                parent,
                stayWorldPosition,
                true,
                false,
                false);

            return asset;
        }

        public static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            var asset = await SpawnByReference<T>(
                assetReference,
                lifeTime,
                Vector3.zero,
                Quaternion.identity,
                null,
                false,
                activateOnSpawn,
                downloadDependencies,
                destroyInstanceWithLifetime,
                lifeTime.Token,
                progress);

            return asset;
        }

        public static async UniTask<T> SpawnByReference<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            Action<T> assetResult,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            var reference = assetReference as AssetReference;

            var asset = await SpawnByReference<T>(
                reference, lifeTime,
                destroyInstanceWithLifetime,
                downloadDependencies,
                activateOnSpawn,
                progress);

            assetResult?.Invoke(asset);

            return asset;
        }

        public static async UniTask<T> SpawnByReference<T>(
            this AssetReference assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime = true,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            var asset = await SpawnByReference<T>(
                assetReference,
                lifeTime,
                Vector3.zero,
                Quaternion.identity,
                null,
                false,
                activateOnSpawn,
                downloadDependencies,
                lifeTime.Token, progress);

            if (destroyInstanceWithLifetime)
                asset.DestroyWith(lifeTime);

            return asset;
        }

        public static UniTask<T> SpawnByReference<T>(
            this AssetReference reference,
            ILifeTime lifeTime,
            Vector3 position = default,
            Transform parent = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            return SpawnByReference<T>(
                reference,
                lifeTime,
                position,
                Quaternion.identity,
                parent,
                false,
                activateOnSpawn,
                downloadDependencies, token, progress);
        }
        
        public static async UniTask<T> SpawnByReference<T>(
            this AssetReference reference,
            ILifeTime lifeTime,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            bool destroyInstanceWithLifetime = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (reference.RuntimeKeyIsValid() == false)
                return default;

            var asset = await SpawnByReference<T>(
                reference.AssetGUID,
                lifeTime,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);
            
            if(asset!=null && lifeTime!=null && destroyInstanceWithLifetime)
                asset.DestroyWith(lifeTime);

            return asset;
        }

        public static UniTask<T> SpawnByReference<T>(
            this AssetReference reference,
            ILifeTime lifeTime,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (reference.RuntimeKeyIsValid() == false)
                return default;

            return SpawnByReference<T>(
                reference.AssetGUID,
                lifeTime,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);
        }

        public static async UniTask<T> SpawnByReference<T>(
            this string reference,
            ILifeTime lifeTime,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (string.IsNullOrEmpty(reference))
            {
                GameLog.Log($"[SpawnObjectAsync<T>] {typeof(T).Name} AssetReference key is NULL");
                return default;
            }

            var asset = default(Object);

            var result = await LoadAssetReferenceAsync<T>(
                reference,
                lifeTime,
                downloadDependencies,
                token,
                progress);

            if (!result.Success)
            {
                GameLog.LogError(
                    $"[SpawnObjectAsync<T>] {typeof(T).Name} AssetReference {reference} load error {result.Error}");
                return default;
            }

            asset = result.Result;

            Object targetObject = null;
            GameObject gameObjectInstance = null;

            switch (asset)
            {
                case GameObject gameObject:
                {
                    gameObjectInstance = gameObject.Spawn(
                        position,
                        rotation,
                        parent,
                        stayWorldPosition,
                        activateOnSpawn);

                    targetObject = gameObjectInstance;

                    break;
                }
                case Component component:
                {
                    var objectInstance = component.gameObject.Spawn(position,
                        rotation,
                        parent,
                        stayWorldPosition,
                        activateOnSpawn);
                    
                    gameObjectInstance = objectInstance;
                    targetObject = objectInstance.GetComponent<T>();

                    break;
                }
                default:
                {
                    targetObject = Object.Instantiate(asset);
                    break;
                }
            }
            
            if (gameObjectInstance != null)
            {
                var incrementCounter = !(lifeTime == null && result.IsInitial);
                var assetLifeTime = gameObjectInstance.GetAssetLifeTime();
                result.Handle.AddTo(assetLifeTime, incrementCounter);
            }

            return targetObject as T;
        }

        public static UniTask<GameObject[]> SpawnByReference(
            this AssetReference reference,
            ILifeTime lifeTime,
            int count,
            Vector3 position = default,
            Quaternion rotation = default,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            if (!reference.RuntimeKeyIsValid())
                return UniTask.FromResult(Array.Empty<GameObject>());

            return SpawnByReference(reference.AssetGUID,
                lifeTime,
                count,
                position,
                rotation,
                parent,
                stayWorldPosition,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);
        }

        public static async UniTask<GameObject[]> SpawnByReference(
            this string reference,
            ILifeTime lifeTime,
            int count,
            Vector3 position = default,
            Quaternion rotation = default,
            Transform parent = null,
            bool stayWorldPosition = false,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(reference))
            {
                GameLog.LogError("[SpawnObjectsAsync] AssetReference key is NULL");
                return Array.Empty<GameObject>();
            }

            var taskList = ListPool<UniTask<GameObject>>.Get();
            taskList.Clear();

            for (int i = 0; i < count; i++)
            {
                var task = SpawnByReference<GameObject>(
                    reference,
                    lifeTime,
                    position, 
                    rotation, 
                    parent,
                    stayWorldPosition, activateOnSpawn, downloadDependencies,
                    token, progress);

                taskList.Add(task);
            }

            var result = await UniTask.WhenAll(taskList);
            return result;
        }

        #endregion

#if !UNITY_WEBGL

        public static T LoadAssetInstanceForCompletion<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime = true)
            where T : Object
        {
            var asset = assetReference.LoadAssetForCompletion<T>(lifeTime);
            if (asset == null) return null;

            var isPawn = false;

            Object instance = null;

            switch (asset)
            {
                case Component component:
                    instance = component.gameObject.Spawn<T>();
                    isPawn = true;
                    break;
                case GameObject gameObjectAsset:
                    instance = gameObjectAsset.Spawn();
                    isPawn = true;
                    break;
                default:
                    instance = Object.Instantiate(asset);
                    break;
            }

            if (!destroyInstanceWithLifetime) return instance as T;

            if (isPawn)
            {
                instance.DestroyWith(lifeTime);
            }
            else
            {
                instance.DestroyWith(lifeTime);
            }

            return instance as T;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAssetForCompletion<T>(this AssetReferenceT<T> assetReference, ILifeTime lifeTime)
            where T : Object
        {
            return LoadAssetForCompletion<T>(assetReference as AssetReference, lifeTime);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAssetForCompletion<T>(this AssetReference assetReference, ILifeTime lifeTime)
            where T : Object
        {
            if (lifeTime.IsTerminated) return default(T);

            if (assetReference == null || assetReference.RuntimeKeyIsValid() == false)
            {
                GameLog.LogError($"AssetReference key is NULL {assetReference}");
                return null;
            }

            var isComponent = typeof(T).IsComponent();

            Object asset = isComponent
                ? LoadAssetSync<GameObject>(assetReference, lifeTime)
                : LoadAssetSync<T>(assetReference, lifeTime);

            if (asset == null) return default(T);

            var result = asset is GameObject gameObjectAsset && isComponent
                ? gameObjectAsset.GetComponent<T>()
                : asset as T;

            return result;
        }

        private static T LoadAssetSync<T>(
            this AssetReference assetReference,
            ILifeTime lifeTime)
            where T : Object
        {
            var handle = assetReference.LoadAssetAsyncOrExposeHandle<T>(out var yetRequested);
            handle.AddTo(lifeTime, yetRequested);

            var asset = handle.WaitForCompletion();
            return asset;
        }

#endif

#if UNITY_EDITOR
        [MenuItem("UniGame/Addressables/Clear Bundle Cache")]
#endif
        public static bool ClearBundleCache()
        {
#if UNITY_WEBGL
            return false;
#else
            return Caching.ClearCache();
#endif
            return false;
        }

        public static async UniTask<bool> ClearCacheAsync()
        {
            var handle = Addressables.CleanBundleCache();
            var result = await handle.ToUniTask();
            return result;
        }

        public static async UniTask DownloadDependenciesAsync(this IEnumerable targets,
            ILifeTime lifeTime,
            Type type = null,
            IProgress<float> process = null)
        {
            await DownloadDependenciesAsync(targets, lifeTime, Addressables.MergeMode.Union, type, process);
        }

        /// <summary>
        /// download all dependencies to the cache
        /// </summary>
        public static async UniTask DownloadDependenciesAsync(
            this IEnumerable targets,
            ILifeTime lifeTime,
            Addressables.MergeMode mergeMode = Addressables.MergeMode.Union,
            Type type = null,
            IProgress<float> process = null)
        {
            var locators = await Addressables
                .LoadResourceLocationsAsync(targets, mergeMode, type)
                .ToUniTask();

            var handle = Addressables.DownloadDependenciesAsync(locators, mergeMode);

            if (handle.IsDone) return;

            handle.AddTo(lifeTime);

            var downloadSize = handle.GetDownloadStatus().TotalBytes;
            if (downloadSize <= 0)
            {
                GameLog.LogFormat("Addressable: {0} :: nothing to download", nameof(DownloadDependenciesAsync));
                return;
            }

            await handle.ToUniTask(process)
                .AttachExternalCancellation(lifeTime.Token)
                .SuppressCancellationThrow();
        }

        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        public static async UniTask DownloadDependencyAsync(
            this object targets,
            ILifeTime lifeTime,
            Type type = null,
            IProgress<float> process = null)
        {
            var resource = ListPool<object>.Get();
            resource.Clear();
            resource.Add(targets);

            await DownloadDependenciesAsync(resource, lifeTime, type, process);

            resource.Despawn();
        }

        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        public static async UniTask DownloadDependencyAsync(
            this object targets,
            ILifeTime lifeTime,
            Addressables.MergeMode mode = Addressables.MergeMode.Union,
            Type type = null,
            IProgress<float> process = null)
        {
            var resource = ListPool<object>.Get();
            resource.Clear();
            resource.Add(targets);

            await DownloadDependenciesAsync(resource, lifeTime, mode, type, process);

            resource.Despawn();
        }

        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        public static async UniTask DownloadDependencyAsync(this object targets,
            ILifeTime lifeTime,
            Addressables.MergeMode mode = Addressables.MergeMode.Union,
            IProgress<float> process = null)
        {
            var resource = ListPool<object>.Get();
            resource.Clear();
            resource.Add(targets);

            await DownloadDependenciesAsync(resource, lifeTime, mode, null, process);

            resource.Despawn();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this AssetReference assetReference,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
            where T : Object
        {
            if (lifeTime.IsTerminated) return default(T);

            if (assetReference == null || assetReference.RuntimeKeyIsValid() == false)
            {
                GameLog.LogError($"AssetReference key is NULL {assetReference}");
                return null;
            }

            var resource = assetReference.AssetGUID;
            var result = await LoadAssetTaskAsync<T>(resource, lifeTime, downloadDependencies, progress);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this string referenceKey,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
        {
            var result = await LoadAssetReferenceAsync<T>(referenceKey,
                lifeTime,
                downloadDependencies,
                progress: progress);

            return result.Result;
        }

        public static async UniTask<T> LoadAssetInstanceAsync<T>(
            this string referenceKey,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null) where T : Object
        {
            var asset = await LoadAssetTaskAsync<T>(referenceKey, lifeTime, downloadDependencies, progress);
            if (asset == null) return null;
            var instance = Object.Instantiate(asset);
            return instance;
        }

        public static void NotifyProgress(IAsyncHandleStatus progressData, IProgress<HandleStatus> progress)
        {
            progress.Report(new HandleStatus()
            {
                Status = progressData.Status,
                DownloadedBytes = progressData.DownloadedBytes,
                IsDone = progressData.IsDone,
                OperationException = progressData.OperationException,
                TotalBytes = progressData.TotalBytes,
            });
        }

        public static async UniTask<T> ConvertToUniTask<T>(this AsyncOperationHandle<T> handle, ILifeTime lifeTime)
            where T : class
        {
            handle.AddTo(lifeTime);
            return await handle.ToUniTask();
        }

        public static async UniTask<TResult> LoadAssetTaskApiAsync<TAsset, TResult>(this AssetReference assetReference,
            ILifeTime lifeTime)
            where TAsset : Object
            where TResult : class
        {
            var result = await assetReference.LoadAssetTaskAsync<TAsset>(lifeTime);
            return result as TResult;
        }

        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this AssetReferenceGameObject assetReference,
            ILifeTime lifeTime)
            where T : class
        {
            var result = await LoadAssetTaskAsync<GameObject>(assetReference as AssetReference, lifeTime);
            if (result is T tResult) return tResult;
            return result != null ? result.GetComponent<T>() : null;
        }

        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this AssetReferenceScriptableObject<T> assetReference,
            ILifeTime lifeTime)
            where T : class
        {
            var result = await LoadAssetTaskAsync<ScriptableObject>(assetReference as AssetReference, lifeTime);
            return result as T;
        }

        public static async UniTask<TApi> LoadAssetTaskAsync<T, TApi>(
            this AssetReferenceScriptableObject<T, TApi> assetReference,
            ILifeTime lifeTime)
            where T : ScriptableObject
            where TApi : class
        {
            var result = await LoadAssetTaskAsync<ScriptableObject>(assetReference, lifeTime);
            return result as TApi;
        }

        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this AssetReferenceScriptableObject assetReference,
            ILifeTime lifeTime)
            where T : class
        {
            var result = await LoadAssetTaskAsync<ScriptableObject>(assetReference as AssetReference, lifeTime);
            return result as T;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetTaskAsync<T>(this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
            where T : Object
        {
            if (lifeTime.IsTerminated) return default(T);

            if (assetReference == null || assetReference.RuntimeKeyIsValid() == false)
            {
                GameLog.LogError($"AssetReference key is NULL {assetReference}");
                return null;
            }

            var resource = assetReference.AssetGUID;
            var result = await LoadAssetTaskAsync<T>(resource, lifeTime, downloadDependencies, progress);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<GameObject> LoadGameObjectTaskAsync(this AssetReference assetReference,
            ILifeTime lifeTime)
        {
            var result = await LoadAssetTaskAsync<GameObject>(assetReference, lifeTime);
            return result;
        }

        public static async UniTask<AddressableLoadResult<T>> LoadAssetReferenceAsync<T>(
            this string referenceKey,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(referenceKey))
            {
                GameLog.LogError($"AssetReference key is NULL {referenceKey}");
                return AddressableLoadResult<T>.FailedResourceResult;
            }

            var isComponent = typeof(T).IsComponent();

            var loadTask = isComponent
                ? LoadReferenceAsync<GameObject>(referenceKey,lifeTime, token, downloadDependencies, progress)
                : LoadReferenceAsync<T>(referenceKey, lifeTime, token, downloadDependencies, progress);

            var loadResult = await loadTask.AttachExternalCancellation(token);
            var asset = loadResult.Result;

            var resultValue = asset switch
            {
                T assetResult => assetResult,
                GameObject gameObjectAsset when isComponent => gameObjectAsset.GetComponent<T>(),
                _ => default
            };

            var result = new AddressableLoadResult<T>()
            {
                Handle = loadResult.Handle,
                Result = resultValue,
                Success = loadResult.Success,
                IsInitial = loadResult.IsInitial,
                Error = string.Empty,
            };

            return result;
        }


        /// <summary>
        /// download dependency by reference and bind to lifetime
        /// </summary>
        public static async UniTask<AsyncOperationStatus> DownloadDependenciesTaskAsync(
            this string resource,
            ILifeTime lifeTime,
            CancellationToken token,
            bool autoReleaseHandle = true,
            IProgress<float> progress = null)
        {
            if (_dependenciesTaskCache.TryGetValue(resource, out var taskSource))
            {
                var result = await taskSource.Task.AttachExternalCancellation(token);
                //if cache valid return it
                if (result.Status == AddressableLoadStatus.Succeeded &&
                    !autoReleaseHandle &&
                    result.Handle.IsValid())
                {
                    result.Handle.AddTo(lifeTime, true);
                    return AsyncOperationStatus.Succeeded;
                }
            }

            taskSource = new UniTaskCompletionSource<AddressableLoadState>();
            _dependenciesTaskCache[resource] = taskSource;

            var taskResult = new AddressableLoadState();

            var dependencies = Addressables
                .DownloadDependenciesAsync(resource, false);

            taskResult.Status = AddressableLoadStatus.Loading;
            taskResult.Handle = dependencies;

            var isCancelled = await dependencies
                .ToUniTask(progress, PlayerLoopTiming.PostLateUpdate, token)
                .SuppressCancellationThrow();

            if (isCancelled)
            {
                taskResult.Status = AddressableLoadStatus.Failed;
                Addressables.Release(dependencies);
                taskSource.TrySetResult(taskResult);
                return AsyncOperationStatus.Failed;
            }

            var status = dependencies.Status;

            if (status == AsyncOperationStatus.Succeeded)
            {
                if (autoReleaseHandle)
                {
                    Addressables.Release(dependencies);
                }
                else
                {
                    dependencies.AddTo(lifeTime);
                }
            }

            taskResult.Status = status == AsyncOperationStatus.Succeeded
                ? AddressableLoadStatus.Succeeded
                : AddressableLoadStatus.Failed;

            taskSource.TrySetResult(taskResult);

            return status;
        }

        private static AsyncOperationHandle<TResult> LoadAssetAsyncOrExposeHandle<TResult>(
            this AssetReference assetReference, out bool yetRequested)
            where TResult : class
        {
            yetRequested = assetReference.OperationHandle.IsValid();
            var handle = yetRequested
                ? assetReference.OperationHandle.Convert<TResult>()
                : assetReference.LoadAssetAsync<TResult>();
            return handle;
        }

        private static async UniTask WaitWhileCachingReadyAsync(CancellationToken cancellationToken = default)
        {
#if !UNITY_WEBGL
            if (Caching.ready) return;

            while (Caching.ready == false && cancellationToken.IsCancellationRequested == false)
            {
                await UniTask.WaitForEndOfFrame(cancellationToken);
            }
#endif
        }

        public static async UniTask<AddressableLoadResult> AttachLifeTimeToReference(
            this AssetReference reference,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
        {
            if (!reference.RuntimeKeyIsValid())
                return AddressableLoadResult.FailedResult;

            var guid = reference.AssetGUID;

            if (_assetTaskCache.TryGetValue(guid, out var state) && state.Handle.IsValid())
            {
                state.Handle.AddTo(lifeTime, true);
                return new AddressableLoadResult()
                {
                    Error = string.Empty,
                    Handle = state.Handle,
                    Result = AddressableLoadStatus.Succeeded,
                    Success = true,
                    IsInitial = false,
                };
            }

            var loadResult = await LoadReferenceAsync<Object>(
                reference.AssetGUID, 
                lifeTime,
                lifeTime.Token, downloadDependencies, progress);
            
            return loadResult;
        }

        public static async UniTask<AddressableLoadResult> AttachLifeTimeToReference(
            this string reference,
            ILifeTime lifeTime,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
        {
            var loadResult = await LoadReferenceAsync<Object>(
                reference,
                lifeTime,
                lifeTime.Token, 
                downloadDependencies, progress);
            
            return loadResult;
        }

        public static async UniTask<AddressableLoadResult> LoadReferenceAsync<T>(
            this string reference,
            ILifeTime lifeTime,
            CancellationToken token,
            bool downloadDependencies = false,
            IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(reference))
                return AddressableLoadResult.FailedResult;

            var result = new AddressableLoadResult();
            
#if !UNITY_WEBGL
            if (!Caching.ready)
                await WaitWhileCachingReadyAsync(token);
#endif
            
            if (downloadDependencies)
                await DownloadDependenciesTaskAsync(reference,lifeTime,token, true, progress);

            if (_assetTaskCache.TryGetValue(reference, out var state))
            {
                var status = state.Status;
                var cachedTask = state.Task;

                if (status != AddressableLoadStatus.Succeeded)
                {
                    var taskResult = await cachedTask.Task
                        .AttachExternalCancellation(token)
                        .SuppressCancellationThrow();

                    if (taskResult.IsCanceled)
                        return AddressableLoadResult.CancelledResult;
                }

                var resourceHandle = state.Handle;
                var isValidHandle = resourceHandle.IsValid();

                //if result still valid when use existing result
                if (isValidHandle && state is { Status: AddressableLoadStatus.Succeeded, Result: not null })
                {
                    if(lifeTime!=null)
                        resourceHandle.AddTo(lifeTime, true);
                    
                    result.Error = string.Empty;
                    result.Success = true;
                    result.Handle = resourceHandle;
                    result.Result = resourceHandle.Result;
                    result.IsInitial = false;
                    
                    return result;
                }
            }
            else
            {
                _assetTaskCache[reference] = new AddressableLoadState()
                {
                    Status = AddressableLoadStatus.None,
                    Result = null,
                };
            }

            state = _assetTaskCache[reference];
            state.Task?.TrySetCanceled();
            state.Task = new UniTaskCompletionSource<AddressableLoadState>();
            var handle = Addressables.LoadAssetAsync<T>(reference);
            state.Handle = handle;

            var loadResult = await handle
                .ToUniTask(progress, PlayerLoopTiming.Update, token, autoReleaseWhenCanceled: true)
                .SuppressCancellationThrow();

            if (loadResult.IsCanceled)
            {
                state.Task.TrySetCanceled();
                state.Status = AddressableLoadStatus.Cancelled;
                
                return AddressableLoadResult.CancelledResult;
            }

            var handleStatus = handle.Status == AsyncOperationStatus.Succeeded
                ? AddressableLoadStatus.Succeeded
                : AddressableLoadStatus.Failed;

            if (handleStatus == AddressableLoadStatus.Succeeded && lifeTime!=null)
                handle.AddTo(lifeTime);
            
            state.Status = handleStatus;
            state.Result = loadResult.Result;
            state.Task.TrySetResult(state);
            
            if(handleStatus != AddressableLoadStatus.Succeeded)
                return AddressableLoadResult.FailedResult;

            result.Error = string.Empty;
            result.Success = true;
            result.Handle = handle;
            result.Result = handle.Result;
            result.IsInitial = true;
            
            return result;
        }


        #region lifetime

        public static AsyncOperationHandle<T> AddTo<T>(this AsyncOperationHandle<T> handle, ILifeTime lifeTime,
            bool incrementRefCount = false)
        {
            if (incrementRefCount)
                Addressables.ResourceManager.Acquire(handle);

            var addressableReference = new AddressableHandleReference<T>()
            {
                Handle = handle
            };

            lifeTime?.AddCleanUpAction(addressableReference.Release);

            return handle;
        }

        public static AsyncOperationHandle AddTo(
            this AsyncOperationHandle handle, 
            ILifeTime lifeTime,
            bool incrementRefCount)
        {
            if (incrementRefCount)
                Addressables.ResourceManager.Acquire(handle);

            var addressableReference = new AddressableHandleReference()
            {
                Handle = handle
            };

            lifeTime.AddCleanUpAction(addressableReference.Release);
            return handle;
        }

        public static UniTask<TAsset> AddToAsUniTask<TAsset>(
            this AsyncOperationHandle<TAsset> handle,
            ILifeTime lifeTime,
            bool incrementRefCount = true)
        {
            var operation = handle.AddTo(lifeTime, incrementRefCount);
            return operation.ToUniTask();
        }

        public static async UniTask ReleaseHandle<TAsset>(this AsyncOperationHandle<TAsset> handle)
        {
            if (handle.IsValid() == false) return;
            await UniTask.SwitchToMainThread();
            Addressables.Release(handle);
        }

        public static async UniTask ReleaseHandle(this AsyncOperationHandle handle)
        {
            if (handle.IsValid() == false) return;
            await UniTask.SwitchToMainThread();
            Addressables.Release(handle);
        }

        public static ILifeTime AddTo<TAsset>(this AssetReferenceT<TAsset> reference, ILifeTime lifeTime)
            where TAsset : Object
        {
            reference.OperationHandle.AddTo(lifeTime);
            return lifeTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle AddTo(this AsyncOperationHandle handle, ILifeTime lifeTime)
        {
            if (lifeTime == null) return handle;

            var addressableReference = new AddressableHandleReference()
            {
                Handle = handle
            };

            lifeTime.AddCleanUpAction(addressableReference.Release);

            return handle;
        }

        public static ILifeTime AddTo(this AssetReference handle, ILifeTime lifeTime)
        {
            handle.OperationHandle.AddTo(lifeTime);
            return lifeTime;
        }

        #endregion
    }

    public class AddressableLoadState
    {
        public static readonly AddressableLoadState FailedResult = new()
        {
            Status = AddressableLoadStatus.Failed
        };

        public static readonly AddressableLoadState CancelledResult = new()
        {
            Status = AddressableLoadStatus.Cancelled
        };

        public UniTaskCompletionSource<AddressableLoadState> Task;
        public AsyncOperationHandle Handle;
        public object Result;
        public AddressableLoadStatus Status;
    }

    public enum AddressableLoadStatus : byte
    {
        None,
        Loading,
        Failed,
        Succeeded,
        Cancelled,
    }

    public struct AddressableLoadResult
    {
        public const string FailedMessage = "Failed to load asset";
        public const string CancelledMessage = "cancelled to load asset";

        public static readonly AddressableLoadResult FailedResult = new()
        {
            Handle = default,
            Result = default,
            Success = false,
            Error = FailedMessage,
        };
        
        public static readonly AddressableLoadResult CancelledResult = new()
        {
            Handle = default,
            Result = default,
            Success = false,
            Error = CancelledMessage,
        };

        public AsyncOperationHandle Handle;
        public object Result;
        public bool Success;
        public string Error;
        public bool IsInitial;
    }
    

    public struct AddressableLoadResult<T>
    {
        public const string FailedMessage = "Failed to load asset";

        public static readonly AddressableLoadResult<T> FailedResourceResult = new()
        {
            Handle = default,
            Result = default,
            Success = false,
            Error = FailedMessage,
        };

        public AsyncOperationHandle Handle;
        public T Result;
        public bool Success;
        public string Error;
        public bool IsInitial;
    }

    public struct AddressableHandleReference<T>
    {
        public AsyncOperationHandle<T> Handle;

        public void Release()
        {
            Handle.ReleaseHandle().Forget();
        }
    }

    public struct AddressableHandleReference
    {
        public AsyncOperationHandle Handle;

        public void Release()
        {
            Handle.ReleaseHandle().Forget();
        }
    }

    public static class GameObjectAddressableExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UniTask<ObjectsItemResult> SpawnAsync(
            this GameObject prototype,
            int count,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            CancellationToken token = default)
        {
            var pawn = ObjectPool
                .SpawnAsync(prototype, count, position, rotation, parent, token);

            return pawn;
        }
    }
}