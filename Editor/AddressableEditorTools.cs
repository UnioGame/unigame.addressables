using System.Collections.Generic;
using System.Linq;
using UniModules.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace UniGame.AddressableTools.Editor
{
    using System.Text;

    public static class AddressableEditorTools
    {
        public const string RemoteLoadKey = "Remote.LoadPath";        
        public const string RemoteBuildKey = "Remote.BuildPath";        
        public const string LocalBuildKey = "Local.BuildPath";        
        public const string LocalLoadKey = "Local.LoadPath";        
        
        private static AddressableAssetSettings addressableAssetSettings;

        [MenuItem("UniGame/Addressables/Print Variables")]
        public static void PrintAllProfileVariables()
        {
            var variables = GetProfileVariables();
            var builder = new StringBuilder();
            
            builder.AppendLine("Addressable Profile Variables:");
            
            foreach(var variable in variables)
            {
                builder.AppendLine($"{variable.Key} : {variable.Value}");
            }
            
            Debug.Log(builder);
        }
        
        public static Dictionary<string,string> GetProfileVariables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var profileSettings = settings.profileSettings;
            var profileId = settings.activeProfileId;
            var keys = profileSettings.GetVariableNames();
            var result = new Dictionary<string, string>();
            foreach(var key in keys)
            {
                var value = profileSettings.GetValueByName(profileId, key);
                result.Add(key, value);
            }
            return result;
        }
        
        public static AssetReference FindReferenceByAddress(this AddressableAssetSettings settings,string address)
        {
            var entry = FindAssetEntryByAddress(settings, address);
            
            if (entry == null) {
                Debug.LogWarning($"Not found asset with address :: {address}");
                return null;
            }
            
            return new AssetReference(entry.guid);
        }
        
        public static AddressableAssetEntry FindAssetEntryByAddress(this AddressableAssetSettings settings,string address)
        {
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries,true,null,
                x => x.address == address || x.guid == address);
            var asset = entries.FirstOrDefault();
            return asset;
        }
        
        public static object EvaluateKey(object obj)
        {
            return obj is IKeyEvaluator ? (obj as IKeyEvaluator).RuntimeKey : obj;
        }
        
        public static bool IsAddressableAsset(this string guid)
        {
            return GetAddressableAssetEntryByGuid(guid) != null;
        }
        
        public static bool IsAddressableAsset(this Object asset)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return !string.IsNullOrEmpty(guid) && IsAddressableAsset(guid);
        }
        
        public static AddressableAssetEntry GetAddressableAssetEntryByGuid(this string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            return addressableSettings.FindAssetEntry(guid);
        }
        
        public static AssetReference FindReferenceByAddress(string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return FindReferenceByAddress(settings, address);
        }

        public static void SetAddressableProperty(this string key,string value)
        {
            addressableAssetSettings = addressableAssetSettings 
                ? addressableAssetSettings 
                :  AddressableAssetSettingsDefaultObject.Settings;

            var activeProfile = addressableAssetSettings.activeProfileId;
            var profileSettings = addressableAssetSettings.profileSettings;
            
            profileSettings.CreateValue(key, value);
            profileSettings.SetValue(activeProfile,key, value);
        }

        public static string EvaluateRuntimeProfileValue(string key)
        {
            addressableAssetSettings = addressableAssetSettings 
                ? addressableAssetSettings 
                :  AddressableAssetSettingsDefaultObject.Settings;
            
            if (!addressableAssetSettings) return key;
            
            var profileId = addressableAssetSettings.activeProfileId;
            var profile = addressableAssetSettings.profileSettings;
            
            var runtimeValue = profile.GetValueByName(profileId, key);
            return runtimeValue;
        }

        public static string GetRemoteLoadPath()
        {
            return RemoteLoadKey.EvaluateAddressableProfileVariable();
        }
        
        public static string GetRemoteBuildPath()
        {
            return RemoteBuildKey.EvaluateAddressableProfileVariable();
        }
        
        public static string GetLocalBuildPath()
        {
            return LocalBuildKey.EvaluateAddressableProfileVariable();
        }
        
        public static string GetLocalLoadPath()
        {
            return LocalLoadKey.EvaluateAddressableProfileVariable();
        }
        
        public static string EvaluateAddressableProfileVariable(this string key)
        {
            var evaluateKey = key;
            addressableAssetSettings = addressableAssetSettings 
                ? addressableAssetSettings 
                :  AddressableAssetSettingsDefaultObject.Settings;
            
            if (!addressableAssetSettings) return evaluateKey;
            
            var profileId = addressableAssetSettings.activeProfileId;
            var profile = addressableAssetSettings.profileSettings;
            
            var runtimeValue = profile.GetValueByName(profileId, evaluateKey);
            if(!string.IsNullOrEmpty(runtimeValue))
                evaluateKey = runtimeValue;
            
            var result = addressableAssetSettings.profileSettings
                .EvaluateString(profileId, evaluateKey);
            
            return result;
        }
        
        public static AddressableAssetEntry CreateAssetEntry<T>(T source, string groupName, string label) where T : UnityEngine.Object
        {
            var entry = CreateAssetEntry(source, groupName);
            if (source != null) {
                source.AddAddressableAssetLabel(label);
            }

            return entry;
        }

        public static AddressableAssetEntry MakeAddressable<T>(this T source) where T : Object
        {
            if (source.IsAddressableAsset())
                return source.GetAddressableAssetEntry();
            var defaultGroup = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
            var entry = CreateAssetEntry(source, defaultGroup.Name);
            source.MarkDirty();
            return entry;
        }

        public static AddressableAssetEntry CreateAssetEntry<T>(this T source, string groupName) where T : Object
        {
            if (source == null || string.IsNullOrEmpty(groupName) || !AssetDatabase.Contains(source))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var group = !GroupExists(groupName) ? CreateGroup(groupName) : GetGroup(groupName);

            var entry = addressableSettings.CreateOrMoveEntry(sourceGuid, group);
            entry.address = sourcePath;
            
            addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

            return entry;
        }

        public static AddressableAssetEntry MarkDirty(this AddressableAssetEntry entry)
        {
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
            return entry;
        }

        public static AddressableAssetEntry CreateAssetEntry<T>(T source) where T : Object
        {
            if (source == null || !AssetDatabase.Contains(source))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var entry = addressableSettings.CreateOrMoveEntry(sourceGuid, addressableSettings.DefaultGroup);
            entry.address = sourcePath;
            
            addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

            return entry;
        }

        public static AddressableAssetGroup GetGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            return addressableSettings.FindGroup(groupName);
        }

        public static AddressableAssetGroup CreateGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            var group = addressableSettings.CreateGroup(groupName, false, false, false, addressableSettings.DefaultGroup.Schemas);
            
            addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, true);

            return group;
        }

        public static bool GroupExists(string groupName)
        {
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            return addressableSettings.FindGroup(groupName) != null;
        }
    }
    
}