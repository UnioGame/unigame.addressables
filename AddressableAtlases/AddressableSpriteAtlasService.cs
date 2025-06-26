using UniCore.Runtime.ProfilerTools;
using UniGame.GameFlow.Runtime;
using UniGame.AddressableTools.Runtime;

namespace UniGame.AddressableAtlases
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using Runtime;
    using Cysharp.Threading.Tasks;
    
    using UniGame.Runtime.DataFlow;
    using UnityEngine;
    using UnityEngine.U2D;
    
    [Serializable]
    public class AddressableSpriteAtlasService : GameService, IAddressableAtlasService
    {
        #region inspector

#if ODIN_INSPECTOR
        [InlineProperty]
        [HideLabel]
#endif
        [SerializeField]
        public AddressableAtlasSettings configuration;
        
#if ODIN_INSPECTOR
        [InlineProperty]
        [Searchable]
#endif
        public Dictionary<string, AddressableAtlasState> atlases = new();
        
        #endregion

        public AddressableSpriteAtlasService(AddressableAtlasSettings settings)
        {
            configuration = settings;

            foreach (var atlasData in configuration.atlases)
            {
                var stateLifeTime = new LifeTime().AddTo(LifeTime);
                
                atlases[atlasData.tag] = new AddressableAtlasState()
                {
                    tag = atlasData.tag,
                    atlasData = atlasData,
                    isLoaded = false,
                    lifeTime = stateLifeTime,
                };
            }
            
            BindToAtlasManager();
            PreloadAtlases();
            
            LifeTime.AddCleanUpAction(CleanUp);
        }
        
        public async UniTask<SpriteAtlas> LoadAtlasAsync(string tag)
        {
            if (atlases.TryGetValue(tag, out var atlasState) == false)
                return null;

            if(atlasState.isLoaded) return atlasState.atlas;
            
            var atlasData = atlasState.atlasData;
            var atlas = await atlasData.reference.LoadAssetTaskAsync(atlasState.lifeTime);
            
            atlasState.atlas = atlas;
            atlasState.isLoaded = atlas != null;
            
            GameLog.Log($"ATLAS: OnSpriteAtlasRequested : TAG {tag} GUID {atlasData.guid}", Color.magenta);

            return atlas;
        }
        
        public void RegisterSpriteAtlas(SpriteAtlas atlas)
        {
            OnSpriteAtlasRegistered(atlas);
        }

        private void PreloadAtlases()
        {
            foreach (var atlasState in atlases)
            {
                var stateData = atlasState.Value;
                if (stateData.atlasData.preload)
                    LoadAtlasAsync(stateData.tag).Forget();
            }
        }

        private void BindToAtlasManager()
        {
            SpriteAtlasManager.atlasRequested -= ProceedSpriteAtlasRequested;
            SpriteAtlasManager.atlasRequested += ProceedSpriteAtlasRequested;
            
            SpriteAtlasManager.atlasRegistered -= OnSpriteAtlasRegistered;
            SpriteAtlasManager.atlasRegistered += OnSpriteAtlasRegistered;
        }

        private void CleanUp()
        {
            SpriteAtlasManager.atlasRequested -= ProceedSpriteAtlasRequested;
            SpriteAtlasManager.atlasRegistered -= OnSpriteAtlasRegistered;
        }

        public void ProceedSpriteAtlasRequested(string tag, Action<SpriteAtlas> atlasAction)
        {
            GetSpriteAtlasAsync(tag, atlasAction)
                .AttachExternalCancellation(LifeTime.Token)
                .Forget();
        }

        private async UniTask<SpriteAtlas> GetSpriteAtlasAsync(string tag, Action<SpriteAtlas> atlasAction)
        {
            var atlas = await LoadAtlasAsync(tag);

            if (atlas)
            {
                atlasAction(atlas);
            }
            else
            {
                GameLog.LogError($"ATLAS: Null Atlas Result by TAG {tag}");
            }
            
            return atlas;
        }
        
        private void OnSpriteAtlasRegistered(SpriteAtlas atlas)
        {
#if UNITY_EDITOR
            GameLog.Log($"ATLAS: SpriteAtlasConfiguration : {nameof(OnSpriteAtlasRegistered)} : {atlas.tag}",
                Color.magenta);
#endif
            var tag = atlas.tag;
            
            if (!atlases.TryGetValue(tag, out var atlasState))
            {
                atlasState = new AddressableAtlasState()
                {
                    lifeTime = new LifeTime().AddTo(LifeTime),
                    atlasData = new AddressableAtlasData(),
                    isLoaded = false,
                    tag = tag,
                };
            }
            
            atlasState.atlas = atlas;
            atlasState.isLoaded = atlas != null;
            atlases[tag] = atlasState;
        }


#if UNITY_EDITOR
        private static MethodInfo _registerMethod;
        private const string AtlasRegisterMethodName = "Register";

        private static MethodInfo RegisterMethod => (_registerMethod = _registerMethod == null
            ? typeof(SpriteAtlasManager).GetMethod(AtlasRegisterMethodName,
                BindingFlags.Static | BindingFlags.NonPublic)
            : _registerMethod);

        [Conditional("UNITY_EDITOR")]
        private void RegisterAtlas(SpriteAtlas atlas) => RegisterMethod?.Invoke(null, new object[] { atlas });

#endif
    }
}