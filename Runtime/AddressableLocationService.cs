namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using Newtonsoft.Json;
    using UniCore.Runtime.ProfilerTools;
    using UniModules.Runtime.Network;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.ResourceLocations;

    public class AddressableLocationService : IAddressableLocationService
    {
        private const string RemoteAddressableLocationKey = nameof(RemoteAddressableLocationKey);
        
        private bool _isRemoteLocationActive = false;
        private bool _isEnabled = true;
        private AddressableRemoteValue _activeLocation;
        private Dictionary<string,AddressableRemoteValue> _remoteLocations = new();
        private string _activeRemoteUrl = string.Empty;
        private string _catalogUrl = string.Empty;
        private AddressableRemoteValue _activeRemote;
        private Dictionary<IResourceLocation,string> _transformCache = new();

        public IReadOnlyDictionary<string,AddressableRemoteValue> RemoteLocations => _remoteLocations;
        
        public AddressableRemoteValue ActiveRemoteLocation => _activeLocation;
        
        public void SetStatus(bool isActive)
        {
            _isEnabled = isActive;
        }
        
        public void Register(AddressableRemoteValue remote)
        {
            if (remote.enabled == false) return;
            _remoteLocations[remote.remoteUrl] = remote;
        }
        
        public bool Remove(AddressableRemoteValue remote)
        {
            return Remove(remote.remoteUrl);
        }
        
        public bool Remove(string remoteUrl)
        {
            return _remoteLocations.Remove(remoteUrl);
        }
        
        public async UniTask<AddressableRemoteResult> SelectRemoteLocationAsync(int tries = 3,int timeout = 5)
        {
            var result = new AddressableRemoteResult()
            {
                success = false,
                error = string.Empty,
                url = string.Empty,
            };
            
            if (_remoteLocations.Count == 0) return result;
            
            var enabledRemote = _remoteLocations
                .Values
                .Where(x => x.enabled)
                .ToList();

            var checkUrls = enabledRemote
                .Select(x => x.testUrl);

            var bestResult = await UrlChecker.SelectFastestEndPoint(checkUrls, tries, timeout);
            var resultUrl = bestResult.url;
            var success = bestResult.success;
            
            if (success)
            {
                var value = _remoteLocations
                    .FirstOrDefault(x => x.Value.testUrl == resultUrl);
                resultUrl = value.Key;
            }
            
            result.success = success;
            result.url = resultUrl;
            result.error = success ? string.Empty : $"Failed to select remote location after tries.";
            
            return result;
        }
        
        public async UniTask<AddressableRemoteResult> ActivateRemoteLocationAsync(AddressableRemoteValue location)
        {
            var result = new AddressableRemoteResult()
            {
                success = false,
                error = string.Empty,
                url = location.remoteUrl,
            };
            
            if(location.enabled == false)
                return result;

            var remoteUrl = location.remoteUrl;
            
            if (remoteUrl.Equals(_activeRemoteUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                result.success = true;
                return result;
            }
            
            _activeLocation = location;
            _activeRemoteUrl = remoteUrl;
            
            var catalog = _activeLocation.remoteCatalogName;
            _catalogUrl = string.IsNullOrEmpty(catalog) 
                ? string.Empty
                : $"{_activeRemoteUrl.TrimEnd('/')}/{catalog.TrimStart('/')}";
            
            // active remote location is changed - clear the cache
            _transformCache.Clear();
            
            var resourceLocator = await Addressables.InitializeAsync().ToUniTask();
            
            if (string.IsNullOrEmpty(_catalogUrl) == false)
            {
                // Reload the catalog with the new URL if necessary
                // You might need to unload the current catalog and load a new one
                var catalogResult = await Addressables
                    .LoadContentCatalogAsync(_catalogUrl)
                    .ToUniTask();

                if (catalogResult != null)
                {
                    var data = JsonConvert.SerializeObject(result);
                    GameLog.Log($"Addressable Catalog loaded with new path: \n{data}", Color.green);
                }
            }

            await Addressables.CleanBundleCache();

            if (_isRemoteLocationActive == false)
            {
                Addressables.ResourceManager.InternalIdTransformFunc = null;
                Addressables.ResourceManager.InternalIdTransformFunc = TransformInternalId;
                _isRemoteLocationActive = true;
            }

            result.url = _activeRemoteUrl;
            result.success = true;
            result.error = string.Empty;

            return result;
        }
        
        public async UniTask<AddressableRemoteResult> ActivateRemoteLocationAsync(string remoteUrl)
        {
            var result = new AddressableRemoteResult()
            {
                success = false,
                error = string.Empty,
                url = remoteUrl,
            };
            
            if (string.IsNullOrEmpty(remoteUrl))
                return result;
            
            if (!_remoteLocations.TryGetValue(remoteUrl, out var location))
                return result;

            result = await ActivateRemoteLocationAsync(location);
            
            return result;
        }
        
        /// <summary>
        /// validate is transform location can be updated
        /// </summary>
        /// <param name="location">target location</param>
        /// <returns>is location should be updated</returns>
        public bool ValidateTransform(IResourceLocation location)
        {
            if (_isRemoteLocationActive == false || _isEnabled == false)
                return false;

            if (string.IsNullOrEmpty(_activeRemoteUrl)) return false;
            
            var internalId = location.InternalId;
            if (string.IsNullOrEmpty(internalId)) return false;
            
            if (internalId.Contains(_activeRemoteUrl, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        
        public string TransformInternalId(IResourceLocation location)
        {
            var internalId = location.InternalId;
            
            var validationResult = ValidateTransform(location);
            if (!validationResult) return location.InternalId;
            
            //is we already transformed this location -  return cached id
            if(_transformCache.TryGetValue(location, out var cachedId))
            {
                GameLog.Log($"Transform cache hit for {location.InternalId} => {cachedId}", Color.green);
                return cachedId;
            }

            var resultId = internalId;
            var replaced = false;
            
            foreach (var locationPair in _remoteLocations)
            {
                var key = locationPair.Key;
                var locationValue = locationPair.Value;
                if(!locationValue.enabled) continue;
                
                if(!internalId.Contains(key,StringComparison.InvariantCultureIgnoreCase))    
                    continue;
                
                resultId = internalId.Replace(key,_activeRemoteUrl);
                replaced = true;
                
                GameLog.Log($"Transforming {internalId} to {resultId} using remote location {key}", Color.green);
                
                break;
            }
            
            //add into cache
            _transformCache[location] = resultId;

            GameLog.Log($"Addressable Remote: RESULT {replaced} : {resultId} | \nORIGIN ={internalId}", Color.green);

            return resultId; // Return the original ID if no replacement is needed
        }

        public void Dispose()
        {
            if (_isRemoteLocationActive == false)
            {
                Addressables.ResourceManager.InternalIdTransformFunc = null;
            }
            
            _isRemoteLocationActive = false;
            _transformCache.Clear();
            _remoteLocations.Clear();
        }
    }

    public struct AddressableRemoteResult
    {
        public bool success;
        public string url;
        public string error;
    }
}