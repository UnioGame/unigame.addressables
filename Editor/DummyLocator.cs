using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UniGame.AddressableTools.Editor
{
    public class DummyLocator : IResourceLocator
    {
        private List<IResourceLocation> _locations = new List<IResourceLocation>();
        
        public IEnumerable<IResourceLocation> AllLocations => _locations;

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            locations = new List<IResourceLocation>();
            return false;
        }

        public string LocatorId => nameof(DummyLocator);

        public IEnumerable<object> Keys => Enumerable.Empty<object>();
    }
}