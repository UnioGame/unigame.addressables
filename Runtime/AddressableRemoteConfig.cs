namespace UniGame.AddressableTools.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>
    /// Define remote addressable sources
    /// to activate configuration to scriptable asset into resources folder
    /// don't forget to enable property in AddressableRemoteConfig
    /// </summary>
    [CreateAssetMenu(menuName = "UniGame/Addressables/AddressableRemoteConfig", fileName = "AddressableRemoteConfig")]
    public class AddressableRemoteConfig : ScriptableObject
    {
        /// <summary>
        /// Enable or disable addressable remote sources
        /// </summary>
        public bool enabled = true;
        /// <summary>
        /// if true, first activated remote source will be used all the time
        /// </summary>
        public bool isPermanentRemote = false;
        /// <summary>
        /// amount of single url tries to check remote source availability
        /// </summary>
        public int urlTriesCount = 3;
        /// <summary>
        /// remote utl check timeout in seconds
        /// </summary>
        public int timeoutSeconds = 5;
        /// <summary>
        /// list of remote addressables sources
        /// is no default value is set, then first remote will be used as default
        /// </summary>
#if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = "@name")]
#endif
        public List<AddressableRemoteValue> remotes = new();
    }


    [Serializable]
    public class AddressableRemoteValue
    {
        public string name;
        /// <summary>
        /// if false, then addressable tool will not use this remote source
        /// </summary>
        public bool enabled = true;
        /// <summary>
        /// test url for checking remote source availability
        /// </summary>
        public string testUrl;
        /// <summary>
        /// Remote url for addressable system source like CDN or remote server
        /// </summary>
        public string remoteUrl;
        /// <summary>
        /// Name of remote catalog, which will be used for addressable system
        /// </summary>
        public string remoteCatalogName;
    }
}