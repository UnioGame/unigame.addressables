namespace UniGame.AddressableTools.Runtime
{
    using System.Threading;
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
        
        public static async UniTask<IAddressableLocationService> CreateAddressableLocationService(AddressableRemoteConfig config,ILifeTime lifeTime)
        {
            var remoteConfig  = config;

            if (remoteConfig == null || remoteConfig.enabled == false)
            {
                GameLog.LogError("Remote addressable configuration is not set or disabled.");
                return null;
            }
            
            var addressableLocations = new AddressableLocationService();
            //register remote locations
            foreach (var remoteLocation in remoteConfig.remotes)
            {
                addressableLocations.Register(remoteLocation);
            }

            //select default active remote location
            var locationResult = await addressableLocations
                .SelectRemoteLocationAsync(remoteConfig.urlTriesCount, remoteConfig.timeoutSeconds);

            if (locationResult.success == false)
            {
                GameLog.LogError($"Addressable Remote Location: Failed to select remote location after .");
                return null;
            }
            
            var remoteUrl = locationResult.url;

            var activateResult = await addressableLocations
                .ActivateRemoteLocationAsync(remoteUrl);

            if (activateResult.success == false)
                return null;

            lifeTime.AddDispose(addressableLocations);
            
            return addressableLocations;
        }

    }
    
    
}   