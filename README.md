# UniGame.AddressableTools

A comprehensive toolkit for working with Unity Addressables system, providing convenient extensions, components, and services for resource management.

# 🚀 Features

- ✅ Simplified work with Addressable resources
- ✅ Automatic resource lifecycle management
- ✅ Typed resource references 
- ✅ Object pooling system

## 📦 Installation

The module is part of UniGame.CoreModules and is automatically included in the project.

Add the following dependencies to your `Packages/manifest.json` file:

```json
  "dependencies": {
    "com.unigame.addressablestools" : "https://github.com/UnioGame/unigame.addressables.git",
  }
```

To enable RX/R3 support add the following script define symbols to your project: "UNIGAME_RX_ENABLED"

### Dependencies

```json
{
    "com.unity.addressables": "2.6.0",
    "com.cysharp.unitask" : "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.unigame.unicore": "https://github.com/UnioGame/unigame.core.git",
    "com.unigame.rx": "https://github.com/UnioGame/unigame.rx.git"
}
```

# ⚡ Quick Start

## Basic Resource Loading

```csharp
using UniGame.AddressableTools.Runtime;
using UniGame.Core.Runtime;
using Cysharp.Threading.Tasks;

public class ResourceLoader : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject prefabReference;
    private LifeTimeDefinition _lifeTime = new();

    private async void Start()
    {
        // Load addressable object and allow to unload it by lifeTime
        var gameObject = await prefabReference.LoadAssetTaskAsync<GameObject>(_lifeTime);
        
        // create instance of the addressable object
        var gameObjectInstance = await prefabReference
            .LoadAssetInstanceTaskAsync<GameObject>(_lifeTime,destroyInstanceWithLifetime : true);
        
        var instance = await prefabReference.SpawnObjectAsync<GameObject>(
            transform.position, 
            transform, 
            _lifeTime);
            
        // Object will be automatically released when _lifeTime terminates
    }

    private void OnDestroy() => _lifeTime.Terminate();
}
```

## Addressable Mono Components

```csharp
[SerializeField] private AssetReferenceComponent<PlayerController> playerReference;

private async void SpawnPlayer()
{
    var player = await playerReference.SpawnObjectAsync<PlayerController>(
        spawnPoint.position,
        parent: gameWorld,
        lifeTime: _lifeTime,
        activateOnSpawn : true);
    
    // player already contains the required component
    player.Initialize();
}
```

# 🔧 Extensions

```csharp
// Load single resource
var asset = await assetReference.LoadAssetTaskAsync<Texture2D>(lifeTime);

// Load with instance creation
var instance = await assetReference.LoadAssetInstanceTaskAsync<GameObject>(
    lifeTime, 
    destroyInstanceWithLifetime: true);

// Create object in world
var spawned = await assetReference.SpawnObjectAsync<GameObject>(
    position: Vector3.zero,
    parent: transform,
    lifeTime: lifeTime);

// Load list of resources
var assets = await assetReferences.LoadAssetsTaskAsync<Sprite>(lifeTime);
```

# Addressable Remotes

tools for simplify management of remote addressable sources

base remote addressable configuration:

```csharp

    /// <summary>
    /// Define remote addressable sources
    /// to activate configuration to scriptable asset into resources folder
    /// don't forget to enable property in AddressableRemoteConfig
    /// </summary>
    [CreateAssetMenu(menuName = "UniGame/Addressables/AddressableRemoteConfig", fileName = "AddressableRemoteConfig")]
    public class AddressableRemoteConfig : ScriptableObject
    {
        /// <summary>
        /// Enable or disable addressable remote sources
        /// </summary>
        public bool enabled = true;
        /// <summary>
        /// amount of single url tries to check remote source availability
        /// </summary>
        public int urlTriesCount = 3;
        /// <summary>
        /// remote utl check timeout in seconds
        /// </summary>
        public int timeoutSeconds = 5;
        /// <summary>
        /// list of remote addressables sources
        /// is no default value is set, then first remote will be used as default
        /// </summary>
#if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = "@name")]
#endif
        public List<AddressableRemoteValue> remotes = new();
    }
````

```csharp
    public class AddressableRemoteValue
    {
        /// <summary>
        /// Name of remote source, used for identification
        /// </summary>
        public string name;
        /// <summary>
        /// if false, then addressable tool will not use this remote source
        /// </summary>
        public bool enabled = true;
        /// <summary>
        /// test url for checking remote source availability
        /// </summary>
        public string testUrl;
        /// <summary>
        /// Remote url for addressable system source like CDN or remote server
        /// </summary>
        public string remoteUrl;
        /// <summary>
        /// Name of remote catalog, which will be used for addressable system
        /// </summary>
        public string remoteCatalogName;
    }
