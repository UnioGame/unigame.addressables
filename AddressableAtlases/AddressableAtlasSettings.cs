namespace UniGame.AddressableAtlases
{
    using System;
    using System.Collections.Generic;

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif
    

    [Serializable]
    public class AddressableAtlasSettings
    {
#if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = "@tag")]
#endif
        public List<AddressableAtlasData> atlases = new();
    }
}