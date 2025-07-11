namespace UniGame.AddressableTools.Runtime
{
    using System.Linq;
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using UniCore.Runtime.ProfilerTools;
    using UnityEngine;

    public static class AddressableTools
    {
        public static async UniTask<IAddressableLocationService> CreateAddressableLocationService(ILifeTime lifeTime)
        {
            var remoteConfig  = Resources.Load<AddressableRemoteConfig>(string.Empty);
            return await CreateAddressableLocationService(remoteConfig, lifeTime);
        }
        
        /// <summary>
        /// Create addressable remote location service.
        /// Register all remote locations from configuration.
        /// </summary>
        public static async UniTask<IAddressableLocationService> CreateAddressableLocationService(AddressableRemoteConfig config,ILifeTime lifeTime)
        {
            var remoteConfig  = config;

            if (remoteConfig == null || remoteConfig.enabled == false)
            {
                GameLog.LogError("Remote addressable configuration is not set or disabled.");
                return null;
            }

            var remotes = remoteConfig.remotes;
            var locationService = new AddressableLocationService();
            var isPermanent = config.isPermanentRemote;
            
            if (locationService.ActiveRemoteLocation != null)
            {
                var activeLocation = locationService.ActiveRemoteLocation;
                var foundLocation = remotes
                    .FirstOrDefault(x => x.remoteUrl.Equals(activeLocation.remoteUrl));
                if (foundLocation == null)
                    locationService.Remove(activeLocation);
            }
            
            //register remote locations
            foreach (var remoteLocation in remotes)
            {
                locationService.Register(remoteLocation);
            }

            var remoteUrl = locationService.ActiveRemoteLocation?.remoteUrl;
            
            if (!isPermanent || locationService.ActiveRemoteLocation == null)
            {
                //select default active remote location
                var locationResult = await locationService
                    .SelectRemoteLocationAsync(remoteConfig.urlTriesCount, remoteConfig.timeoutSeconds);
                
                if (locationResult.success == false)
                {
                    GameLog.LogError($"Addressable Remote Location: Failed to select remote location after .");
                    return null;
                }
                
                GameLog.Log($"Addressable Remote Location: Selected remote location = {locationResult.url}",Color.green);
                
                remoteUrl = locationResult.url;
            }

            var activateResult = await locationService
                .ActivateRemoteLocationAsync(remoteUrl);

            if (activateResult.success == false) return null;

            lifeTime.AddDispose(locationService);
            
            return locationService;
        }

    }
    
    
}   