```

## 🧰 Usage

- Set up remote addressable configuration
- Create `AddressableLocationService` instance for managing remote sources

```csharp

    AddressableRemoteConfig configuration;
    LifeTime lifeTime = new()

    /// Create addressable remote location service.
    /// Register all remote locations from configuration.
    var addressableLocationService ??= await AddressableTools
        .CreateAddressableLocationService(configuration,lifeTime);

    // Get best remote location registered in service
    var bestLocation = await addressableLocationService
        .SelectRemoteLocationAsync(tries:3,timeout:5); 
    
    if(bestLocation.success)
    {
        /// Activate remote location by url, the url should be registered in configuration
        var urlActivateResult = addressableLocationService
            .ActivateRemoteLocationAsync(bestLocation.url);
    }
    
    // just for the demo select first remote location
    var location = addressableLocationService.RemoteLocations.Values.FirstOrDefault();
    /// Activate remote location by one of the registered locations
    var activateResult = addressableLocationService
        .ActivateRemoteLocationAsync(location);
        
```

Remember:

1. Write into `AddressableRemoteConfig` all your remote addressable sources, it's will be used for replacement and selection the best
2. If you change remote addressable configuration for new remote, Addressable cache will be cleared

# 🧩 Mono Tools

## AddressableInstancer

Component for automatic creation of objects from Addressable resources.

```csharp
public class AddressableInstancer : MonoBehaviour
{
    [SerializeField] private List<AddressableInstance> links;
    [SerializeField] private bool createOnStart = true;
    [SerializeField] private bool unloadOnDestroy = true;
    
    // Transform settings
    [SerializeField] private Transform parent;
    [SerializeField] private Vector3 position;
    [SerializeField] private Quaternion rotation;
}
```

### Working with Dependencies

```csharp
// Preload dependencies
await assetReference.DownloadDependencyAsync(lifeTime);

// Load with progress
var progress = new Progress<float>(p => Debug.Log($"Progress: {p:P}"));
await assetReference.LoadAssetTaskAsync<GameObject>(lifeTime, true, progress);

// Clear cache
await AddressableExtensions.ClearCacheAsync();
```

## 🎱 Object Pooling

### Pool Creation

```csharp
// Create pool with preloading, all objects will be automatically released when the lifetime ends
await bulletPrefab.AttachPoolLifeTimeAsync(lifeTime, preloadCount: 50);

// Warm up pool
await bulletPrefab.WarmUp(lifeTime, count: 20, activate: false);
```

### Using Pool

```csharp
// Create object from pool
var bullet = await bulletPrefab.SpawnAsync(lifeTime, firePoint.position, firePoint.rotation);

// Create active object
var activeBullet = await bulletPrefab.SpawnActiveAsync(lifeTime, firePoint);

activeBullet.Despawn(); // Return to pool
```

# 🛠️ Editor Tools

## Validation and Fixing

**Menu:** `UniGame/Addressables/`

- `Validate Addressables Errors` - Check for errors
- `Fix Addressables Errors` - Automatic fixing
- `Remove Missing References` - Remove broken references
- `Remote Empty Groups` - Remove empty groups

# 📚 Examples

## Level Loader

```csharp
public class LevelLoader : MonoBehaviour
{
    [SerializeField] private AssetReference levelScene;
    [SerializeField] private List<AssetReferenceGameObject> levelPrefabs;
    private LifeTimeDefinition _levelLifeTime = new();

    public async UniTask LoadLevel()
    {
        // Load scene
        var sceneInstance = await levelScene.LoadSceneTaskAsync(
            _levelLifeTime, 
            LoadSceneMode.Additive);
    }

    public void UnloadLevel() => _levelLifeTime.Release();
}
```

# Notes

### Lifecycle Management

Always use `ILifeTime` for automatic resource cleanup:

```csharp
// ✅ Correct
var asset = await reference.LoadAssetTaskAsync<GameObject>(lifeTime);

// ❌ Incorrect - resource won't be released
var asset = await reference.LoadAssetTaskAsync<GameObject>(null);
```

# 📄 License

MIT License - see LICENSE file for details.