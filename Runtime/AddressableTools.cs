namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using UniCore.Runtime.ProfilerTools;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceLocations;

    public static class AddressableTools
    {
        private static AddressableRemoteConfig addressableRemote;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // Initialization logic can be added here if needed
            InitializeAsync().Forget();
        }

        public static async UniTask InitializeAsync()
        {
            var remoteAddressableConfig = Resources.Load<AddressableRemoteConfig>(string.Empty);
            if (remoteAddressableConfig != null)
            {
                InitializeRemoteAddressableAsync().Forget();
            }
        }
        
        public static async UniTask InitializeRemoteAddressableAsync()
        {
            addressableRemote = Resources.Load<AddressableRemoteConfig>(string.Empty);
            if (addressableRemote == null) return;

            await InitializeRemoteAddressableAsync(addressableRemote);
        }
        
        public static async UniTask InitializeRemoteAddressableAsync(AddressableRemoteConfig config)
        {
            var remotes = config.remotes;
            
        }
        
        public static string TransformInternalId(IResourceLocation location)
        {
            GameLog.Log($"START REPLACE {location.InternalId}",Color.yellow);
        
            if(location.InternalId.Contains(replacementCatalogPath, StringComparison.OrdinalIgnoreCase))
            {
                return location.InternalId; // No change needed
            }

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