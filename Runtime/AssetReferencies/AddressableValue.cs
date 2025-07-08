namespace UniGame.AddressableTools.Runtime
{
    using System;
    using UnityEngine.AddressableAssets;
    using Object = UnityEngine.Object;

#if ODIN_INSPECTOR
     using Sirenix.OdinInspector;
#endif
    
    [Serializable]
    public class AddressableValue<TValue>
        where TValue : Object
    {
#if ODIN_INSPECTOR
        [HideLabel]
        [OnValueChanged(nameof(UpdateView))]
#endif
        
        public AssetReferenceT<TValue> reference = new(string.Empty);
        
        [NonSerialized]
#if ODIN_INSPECTOR
        [HideLabel]
        [InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        [FoldoutGroup("reference")]
        [ShowIf(nameof(HasValue))]
        [OnInspectorGUI]
#endif
        private Object _value;

        public TValue EditorValue
        {
            get
            {
#if UNITY_EDITOR
                return reference.editorAsset;
#endif
                return null;
            }
        }
        
        public bool HasValue => reference != null && reference.RuntimeKeyIsValid();
        
#if ODIN_INSPECTOR
        [Button]
        [OnInspectorInit]
#endif
        public void UpdateView()
        {
#if UNITY_EDITOR
            _value = reference.editorAsset;
#endif
        }
        
        public static explicit operator AssetReferenceT<TValue>(AddressableValue<TValue> v)
        {
            return v.reference;
        }

    }
    
    
    [Serializable]
    public class AddressableValue
    {
#if ODIN_INSPECTOR
        [HideLabel]
        [OnValueChanged(nameof(UpdateView))]
#endif
        public AssetReference reference;
        
        [NonSerialized]
#if ODIN_INSPECTOR
        [HideLabel]
        [InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        [FoldoutGroup("reference")]
        [ShowIf(nameof(HasValue))]
        [OnInspectorGUI]
#endif
        private Object _value;

        public Object EditorValue
        {
            get
            {
#if UNITY_EDITOR
                return reference.editorAsset;
#endif
                return null;
            }
        }
        
        public bool HasValue => reference != null && reference.RuntimeKeyIsValid();
        
#if ODIN_INSPECTOR
        [Button]
        [OnInspectorInit]
#endif
        public void UpdateView()
        {
#if UNITY_EDITOR
            _value = reference.editorAsset;
#endif
        }
        
        public static explicit operator AssetReference(AddressableValue v)
        {
            return v.reference;
        }
    }
}