namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using UniCore.Runtime.ProfilerTools;
    using UniModules.Runtime.Network;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.ResourceLocations;

    public class AddressableLocationHandler
    {
        private const string RemoteAddressableLocationKey = nameof(RemoteAddressableLocationKey);
        
        private bool isRemoteLocationActive = false;
        private AddressableRemoteValue activeLocation;
        private Dictionary<string,AddressableRemoteValue> remoteLocations = new();
        private string activeRemoteUrl = string.Empty;
        private string catalogUrl = string.Empty;
        private AddressableRemoteValue activeRemote;

        public void Register(AddressableRemoteValue remote)
        {
            if (remote.enabled == false) return;
            remoteLocations[remote.remoteUrl] = remote;
        }
        
        public bool Remove(AddressableRemoteValue remote)
        {
            return Remove(remote.remoteUrl);
        }
        
        public bool Remove(string remoteUrl)
        {
            return remoteLocations.Remove(remoteUrl);
        }
        
        public async UniTask<UrlResult> SelectRemoteLocationAsync(int tries = 3,int timeout = 5)
        {
            var result = new UrlResult()
            {
                success = false,
                time = float.MaxValue,
                url = string.Empty
            };
            
            if (remoteLocations.Count == 0) return result;
            
            var enabledRemote = remoteLocations
                .Values
                .Where(x => x.enabled)
                .ToList();

            var checkUrls = enabledRemote
                .Select(x => x.testUrl);

            var bestResult = await UrlChecker.SelectFastestEndPoint(checkUrls, tries, timeout);
            return bestResult;
        }
        
        public async UniTask<bool> ActivateRemoteLocationAsync(string remoteUrl)
        {
            if (string.IsNullOrEmpty(activeRemoteUrl))
                return false;

            if (!remoteLocations.TryGetValue(remoteUrl, out var location))
                return false;
            
            if(location.enabled == false)
                return false;
            
            if(remoteUrl.Equals(activeRemoteUrl, StringComparison.OrdinalIgnoreCase))
                return true;
            
            await Addressables.InitializeAsync().ToUniTask();
            
            activeLocation = location;
            activeRemoteUrl = remoteUrl;
            var catalog = activeLocation.remoteCatalogName;
            catalogUrl = $"{activeRemoteUrl.TrimEnd('/')}/{catalog.TrimStart('/')}";
            
            // Reload the catalog with the new URL if necessary
            // You might need to unload the current catalog and load a new one
            var result = await Addressables
                .LoadContentCatalogAsync(catalogUrl)
                .ToUniTask();

            await Addressables.CleanBundleCache();

            if (isRemoteLocationActive == false)
            {
                Addressables.ResourceManager.InternalIdTransformFunc = null;
                Addressables.ResourceManager.InternalIdTransformFunc = TransformInternalId;
            }

            isRemoteLocationActive = true;
            return true;
        }
        
        public string TransformInternalId(IResourceLocation location)
        {
            if (isRemoteLocationActive == false)
                return location.InternalId;
            
            GameLog.Log($"START REPLACE {location.InternalId}",Color.yellow);
        
            if(location.InternalId.Contains(activeRemoteUrl, StringComparison.OrdinalIgnoreCase))
                return location.InternalId; // No change needed

            var replaced = false;
            var resultId = location.InternalId;
            var filterPath = string.Empty;

            if (resultId.Contains(RemoteLoadPathKey))
                filterPath = RemoteLoadPathKey;
            if(resultId.Contains(filterCatalogPath))
                filterPath = filterCatalogPath;
        
            // Example: Replace a portion of the InternalId with the new path
            if (!string.IsNullOrEmpty(filterPath))
            {
                replaced = true;
                resultId = location.InternalId.Replace(filterPath, replacementCatalogPath);
            }
        
            GameLog.Log($"Try replace {replaced} RESULT {resultId} | ORIGIN ={location.InternalId} | \nFILTER {filterCatalogPath} | REPLACE {replacementCatalogPath}", Color.yellow);

            return resultId; // Return the original ID if no replacement is needed
        }
    }
}