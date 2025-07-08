using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniGame.AddressableTools.Editor
{
    using UniModules;

    public static class AddressablesCleaner
    {
        public const string AddressablesCachePath     = "./Library/com.unity.addressables";
        public const string StreamingAddressablesPath = "/aa";
            
        [MenuItem("UniGame/Addressables/Clean Library Cache")]
        public static void RemoveLibraryCache()
        {
            try {
                FileUtils.DeleteDirectoryFiles(AddressablesCachePath);
                FileUtils.DeleteSubDirectories(AddressablesCachePath);
            }
            catch (Exception e) {
                Debug.LogError(e);
            }

            Debug.Log("Addressables Library Cache Removed");
        }

        [MenuItem("UniGame/Addressables/Clean Default Context Builder")]
        public static void CleanDefaultContextBuilder()
        {
            AddressableAssetSettings.CleanPlayerContent(null);
        }
 
        [MenuItem("UniGame/Addressables/Clean All")]
        public static void CleanAll()
        {
            RemoveLibraryCache();
            RemoveStreamingCache();
            CleanPlayerContent(null);
            Debug.Log("Addressables Cache Removed");
        }
        
        public static void CleanPlayerContent(IDataBuilder builder)
        {
            AddressableAssetSettings.CleanPlayerContent(builder);
        }

        
        public static void RemoveStreamingCache()
        {
            try {
                var targetPath = Application.streamingAssetsPath + StreamingAddressablesPath;
                FileUtils.DeleteDirectory(targetPath);
            }
            catch (Exception e) {
                Debug.LogError(e);
            }

        }


    }
}
