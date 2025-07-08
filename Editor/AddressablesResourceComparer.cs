using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UniGame.AddressableTools.Editor
{
    public class AddressablesResourceComparer :  IEqualityComparer<IResourceLocation>
    {
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            return x.PrimaryKey.Equals(y.PrimaryKey) && x.ResourceType.Equals(y.ResourceType) && x.InternalId.Equals(y.InternalId);
        }

        public int GetHashCode(IResourceLocation loc)
        {
            return loc.PrimaryKey.GetHashCode() * 31 + loc.ResourceType.GetHashCode();
        }
    }
}