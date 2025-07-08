namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using Core.Runtime;
    using Cysharp.Threading.Tasks;

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    
    [Serializable]
    public class AddressableResourcePreloader
    {
#if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = "@Label")]
#endif
        public List<AddressablePreloadValue> warmupReferences = new();
        
        public void WarmUp(ILifeTime lifeTime)
        {
            foreach (var value in warmupReferences)
            {
                if(value == null || value.reference.RuntimeKeyIsValid() == false)
                    continue;
                
                var reference = value.reference;
                reference.WarmUpReference(lifeTime, value.count, value.prewarm, value.killDelay)
                    .AttachExternalCancellation(lifeTime.Token)
                    .Forget();
            }
        }
    }
}