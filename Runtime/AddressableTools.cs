namespace UniGame.AddressableTools.Runtime
{
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public static class AddressableTools
    {
        private static AddressableLocationHandler addressableRemote;
        private static AddressableRemoteConfig remoteConfig;
        private static bool initialized = false;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // Initialization logic can be added here if needed
            InitializeAsync().Forget();
        }

        public static async UniTask InitializeAsync()
        {
            initialized = false;

            await InitializeRemoteAddressableAsync();
            
            initialized = true;
        }
        
        public static async UniTask WaitForReadyAsync()
        {
            if (initialized) return;
            await UniTask.WaitUntil(static () => initialized);
        }
        
        public static async UniTask InitializeRemoteAddressableAsync()
        {
            remoteConfig  = Resources.Load<AddressableRemoteConfig>(string.Empty);
            if (remoteConfig == null || remoteConfig.enabled == false) return;
            
            
            addressableRemote = new();
            foreach (var remoteLocation in remoteConfig.remotes)
            {
                addressableRemote.Register(remoteLocation);
            }

            await addressableRemote.SelectRemoteLocationAsync();
        }

    }
    
    
}   