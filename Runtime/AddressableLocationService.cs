namespace UniGame.AddressableTools.Runtime
{
    using System;
    using UnityEngine;
    using System.Linq;
    using Newtonsoft.Json;
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using UniCore.Runtime.ProfilerTools;
    using UniModules.Runtime.Network;
#if UNITY_EDITOR
    using UnityEditor.AddressableAssets;
#endif
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.ResourceLocations;

    public class AddressableLocationService : IAddressableLocationService
    {
        private const string AddressableRemoteLocationKey = nameof(AddressableRemoteLocationKey);
        
        private bool _isRemoteLocationActive = false;
        private bool _isEnabled = true;
        private string _activeRemoteUrl = string.Empty;
        private string _catalogUrl = string.Empty;
        
        private AddressableRemoteValue _activeLocation;
        
        private Dictionary<string,AddressableRemoteValue> _remoteLocations = new();
        private Dictionary<IResourceLocation,string> _transformCache = new();

        public AddressableLocationService()
        {
            RestoreCachedRemote();
        }

#if UNITY_EDITOR
        public static bool IsAssetDatabase =>
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilder.Name.Contains("Asset Database");
#else
        public static bool IsAssetDatabase => false;
#endif
        
        public IReadOnlyDictionary<string,AddressableRemoteValue> RemoteLocations => _remoteLocations;
        
        public AddressableRemoteValue ActiveRemoteLocation => _activeLocation;

        public bool RestoreCachedRemote()
        {
            if (!PlayerPrefs.HasKey(AddressableRemoteLocationKey)) return false;
            var cachedData = PlayerPrefs.GetString(AddressableRemoteLocationKey, string.Empty);
            var cachedLocation = JsonConvert.DeserializeObject<AddressableRemoteValue>(cachedData);
            if(cachedLocation == null) return false;
            
            Register(cachedLocation);
            
            _activeLocation = cachedLocation;
            _activeRemoteUrl = cachedLocation.remoteUrl;
            
            return true;
        }
        
        public bool SaveCachedRemote(AddressableRemoteValue value)
        {
            if (value == null) return false;
            var data = JsonConvert.SerializeObject(value);
            PlayerPrefs.SetString(AddressableRemoteLocationKey, data);
            PlayerPrefs.Save();
            return true;
        }
        
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
            if (!string.IsNullOrEmpty(_activeRemoteUrl) &&
                _activeRemoteUrl.Equals(remoteUrl, StringComparison.OrdinalIgnoreCase))
            {
                _activeLocation = null;
                _activeRemoteUrl = string.Empty;
            }
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
        
        public async UniTask<AddressableRemoteResult> ActivateRemoteLocationAsync(AddressableRemoteValue remoteLocation)
        {
            var result = new AddressableRemoteResult()
            {
                success = false,
                error = string.Empty,
                url = remoteLocation.remoteUrl,
            };

            //if remote location is not registered - add it to the list
            if (!_remoteLocations.TryGetValue(remoteLocation.remoteUrl, out var location))
            {
                location = remoteLocation;
                Register(remoteLocation);
            }
            
            if(location.enabled == false) return result;

            var remoteUrl = location.remoteUrl;
            
            if (remoteUrl.Equals(_activeRemoteUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                result.success = true;
                return result;
            }
            
            var catalog = location.remoteCatalogName;
            var catalogUrl = string.IsNullOrEmpty(catalog) 
                ? string.Empty
                : $"{remoteUrl.TrimEnd('/')}/{catalog.TrimStart('/')}";
            
            var resourceLocator = await Addressables.InitializeAsync().ToUniTask();
            
            if (string.IsNullOrEmpty(catalogUrl) == false)
            {
                // Reload the catalog with the new URL if necessary
                // You might need to unload the current catalog and load a new one
                var catalogResult = await Addressables
                    .LoadContentCatalogAsync(catalogUrl)
                    .ToUniTask();

                if (catalogResult == null)
                {
                    var message = "Failed to load content catalog from URL: " + catalogUrl;
                    GameLog.LogError(message);
                    result.error = message;
                    return result;
                }
            }

            if (_activeRemoteUrl == null ||
                !_activeRemoteUrl.Equals(remoteUrl, StringComparison.OrdinalIgnoreCase))
            {
                SaveCachedRemote(location);
                // active remote location is changed - clear the cache
                _transformCache.Clear();
                if (!IsAssetDatabase)
                    await Addressables.CleanBundleCache();
            }
            
            _activeLocation = location;
            _activeRemoteUrl = remoteUrl;
            _catalogUrl = catalogUrl;
            
            if (_isRemoteLocationActive == false)
            {
                Addressables.ResourceManager.InternalIdTransformFunc = null;
                Addressables.ResourceManager.InternalIdTransformFunc = TransformInternalId;
                _isRemoteLocationActive = true;
            }

            result.url = remoteUrl;
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
                return cachedId;

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
                break;
            }
            
            //add into cache
            _transformCache[location] = resultId;
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