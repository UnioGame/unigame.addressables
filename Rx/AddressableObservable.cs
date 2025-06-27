namespace UniGame.AddressableTools.Reactive
{
    using System;
    using Cysharp.Threading.Tasks;
    using Core.Runtime;
    using Core.Runtime.Extension;
    using R3;
    using Runtime;
    using UniGame.Runtime.DataFlow;
    using UniGame.Runtime.Rx;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    using Object = UnityEngine.Object;

    public class AddressableObservable<TAddressable,TData,TApi> : 
        IAddressableObservable<TApi> 
        where TAddressable : AssetReference 
        where TData : Object
        where TApi : class
    {
        private TAddressable _reference;
        private LifeTime _lifeTime;
        private ReactiveValue<TApi> _addressableObservable;

        public AddressableObservable(TAddressable addressable)
        {
            _lifeTime = new LifeTime();
            _addressableObservable = new ReactiveValue<TApi>();
            _lifeTime.AddDispose(_addressableObservable);
            _reference = addressable;
        }
        
        #region public methods

        public AssetReference AssetReference => _reference;
        
        public IDisposable Subscribe(IObserver<TApi> observer)
        {
            if (!ValidateReference()) {
                observer.OnError(new MissingReferenceException($"Asset reference of {this.GetType().Name} is wrong"));
                return Disposable.Empty;
            }

            var disposable = _addressableObservable.Subscribe(observer);

            if (_addressableObservable.HasValue)
                return disposable;
            
            LoadReference(_lifeTime)
                .AttachExternalCancellation(_lifeTime.AsCancellationToken())
                .Forget();
            
            return disposable;
        }

        public void Dispose() => _lifeTime.Terminate();

        #endregion
        
        #region private methods

        private bool ValidateReference()
        {
            if (_reference != null && _reference.RuntimeKeyIsValid()) 
                return true;
            
            return false;

        }
        
        private async UniTask LoadReference(ILifeTime lifeTime)
        {
            var targetType = typeof(TData);
            var apiType = typeof(TApi);

            var isComponent = targetType.IsComponent() || apiType.IsComponent();

            var valueData = isComponent
                ? await _reference.LoadGameObjectAssetTaskAsync<TApi>(lifeTime)
                : await _reference.LoadAssetTaskApiAsync<TData,TApi>(lifeTime);

            await UniTask.SwitchToMainThread();

            _addressableObservable.Value = valueData;
        }
        
        
        #endregion

    }
}
