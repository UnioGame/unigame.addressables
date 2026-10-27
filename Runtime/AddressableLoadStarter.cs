using System;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[assembly: InternalsVisibleTo("unigame.addressables.tools.tests")]

namespace UniGame.AddressableTools.Runtime
{
    /// <summary>
    /// The one place <see cref="AddressableExtensions.LoadReferenceAsync{T}"/> starts an Addressables load. Tests replace
    /// <see cref="Start"/> with a controllable operation; production code never touches it.
    /// </summary>
    internal static class AddressableLoadStarter<T>
    {
        internal static readonly Func<string, AsyncOperationHandle<T>> Default =
            key => Addressables.LoadAssetAsync<T>(key);

        internal static Func<string, AsyncOperationHandle<T>> Start = Default;

        internal static void Reset() => Start = Default;
    }
}
