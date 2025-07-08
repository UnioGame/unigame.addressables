using System.Collections.Generic;
using UniModules.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UniGame.AddressableTools.Editor
{
    public static class AddressableExtensions
    {
        public static AssetReferenceGameObject PrefabToAssetReference(this Component source)
        {
            return source.gameObject.PrefabToAssetReference();
        }

        
        public static AssetReferenceGameObject PrefabToAssetReference(this GameObject source)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(source))
                return null;
            
            var pathToPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            var guid = AssetDatabase.AssetPathToGUID(pathToPrefab);
            return new AssetReferenceGameObject(guid);
        }

        public static void RemoveAddressableAssetLabel(this Object source, string label)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return;

            var entry = source.GetOrCreateAddressableAssetEntry();
            if (entry != null && entry.labels.Contains(label)) {
                entry.labels.Remove(label);
                
                AddressableAssetSettingsDefaultObject.Settings.SetDirty(AddressableAssetSettings.ModificationEvent.LabelRemoved, entry, true);
            }
        }

        public static IReadOnlyList<string> GetAllAddressableLabels(this Object source)
        {
            return AddressableAssetSettingsDefaultObject.Settings.GetLabels();
        }
        
        public static HashSet<string> GetAddressableLabels(this Object source)
        {
            var entry = source.GetAddressableAssetEntry();
            return entry?.labels;
        }

        public static void AddAddressableAssetLabel(this Object source, string label)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return;

            var entry = source.GetOrCreateAddressableAssetEntry();
            if (entry != null && entry.labels.Add(label)) {
                AddressableAssetSettingsDefaultObject.Settings.SetDirty(AddressableAssetSettings.ModificationEvent.LabelAdded, entry, true);
            }
        }

        public static void SetAddressableAssetAddress(this Object source, string address)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return;

            var entry = source.GetOrCreateAddressableAssetEntry();
            if (entry != null) {
                entry.address = address;
            }
        }

        public static void SetAddressableAssetGroup(this Object source, string groupName)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return;
            
            var group = !AddressableEditorTools.GroupExists(groupName) ?
                AddressableEditorTools.CreateGroup(groupName) : 
                AddressableEditorTools.GetGroup(groupName);
            
            source.SetAddressableAssetGroup(group);
        }
        
        public static void SetAddressableAssetGroup(this Object source, AddressableAssetGroup group)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return;

            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            addressableSettings.CreateOrMoveEntry(source.GetGUID(), group);
        }
        
        public static void AddToDefaultAddressableGroupIfNone(this Object source)
        {
            if (source.IsInAnyAddressableAssetGroup()) return;
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            SetAddressableAssetGroup(source, addressableSettings.DefaultGroup);
        }
        
        public static void AddToDefaultAddressableGroup(this Object source)
        {
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            SetAddressableAssetGroup(source, addressableSettings.DefaultGroup);
        }
        
        public static HashSet<string> GetAddressableAssetLabels(this Object source)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return null;

            var entry = source.GetAddressableAssetEntry();
            return entry?.labels;
        }

        public static string GetAddressableAssetPath(this Object source)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return string.Empty;

            var entry = source.GetAddressableAssetEntry();
            return entry != null ? entry.address : string.Empty;
        }

        public static bool IsInAddressableAssetGroup(this Object source, string groupName)
        {
            if (source == null || !AssetDatabase.Contains(source)) return false;
            var group = source.GetCurrentAddressableAssetGroup();
            return group != null && (string.IsNullOrEmpty(groupName) || group.Name == groupName);
        }

        public static bool IsAddressable(this Object source) => IsInAddressableAssetGroup(source, string.Empty);
        
        public static bool IsInAnyAddressableAssetGroup(this Object source) => IsInAddressableAssetGroup(source, string.Empty);

        public static AddressableAssetGroup GetCurrentAddressableAssetGroup(this Object source)
        {
            if(source == null || !AssetDatabase.Contains(source))
                return null;
            
            var entry = source.GetAddressableAssetEntry();
            return entry?.parentGroup;
        }

        public static AddressableAssetEntry GetOrCreateAddressableAssetEntry(this Object source)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return null;

            var entry = source.GetAddressableAssetEntry();
            if (entry == null)
            {
                var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
                var guid = source.GetGUID();
                addressableSettings.CreateOrMoveEntry(guid, addressableSettings.DefaultGroup);
            }

            return entry;
        }
        
        public static AddressableAssetEntry FindAddressableAssetEntryByAddress(this string addressablePath)
        {
            if (string.IsNullOrEmpty(addressablePath))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = addressableSettings.FindAssetEntryByAddress(addressablePath);
            return entry;
        }
        
        public static AddressableAssetEntry GetAddressableAssetEntryByAssetPath(this string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            var sourceGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(sourceGuid))
                return null;
            
            return addressableSettings.FindAssetEntry(sourceGuid);
        }
        
        public static AddressableAssetEntry GetAddressableAssetEntry(this Object source)
        {
            if (source == null || !AssetDatabase.Contains(source))
                return null;
            var sourcePath = AssetDatabase.GetAssetPath(source);
            
            if (string.IsNullOrEmpty(sourcePath))
                return null;
            
            return GetAddressableAssetEntryByAssetPath(sourcePath);
        }
    }
}