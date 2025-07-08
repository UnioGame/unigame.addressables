using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UniGame.AddressableTools.Runtime
{
    public class AddressableRuntimeTools
    {
        public static List<IResourceLocation> GatherDependenciesFromLocations(IList<IResourceLocation> locations)
        {
            var locHash = new HashSet<IResourceLocation>();
            foreach (var loc in locations)
            {
                if (loc.ResourceType == typeof(IAssetBundleResource))
                    locHash.Add(loc);
                
                if (!loc.HasDependencies) continue;
                
                foreach (var dep in loc.Dependencies)
                    if (dep.ResourceType == typeof(IAssetBundleResource))
                        locHash.Add(dep);
            }
            
            return new List<IResourceLocation>(locHash);
        }
    }
    
    
}