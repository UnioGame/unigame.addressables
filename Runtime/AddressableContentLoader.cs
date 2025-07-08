namespace UniGame.AddressableTools.Runtime
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Threading;
	using Cysharp.Threading.Tasks;
	using UnityEngine;
	using UnityEngine.AddressableAssets;
	using UnityEngine.AddressableAssets.ResourceLocators;
	using UnityEngine.ResourceManagement.AsyncOperations;
	using UnityEngine.ResourceManagement.ResourceLocations;

	public class AddressableContentLoader
    {
	    public const float ToMb = 1048576.0f;
	    
        public AsyncOperationHandle downloadHandle;
		public AsyncOperationHandle<IList<IResourceLocation>> validateAddressHandle;
		public bool downloadHandleStatus;
		public IResourceLocator resourceLocator;

		public event Action<float,string> OnLoadingProgressUpdate;
		public event Action OnStartLoading;
		public event Action OnEndLoading;
		public event Action<string> OnLoadingError;

		public async UniTask InitializeAsync(CancellationToken ct)
		{
			Debug.Log("AddressablesLoader InitializeAsync");
			if(resourceLocator!=null)
			{
				Debug.LogWarning("AddressablesLoader Already initialized!");
				return;
			}

			resourceLocator = await Addressables
				.InitializeAsync()
				.ToUniTask(cancellationToken: ct);
			
			validateAddressHandle = Addressables
				.LoadResourceLocationsAsync(resourceLocator.Keys, Addressables.MergeMode.Union);
			
			await validateAddressHandle.Task;
		}

		public async UniTask<bool> LoadAssetBundlesAsync(CancellationToken ct)
		{
			Debug.Log("AddressablesLoader LoadAssetBundlesAsync");
			
			if (downloadHandleStatus)
			{
				Debug.LogWarning("AddressablesLoader AssetBundles already loaded");
				return true;
			}

			if (!validateAddressHandle.IsValid() || 
			    validateAddressHandle.Status != AsyncOperationStatus.Succeeded)
			{
				return false;
			}
			
			//AssetBundle.UnloadAllAssetBundles(true); //for testing
			//await Addressables.ClearDependencyCacheAsync(_validateAddressHandle.Result, false); //for testing

			OnStartLoading?.Invoke();
			
			downloadHandle = Addressables.DownloadDependenciesAsync(validateAddressHandle.Result, false);

			var downLoadSize = await Addressables.GetDownloadSizeAsync(validateAddressHandle.Result) / ToMb;
			var downLoadSizeStr = Math.Round(downLoadSize, 1)
				.ToString(CultureInfo.InvariantCulture);
			
			try
			{
				while (downloadHandle.Status == AsyncOperationStatus.None)
				{
					var status = downloadHandle.GetDownloadStatus();
					var progress = status.Percent;
					var loadedMb = Math.Round(status.DownloadedBytes / ToMb, 1).ToString(CultureInfo.InvariantCulture);
					OnLoadingProgressUpdate?.Invoke(progress, $"Downloading: {loadedMb}/{downLoadSizeStr}");
					//_loadingScreen.ReportWithText(progress, $"Downloading: {loadedMb}/{downLoadSizeStr}");
					await UniTask.DelayFrame(1, PlayerLoopTiming.Update, ct);
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				OnLoadingError?.Invoke(e.Message);
				OnEndLoading?.Invoke();
				return false;
			}

			downloadHandleStatus = downloadHandle.Status == AsyncOperationStatus.Succeeded;

			Addressables.Release(downloadHandle);

			OnEndLoading?.Invoke();
			
			return downloadHandleStatus;
		}
    }
}