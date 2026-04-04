# UniGame.AddressableTools

UniGame.AddressableTools is a practical layer on top of Unity Addressables for game code. It reduces boilerplate around asset loading, lifetime-based cleanup, prefab spawning, pooling, and remote catalog selection.

## Why use it

- Load addressable assets with `ILifeTime` and release them automatically.
- Use strongly typed references for components and scriptable objects.
- Spawn and warm up addressable prefabs with a small API surface.
- Switch remote addressable sources without wiring custom infrastructure each time.
- Fix common Addressables project issues from a compact set of editor commands.

Using `ILifeTime` makes resource management easier because loading and cleanup stay tied to the same gameplay scope. Instead of manually tracking handles and release points, you bind the asset to a lifetime such as a screen, scene, system, or spawned object and let cleanup happen when that scope ends.

## Installation

Add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unigame.addressablestools": "https://github.com/UnioGame/unigame.addressables.git"
  }
}
```

Core dependencies:

```json
{
  "com.unity.addressables": "2.8.1",
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
  "com.unigame.unicore": "https://github.com/UnioGame/unigame.core.git"
}
```

Optional:

- Define `UNIGAME_RX_ENABLED` to enable reactive extensions.
- Odin Inspector attributes are used conditionally when available.

## Quick start

### Load an asset with automatic cleanup

```csharp
using Cysharp.Threading.Tasks;
using UniGame.AddressableTools.Runtime;
using UniGame.Core.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class PrefabLoader : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject prefabReference;

    private LifeTime _lifeTime;

    private void Awake()
    {
        _lifeTime = new LifeTime();
    }

    private async void Start()
    {
        var prefab = await prefabReference.LoadAssetTaskAsync(_lifeTime);
        var instance = await prefabReference.SpawnObjectAsync<GameObject>(
            position: transform.position,
            parent: transform,
            lifeTime: _lifeTime,
            activateOnSpawn: true);
    }

    private void OnDestroy()
    {
        _lifeTime.Terminate();
    }
}
```

### Use a typed component reference

```csharp
using UniGame.AddressableTools.Runtime;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private AssetReferenceComponent<PlayerController> playerReference;

    public async void Spawn(ILifeTime lifeTime, Transform parent)
    {
        var player = await playerReference.SpawnObjectAsync<PlayerController>(
            position: Vector3.zero,
            parent: parent,
            lifeTime: lifeTime,
            activateOnSpawn: true);

        player.Initialize();
    }
}
```

### Preload a pool before gameplay starts

```csharp
using UniGame.AddressableTools.Runtime;
using UnityEngine.AddressableAssets;

public async UniTask WarmupWeaponPool(AssetReferenceGameObject projectile, ILifeTime lifeTime)
{
    await projectile.AttachPoolLifeTimeAsync(lifeTime, preloadCount: 32);
    await projectile.WarmUp(lifeTime, count: 16, activate: false);
}
```

## Core runtime API

The package is built around a few methods that cover most daily usage.

### Loading and spawning

```csharp
var texture = await textureReference.LoadAssetTaskAsync<Texture2D>(lifeTime);

var instance = await prefabReference.LoadAssetInstanceTaskAsync<GameObject>(
    lifeTime,
    destroyInstanceWithLifetime: true);

var spawned = await prefabReference.SpawnObjectAsync<GameObject>(
    position: Vector3.zero,
    parent: transform,
    lifeTime: lifeTime,
    activateOnSpawn: true);
```

`SpawnObjectAsync` also works without an explicit lifetime. If `lifeTime` is `null`, the loaded addressable handle is attached to the spawned instance lifetime. When that instance is destroyed and no other references keep the asset alive, the addressable reference can be released automatically.

### Bulk loading and scene loading

```csharp
var sprites = await spriteReferences.LoadAssetsTaskAsync<Sprite>(lifeTime);

var scene = await sceneReference.LoadSceneTaskAsync(
    lifeTime,
    loadSceneMode: LoadSceneMode.Additive);
```

### Dependencies and cache

```csharp
await prefabReference.DownloadDependencyAsync(lifeTime);

var progress = new Progress<float>(value => Debug.Log($"Download: {value:P}"));
var asset = await prefabReference.LoadAssetTaskAsync<GameObject>(lifeTime, true, progress);

await AddressableExtensions.ClearCacheAsync();
```

## Typed references

Typed references remove repeated casts and make inspector usage safer.

### Component references

```csharp
[SerializeField] private AssetReferenceComponent<PlayerController> player;
[SerializeField] private AssetReferenceComponent<Rigidbody, IMovable> movable;
```

### ScriptableObject references

```csharp
[SerializeField] private AssetReferenceScriptableObject<GameBalance> balance;
[SerializeField] private AssetReferenceScriptableObject<GameSettings, IGameSettings> settings;
```

Use typed references when the consuming code expects a concrete API, not just a raw `Object` or `GameObject`.

## Pooling and preload

Pooling helpers are useful when you repeatedly spawn the same addressable prefabs during gameplay.

```csharp
await projectileReference.AttachPoolLifeTimeAsync(lifeTime, preloadCount: 50);

var pooled = await projectileReference.SpawnAsync(
    lifeTime,
    firePoint.position,
    firePoint.rotation,
    parent: null,
    stayPosition: true);

var active = await projectileReference.SpawnActiveAsync(
    lifeTime,
    firePoint.position,
    firePoint.rotation);

active.Despawn();
```

For scene-level warmup, use the preload helpers to prime pools before they are needed:

```csharp
public class BootstrapPreloader : MonoBehaviour
{
    [SerializeField] private AddressableMonoPreloader preloader;

    private void Start()
    {
        preloader.WarmUp();
    }
}
```

## Remote addressables

Use `AddressableRemoteConfig` when your project needs multiple remote content sources or runtime catalog switching.

`AddressableRemoteConfig` lets you define:

- whether remote loading is enabled
- whether the first selected remote should remain permanent
- endpoint probe retry count and timeout
- a list of remote sources with `testUrl`, `remoteUrl`, and `remoteCatalogName`

Example bootstrap:

```csharp
using UniGame.AddressableTools.Runtime;
using UniGame.Core.Runtime;

public async UniTask<IAddressableLocationService> InitializeRemotes(
    AddressableRemoteConfig config,
    ILifeTime lifeTime)
{
    var service = await AddressableTools.CreateAddressableLocationService(config, lifeTime);
    return service;
}
```

What this gives you:

- remote registration from config
- fastest endpoint selection based on `testUrl`
- activation of the selected remote catalog
- cached active remote between sessions
- cache reset when the active remote changes

## Editor utilities

The package includes a small set of maintenance commands under `UniGame/Addressables/`:

- `Validate Addressables Errors`
- `Fix Addressables Errors`
- `Remove Empty Groups`
- `Remove Missing References`
- `Clean Library Cache`
- `Clean Default Context Builder`
- `Clean All`
- `Print Variables`

These tools are intended for common Addressables maintenance tasks, not for full editor workflow customization.

## Recommended usage patterns

- Always pass a valid `ILifeTime` when loading or spawning addressable content.
- Use typed references when code depends on a specific component or API.
- Warm up pools for high-frequency gameplay prefabs before the first use.
- Keep remote source configuration centralized in `AddressableRemoteConfig`.
- Use the editor commands to recover from broken Addressables state before editing settings manually.

## Notes

- This package is most useful when your project already uses UniTask and UniGame lifetime patterns.
- Reactive extensions are optional and should stay out of the core integration unless the project already uses them.
- Some inspector enhancements are enabled only when Odin Inspector is present.

## License

MIT License. See `LICENSE` for details.