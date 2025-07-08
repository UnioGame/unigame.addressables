using System;
using UniGame.Core.Runtime.ObjectPool;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniGame.AddressableTools.Runtime
{
    public interface IAsyncHandleStatus : IObservable<IAsyncHandleStatus>
    {
        AsyncOperationStatus Status{ get;}
        
        long TotalBytes { get; }
     
        long DownloadedBytes { get; }
        
        bool  IsDone { get; }
        
        float Percent { get; }
        
        Exception OperationException { get; }
    }

    public interface IPoolableAsyncHandleStatus : 
        IAsyncHandleStatus,
        IPoolable
    {
        void DespawnHandleStatus();
    }
    
    
}