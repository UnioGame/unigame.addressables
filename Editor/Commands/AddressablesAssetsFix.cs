using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UniModules.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniGame.AddressableTools.Editor
{
public static class AddressablesAssetsFix
{
    private const string EmptyAddressableEntry = "- {fileID: 0}";
    private static AddressableAssetEntryError emptyError = new AddressableAssetEntryError()
    {
        Entry     = null,
        Error     = string.Empty,
        Group     = null,
        ErrorType = AddressableErrorType.None
    };
    
    private static AddressableAssetEntryError missingAddressableEntryError = new AddressableAssetEntryError()
    {
        Entry     = null,
        Error     = "Missing Addressable Entry",
        Group     = null,
        ErrorType = AddressableErrorType.MissingEntry
    };

    [MenuItem(itemName: "UniGame/Addressables/Validate Addressables Errors")]
    public static void Validate()
    {
        var errors  = new List<AddressableAssetEntryError>();
        
        errors = ValidateMissingReferences(errors);
        var isValid = errors.Count <= 0;
        PrintStatus(isValid, errors, LogType.Error);
        
        if (!isValid)
            return;
        
        errors = ValidateAddressablesGuid(errors);
        isValid = errors.Count <= 0;
        PrintStatus(isValid, errors, LogType.Error);
    }

    [MenuItem(itemName: "UniGame/Addressables/Fix Addressables Errors")]
    public static void FixAddressablesErrors()
    {
        RemoveMissingGroupReferences();
        
        var errors  = new List<AddressableAssetEntryError>();
        errors = ValidateMissingReferences(errors);
        FixMissingReferences(errors);
        
        errors = ValidateAddressablesGuid(errors);
        FixAddressablesGuids(errors);
        
        var isValid = errors.Count <= 0;
        PrintStatus(isValid, errors, LogType.Warning);
    }
    
    [MenuItem(itemName: "UniGame/Addressables/Remote Empty Groups")]
    public static void RemoveEmptyGroups()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var groups = settings.groups;
        var removed = new List<AddressableAssetGroup>();
        
        foreach (var assetGroup in groups)
        {
            var count = assetGroup.entries.Count;
            if(count > 0) continue;
            removed.Add(assetGroup);
        }

        foreach (var assetGroup in removed)
        {
            settings.RemoveGroup(assetGroup);
        }
    }
    
    [MenuItem(itemName: "UniGame/Addressables/Remove Missing References")]
    public static bool RemoveMissingGroupReferences()
    {
        var result = false;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var groups = settings.groups;
        
        var missingGroupsIndices = new List<int>();
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null)
                missingGroupsIndices.Add(i);
        }
        
        if (missingGroupsIndices.Count > 0)
        {
            Debug.Log("Addressable settings contains " + missingGroupsIndices.Count + " group reference(s) that are no longer there. Removing reference(s).");
            for (var i = missingGroupsIndices.Count - 1; i >= 0; i--)
            {
                groups.RemoveAt(missingGroupsIndices[i]);
            }

            result = true;
            settings.MarkDirty();
        }

        result = false;
        return result;
    }


    public static void FixAddressablesGuids(List<AddressableAssetEntryError> errors)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings) return;

        errors = errors.Where(x => x.ErrorType == AddressableErrorType.GuidError).ToList();
        foreach (var assetEntryError in errors)
        {
            var entry = assetEntryError.Entry;
            settings.RemoveAssetEntry(entry.guid);
        }

        settings.MarkDirty();
        AssetDatabase.Refresh();

        foreach (var assetEntryError in errors)
        {
            var entry      = assetEntryError.Entry;
            var asset      = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);
            var assetGroup = assetEntryError.Group;

            asset.SetAddressableAssetGroup(assetGroup);
            var assetEntry = asset.GetAddressableAssetEntry();
            Debug.Log($"create addressable entry {assetEntry?.parentGroup.name} : {assetEntry?.guid} {assetEntry?.AssetPath} ");
        }

        settings.MarkDirty();
        AssetDatabase.Refresh();
    }

    public static void FixMissingReferences(List<AddressableAssetEntryError> errors)
    {
        if (errors.All(x => x.ErrorType != AddressableErrorType.MissingEntry))
            return;
        
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings) return;
        var settingsPath    = settings.AssetPath;
        var settingsContent = File.ReadAllLines(settingsPath).Where(x => !x.Contains(EmptyAddressableEntry));
            
        File.WriteAllLines(settingsPath,settingsContent);

        settings.MarkDirty();
        AssetDatabase.Refresh();
    }

    public static void PrintStatus(bool isValid, List<AddressableAssetEntryError> errors, LogType logType)
    {
        if (isValid)
        {
            Debug.Log($"Addressables GUID Validated");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Addressables GUID Validator find errors [{errors.Count}]:");
        foreach (var error in errors)
        {
            builder.AppendLine(error.Error);
        }

        var errorMessage = builder.ToString();

        switch (logType)
        {
            case LogType.Error:
                Debug.LogError(errorMessage);
                break;
            case LogType.Assert:
                Debug.LogAssertion(errorMessage);
                break;
            case LogType.Warning:
                Debug.LogWarning(errorMessage);
                break;
            case LogType.Log:
                Debug.Log(errorMessage);
                break;
            default:
                Debug.Log(errorMessage);
                break;
        }
    }

    public static List<AddressableAssetEntryError> ValidateMissingReferences(List<AddressableAssetEntryError> errors)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings) return errors;

        var settingsPath    = settings.AssetPath;
        var settingsContent = File.ReadAllText(settingsPath);

        if (!settingsContent.Contains(EmptyAddressableEntry))
            return errors;

        var error = missingAddressableEntryError;
        
        errors.Add(error);
        return errors;
    }

    public static List<AddressableAssetEntryError> ValidateAddressablesGuid(List<AddressableAssetEntryError> errors)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings) return errors;

        var groups = settings.groups;
        foreach (var addressableAssetGroup in groups)
        {
            if(addressableAssetGroup == null)
                continue;
            
            var entries = addressableAssetGroup.entries;
            foreach (var entry in entries)
            {
                var entryStatus = Validate(entry);
                if (!string.IsNullOrEmpty(entryStatus.Error))
                    errors.Add(entryStatus);
            }
        }

        return errors;
    }

    public static AddressableAssetEntryError Validate(AddressableAssetEntry entry)
    {
        if (entry == null)
        {
            return missingAddressableEntryError;
        }
        
        var assetByPath      = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);
        var assetGuidPath    = AssetDatabase.GUIDToAssetPath(entry.guid);
        var assetByGuid      = AssetDatabase.LoadAssetAtPath<Object>(assetGuidPath);
        var entryParentGroup = entry.parentGroup;

        var isValid = assetByGuid == assetByPath;
        if (isValid) return emptyError;

        var errorMessage =
            $"ERROR ADDRESSABLE REF AT GROUP : {entryParentGroup.Name} : {entry.address} \n\t {assetByGuid?.name}: {entry.guid}  by GUID != \n\t {assetByPath?.name} : {entry.AssetPath} by PATH ";

        return new AddressableAssetEntryError()
        {
            ErrorType = AddressableErrorType.GuidError,
            Entry = entry,
            Group = entryParentGroup,
            Error = errorMessage
        };
    }
}

public struct AddressableAssetEntryError
{
    public AddressableErrorType  ErrorType;
    public AddressableAssetEntry Entry;
    public AddressableAssetGroup Group;
    public string                Error;
}

public enum AddressableErrorType
{
    None = 0,
    GuidError,
    MissingEntry
}
}