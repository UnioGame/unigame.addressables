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
                lifeTime.AddCleanUpAction(() =>  Addressables
                    .UnloadSceneAsync(sceneHandle,true)
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

        public static async UniTask<List<TResult>> LoadScriptableAssetsTaskAsync<TResult>(
            this IEnumerable<AssetReference> assetReference,
            ILifeTime lifeTime)
            where TResult : class
        {
            var container = new List<TResult>();
            await assetReference.LoadAssetsTaskAsync<ScriptableObject, TResult, AssetReference>(container, lifeTime);
            return container;
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
            
            return await handle.ToUniTask(progress,cancellationToken:lifeTime.Token);
        }
        
        public static async UniTask<IList<T>> LoadAssetsTaskAsync<T>(
            this IEnumerable resources, 
            ILifeTime lifeTime,
            Addressables.MergeMode mode = Addressables.MergeMode.Union,
            IProgress<float> progress = null)
        {
            var handle = Addressables.LoadAssetsAsync<T>(resources, null,mode,true);
            handle.AddTo(lifeTime);
            var result = await handle.ToUniTask(progress,cancellationToken:lifeTime.Token);
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

            var result = await UniTask.WhenAll(taskList);
            for (var j = 0; j < result.Length; j++)
            {
                if (result[j] is TResult item) resultContainer.Add(item);
            }

            taskList.Despawn();

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

            var result = await UniTask.WhenAll(taskList);
            for (var j = 0; j < result.Length; j++)
            {
                if (result[j] is TResult item) resultContainer.Add(item);
            }

            taskList.Despawn();

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
        
        public static UniTask<T> InstantiateTaskAsync<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            return LoadAssetInstanceTaskAsync(assetReference, lifeTime, 
                destroyInstanceWithLifetime, downloadDependencies, activateOnSpawn, progress);
        }
        
        public static UniTask<T> InstantiateTaskAsync<T>(
            this AssetReferenceGameObject assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Component
        {
            return LoadAssetInstanceTaskAsync<T>(assetReference, lifeTime, 
                destroyInstanceWithLifetime, downloadDependencies, activateOnSpawn, progress);
        }

        public static UniTask<T> LoadAssetInstanceTaskAsync<T>(
            this AssetReferenceT<T> assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            var reference = assetReference as AssetReference;
            return LoadAssetInstanceTaskAsync<T>(reference, lifeTime,
                destroyInstanceWithLifetime,downloadDependencies,
                activateOnSpawn,
                progress);
        }
        
        public static async UniTask<T> LoadAssetInstanceTaskAsync<T>(
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
            var asset =await LoadAssetInstanceTaskAsync<T>(reference, lifeTime,
                destroyInstanceWithLifetime,downloadDependencies,
                activateOnSpawn,
                progress);
            assetResult?.Invoke(asset);
            return asset;
        }

        public static async UniTask<T> LoadAssetInstanceTaskAsync<T>(
            this AssetReference assetReference,
            ILifeTime lifeTime,
            bool destroyInstanceWithLifetime = true,
            bool downloadDependencies = false,
            bool activateOnSpawn = true,
            IProgress<float> progress = null)
            where T : Object
        {
            if (assetReference.RuntimeKeyIsValid() == false)
            {
                GameLog.Log($"[LoadAssetInstanceTaskAsync] AssetReference key {assetReference.AssetGUID} is NULL");
                return default;
            }
            
            var guid = assetReference.AssetGUID;
            var asset = await SpawnObjectAsync<T>(guid,Vector3.zero,
                null,lifeTime,
                downloadDependencies,
                activateOnSpawn,
                lifeTime.Token, progress);
            
            if(destroyInstanceWithLifetime)
                asset.DestroyWith(lifeTime);
            
            return asset;
        }

        public static UniTask<T> SpawnObjectAsync<T>(
            this AssetReference reference,
            Vector3 position = default,
            Transform parent = null,
            ILifeTime lifeTime = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (reference.RuntimeKeyIsValid() == false)
                return default;
            
            return SpawnObjectAsync<T>(reference.AssetGUID,
                position,parent,
                lifeTime, 
                activateOnSpawn,
                downloadDependencies,token, progress);
        }
        
        public static UniTask<T> SpawnObjectAsync<T>(
            this AssetReferenceT<T> reference,
            Vector3 position = default,
            Transform parent = null,
            ILifeTime lifeTime = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
            where T : Object
        {
            if (reference.RuntimeKeyIsValid() == false)
                return default;
            
            return SpawnObjectAsync<T>(reference.AssetGUID,position,
                parent,
                lifeTime,
                activateOnSpawn,
                downloadDependencies,
                token,
                progress);
        }

        public static async UniTask<T> SpawnObjectAsync<T>(
            this string reference,
            Vector3 position = default,
            Transform parent = null,
            ILifeTime lifeTime = null,
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
            
            var result = await LoadAssetInternalAsync<T>(reference, 
                    lifeTime, 
                    downloadDependencies,
                    token,
                    progress).AttachExternalCancellation(token);
            
            if (!result.Success)
            {
                GameLog.LogError($"[SpawnObjectAsync<T>] {typeof(T).Name} AssetReference {reference} load error {result.Error}");
                return default;
            }

            var asset = result.Result;
            T instance = null;
            
            switch (asset)
            {
                case GameObject gameObject:
                {
                    var gameObjectInstance = gameObject.Spawn(
                        position,
                        Quaternion.identity,
                        parent);
                    
                    instance = gameObjectInstance as T;
                    lifeTime ??= gameObjectInstance.GetAssetLifeTime();
                    if(activateOnSpawn) gameObjectInstance.SetActive(true);
                    break;
                }
                case Component component:
                {
                    var objectInstance = component
                        .gameObject.Spawn(position, Quaternion.identity, parent);
                    
                    instance = objectInstance.GetComponent<T>();
                    lifeTime ??= objectInstance.GetAssetLifeTime();
                    if(activateOnSpawn) objectInstance.SetActive(true);
                    break;
                }
                default:
                {
                    instance = Object.Instantiate(asset);
                    break;
                }
            }
            
            if(lifeTime!=null)
                result.Handle.AddTo(lifeTime);

            return instance;
        }

        public static UniTask<GameObject[]> SpawnObjectsAsync(
            this AssetReference reference,
            int count,
            Vector3 position = default,
            Transform parent = null,
            ILifeTime lifeTime = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            if (!reference.RuntimeKeyIsValid()) 
                return UniTask.FromResult(Array.Empty<GameObject>());
            
            return SpawnObjectsAsync(
                reference.AssetGUID, 
                count, 
                position, 
                parent, 
                lifeTime, 
                activateOnSpawn,
                downloadDependencies, 
                token, 
                progress);
        }

        public static async UniTask<GameObject[]> SpawnObjectsAsync(
            this string reference,
            int count,
            Vector3 position = default,
            Transform parent = null,
            ILifeTime lifeTime = null,
            bool activateOnSpawn = true,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(reference))
            {
                GameLog.LogError("[SpawnObjectsAsync] AssetReference key is NULL");
                return default;
            }
            
            if(count<=0) return Array.Empty<GameObject>();
            
            var result = await LoadAssetInternalAsync<GameObject>(
                    reference, 
                    lifeTime, 
                    downloadDependencies,
                    token,
                    progress);
            
            if (!result.Success)
            {
                GameLog.Log($"[LoadAssetInstanceTaskAsync] load {reference} failed {result.Error}",Color.red);
                return default;
            }
            
            if(lifeTime!=null)
                result.Handle.AddTo(lifeTime);

            var asset = result.Result;
            
            var pawns = await asset.SpawnAsync(
                count,
                position,
                Quaternion.identity,
                parent,token:token);

            var instance = pawns.Items;
                    
            for (var i = 0; i < pawns.Length; i++)
            {
                var o = pawns.Items[i];
                
                if(activateOnSpawn) o.SetActive(true);
                if (lifeTime != null) continue;
                
                var objectLifeTime = o.GetAssetLifeTime();
                result.Handle.AddTo(objectLifeTime,i>0);
            }
            
            return instance;
        }
        
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
            
            if (asset == null)
                return default(T);

            var result = asset is GameObject gameObjectAsset && isComponent ?
                gameObjectAsset.GetComponent<T>() :
                asset as T;
            
            return result;
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

        public static async UniTask DownloadDependenciesAsync(
            this IEnumerable targets,
            ILifeTime lifeTime,
            Type type = null,
            IProgress<float> process = null)
        {
            await DownloadDependenciesAsync(targets, lifeTime, Addressables.MergeMode.Union, type,process);
        }

        /// <summary>
        /// download all dependencies to the cache
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="lifeTime"></param>
        /// <param name="type"></param>
        /// <param name="process"></param>
        /// <param name="mergeMode"></param>
        public static async UniTask DownloadDependenciesAsync(
            this IEnumerable targets,
            ILifeTime lifeTime,
            Addressables.MergeMode mergeMode = Addressables.MergeMode.Union,
            Type type = null,
            IProgress<float> process = null)
        {
            var locators = await Addressables
                .LoadResourceLocationsAsync(targets,mergeMode,type)
                .ToUniTask();
            
            var handle = Addressables.DownloadDependenciesAsync(locators, mergeMode);
            
            if(handle.IsDone) return;

            handle.AddTo(lifeTime);
            
            var downloadSize = handle.GetDownloadStatus().TotalBytes;
            if (downloadSize <= 0)
            {
                GameLog.LogFormat("Addressable: {0} :: nothing to download",nameof(DownloadDependenciesAsync));
                return;
            }
            
            await handle.ToUniTask(process)
                .AttachExternalCancellation(lifeTime.Token)
                .SuppressCancellationThrow();
        }

        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="lifeTime"></param>
        /// <param name="autoReleaseHandle"></param>
        /// <param name="type"></param>
        /// <param name="process"></param>
        public static async UniTask DownloadDependencyAsync(
            this object targets,
            ILifeTime lifeTime,
            Type type = null,
            IProgress<float> process = null)
        {
            var resource = ListPool<object>.Get();
            resource.Clear();
            resource.Add(targets);
            
            await DownloadDependenciesAsync(resource,lifeTime,type,process);
            
            resource.Despawn();
        }

        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="lifeTime"></param>
        /// <param name="autoReleaseHandle"></param>
        /// <param name="mode"></param>
        /// <param name="type"></param>
        /// <param name="process"></param>
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
            
            await DownloadDependenciesAsync(resource,lifeTime,mode,type,process);
            
            resource.Despawn();
        }
        
        /// <summary>
        /// download single dependencies to the cache
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="lifeTime"></param>
        /// <param name="autoReleaseHandle"></param>
        /// <param name="mode"></param>
        /// <param name="type"></param>
        /// <param name="process"></param>
        public static async UniTask DownloadDependencyAsync(
            this object targets,
            ILifeTime lifeTime,
            Addressables.MergeMode mode = Addressables.MergeMode.Union,
            IProgress<float> process = null)
        {
            var resource = ListPool<object>.Get();
            resource.Clear();
            resource.Add(targets);
            
            await DownloadDependenciesAsync(resource,lifeTime,mode,null,process);
            
            resource.Despawn();
        }

#if !UNITY_WEBGL
        
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

        public static async UniTask<T> LoadAssetTaskAsync<T>(
            this string referenceKey, 
            ILifeTime lifeTime, 
            bool downloadDependencies = false,
            IProgress<float> progress = null)
        {
            if (lifeTime.IsTerminated)
                return default;

            var result = await LoadAssetInternalAsync<T>(referenceKey, lifeTime, 
                downloadDependencies,lifeTime.Token, progress);
            
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

        public static void NotifyProgress(IAsyncHandleStatus progressData,IProgress<HandleStatus> progress )
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

        public static async UniTask<T> ConvertToUniTask<T>(this AsyncOperationHandle<T> handle, ILifeTime lifeTime) where T : class
        {
            handle.AddTo(lifeTime);
            return await handle.ToUniTask();
        }

        public static async UniTask<TResult> LoadAssetTaskApiAsync<TAsset, TResult>(this AssetReference assetReference, ILifeTime lifeTime)
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

        public static async UniTask<T> LoadAssetTaskAsync<T>(this AssetReferenceT<T> assetReference, ILifeTime lifeTime)
            where T : Object
        {
            return await LoadAssetTaskAsync<T>(assetReference as AssetReference, lifeTime);
        }

        public static async UniTask<GameObject> LoadGameObjectAssetTaskAsync(this AssetReference assetReference, ILifeTime lifeTime)
        {
            var result = await LoadAssetTaskAsync<GameObject>(assetReference, lifeTime);
            return result;
        }
        
        public static async UniTask<T> LoadGameObjectAssetTaskAsync<T>(this AssetReferenceT<T> assetReference, ILifeTime lifeTime)
            where T : Component
        {
            var result = await LoadAssetTaskAsync<GameObject>(assetReference, lifeTime);
            return result ?
                result.GetComponent<T>() :
                null;
        }

        public static async UniTask<T> LoadGameObjectAssetTaskAsync<T>(this AssetReference assetReference, ILifeTime lifeTime)
            where T : class
        {
            var result = await LoadAssetTaskAsync<GameObject>(assetReference, lifeTime);
            return result ?
                result.GetComponent<T>() :
                null;
        }

        public static async UniTask<TAsset> LoadAddressableByResourceAsync<TAsset>(
            this string resource,
            ILifeTime lifeTime)
        {
            var asset = await Addressables
                .LoadAssetAsync<TAsset>(resource)
                .AddToAsUniTask(lifeTime);
            return asset;
        }
        
        public static async UniTask<AsyncOperationStatus> DownloadDependenciesTaskAsync(
            this string resource,
            bool autoReleaseHandle = true,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            var dependencies = Addressables
                .DownloadDependenciesAsync(resource,false);
            
            await dependencies.ToUniTask(progress,PlayerLoopTiming.Update,token);

            var status = dependencies.Status;
            
            if(status == AsyncOperationStatus.Succeeded && autoReleaseHandle)
                Addressables.Release(dependencies);

            return status;
        }
  
        private static AsyncOperationHandle<TResult> LoadAssetAsyncOrExposeHandle<TResult>(
            this AssetReference assetReference, out bool yetRequested)
            where TResult : class
        {
            yetRequested = assetReference.OperationHandle.IsValid();
            var handle = yetRequested ? 
                assetReference.OperationHandle.Convert<TResult>():
                assetReference.LoadAssetAsync<TResult>();
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

        private static async UniTask<AddressableLoadResult> LoadAssetTaskWithProgressAsync<T>(
            this string reference,
            bool downloadDependencies = false,
            CancellationToken token = default,
            IProgress<float> progress = null)
        {
            
#if !UNITY_WEBGL
            if (Caching.ready == false)
            {
                await WaitWhileCachingReadyAsync(token);
            }
#endif
            
            if (downloadDependencies)
            {
                await DownloadDependenciesTaskAsync(reference, true, token, progress);
            }
            
            var handle = Addressables.LoadAssetAsync<T>(reference);
            
            var loadResult = await handle
                .ToUniTask(progress,PlayerLoopTiming.Update,token)
                .SuppressCancellationThrow();

            if (loadResult.IsCanceled || loadResult.Result == null)
            {
                ReleaseHandle(handle).Forget();
                return AddressableLoadResult.FailedResult;
            }
            
            var result = new AddressableLoadResult()
            {
                Handle = handle,
                Result = loadResult.Result,
                Success = handle.Status == AsyncOperationStatus.Succeeded
            };
            
            return result;
        }
        
        public static async UniTask<AddressableLoadResult<T>> LoadAssetInternalAsync<T>(
            this string referenceKey, 
            ILifeTime lifeTime = null,
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
                ? LoadAssetTaskWithProgressAsync<GameObject>(referenceKey, 
                    downloadDependencies,token, progress)
                : LoadAssetTaskWithProgressAsync<T>(referenceKey,
                    downloadDependencies,token, progress);
            
            var loadResult = await loadTask.AttachExternalCancellation(token);
            
            if (!loadResult.Success) 
                return AddressableLoadResult<T>.FailedResourceResult;

            if(lifeTime!=null) 
                loadResult.Handle.AddTo(lifeTime);
            
            var resultData = AddressableLoadResult<T>.CompleteResourceResult;
            resultData.Handle = loadResult.Handle;
            
            var asset = loadResult.Result;

            var resultValue = asset switch
            {
                T assetResult => assetResult,
                GameObject gameObjectAsset when isComponent => gameObjectAsset.GetComponent<T>(),
                _ => default
            };

            resultData.Result = resultValue;
            return resultData;
        }
        
        #region lifetime

        public static AsyncOperationHandle<T> AddTo<T>(
            this AsyncOperationHandle<T> handle, 
            ILifeTime lifeTime, 
            bool incrementRefCount = false)
        {
            if (incrementRefCount)
                Addressables.ResourceManager.Acquire(handle);

            var addressableReference = new AddressableHandleReference<T>()
            {
                Handle = handle
            };
            
            lifeTime.AddCleanUpAction(addressableReference.Release);
            
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
            var operation = handle.AddTo(lifeTime,incrementRefCount);
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
        public static AsyncOperationHandle AddTo(
            this AsyncOperationHandle handle,
            ILifeTime lifeTime)
        {
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

    public struct AddressableLoadResult
    {
        public static AddressableLoadResult FailedResult = new AddressableLoadResult()
        {
            Success = false
        };
        
        public AsyncOperationHandle Handle;
        public object Result;
        public bool Success;
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
        
        public static readonly AddressableLoadResult<T> CompleteResourceResult = new()
        {
            Handle = default,
            Result = default,
            Success = true,
            Error = string.Empty,
        };
        
        public AsyncOperationHandle Handle;
        public T Result;
        public bool Success;
        public string Error;
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
                .SpawnAsync(prototype,count, position, rotation, parent, token);
            
            return pawn;
        }
        
    }
}
