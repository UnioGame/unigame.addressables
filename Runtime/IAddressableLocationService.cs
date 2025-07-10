namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using UniModules.Runtime.Network;
    using UnityEngine.ResourceManagement.ResourceLocations;

    public interface IAddressableLocationService : IDisposable
    {
        IReadOnlyDictionary<string, AddressableRemoteValue> RemoteLocations { get; }
        AddressableRemoteValue ActiveRemoteLocation { get; }
        /// <summary>
        /// allow or disallow remote location transformation
        /// </summary>
        void SetStatus(bool isActive);
        /// <summary>
        /// register remote location, only registered locations can be used for remote addressable remote loading
        /// </summary>
        void Register(AddressableRemoteValue remote);
        /// <summary>
        /// Remove remote location from registered list.
        /// </summary>
        bool Remove(AddressableRemoteValue remote);
        bool Remove(string remoteUrl);
        /// <summary>
        /// Try to select best remote location for addressable system.
        /// </summary>
        UniTask<AddressableRemoteResult> SelectRemoteLocationAsync(int tries = 3,int timeout = 5);
        
        UniTask<AddressableRemoteResult> ActivateRemoteLocationAsync(AddressableRemoteValue location);
        
        UniTask<AddressableRemoteResult> ActivateRemoteLocationAsync(string remoteUrl);

        /// <summary>
        /// validate is transform location can be updated
        /// </summary>
        /// <param name="location">target location</param>
        /// <returns>is location should be updated</returns>
        bool ValidateTransform(IResourceLocation location);
    }
}