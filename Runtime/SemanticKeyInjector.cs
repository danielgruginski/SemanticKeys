using UnityEngine;
using System.Reflection;

namespace SemanticKeys
{
    /// <summary>
    /// A universal bridge that injects a SemanticKey value into a target Component's string field.
    /// 
    /// OPTIMIZATION:
    /// All logic is wrapped in UNITY_EDITOR.
    /// At Runtime (Build), this component becomes empty and does nothing.
    /// It relies on the fact that the value was "baked" into the target component's serialization
    /// during Editor time.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-1000)]
    public class SemanticKeyInjector : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Target")]
        [Tooltip("The component containing the string field you want to control.")]
        [SerializeField] private Component _targetComponent;

        [Tooltip("The exact name of the public string field (or property) in the target component.")]
        [SerializeField] private string _fieldName;

        [Header("Source")]
        [SerializeField] private SemanticKey _key;

        // Cache reflection info to avoid GC
        private FieldInfo _cachedField;
        private PropertyInfo _cachedProperty;
        private bool _reflectionFailed;

        private void OnEnable()
        {
            InitializeReflection();
            Inject();
        }

        private void OnValidate()
        {
            _cachedField = null;
            _cachedProperty = null;
            _reflectionFailed = false;
            InitializeReflection();
            Inject();
        }

        private void Update()
        {
            // Continuous enforcement in Editor ("Soft Lock")
            if (!Application.isPlaying)
            {
                Inject();
            }
        }

        private void InitializeReflection()
        {
            if (_targetComponent == null || string.IsNullOrEmpty(_fieldName)) return;

            var type = _targetComponent.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // 1. Exact Match
            _cachedField = type.GetField(_fieldName, flags);
            if (_cachedField == null) _cachedProperty = type.GetProperty(_fieldName, flags);

            // 2. Fuzzy Match (Case Insensitive)
            if (_cachedField == null && _cachedProperty == null)
            {
                _cachedField = type.GetField(_fieldName, flags | BindingFlags.IgnoreCase);
            }
            if (_cachedField == null && _cachedProperty == null)
            {
                _cachedProperty = type.GetProperty(_fieldName, flags | BindingFlags.IgnoreCase);
            }

            if (_cachedField == null && _cachedProperty == null)
            {
                _reflectionFailed = true;
                if (_key.IsValid)
                    Debug.LogWarning($"[SemanticKeyInjector] Could not find field/property '{_fieldName}' on '{type.Name}'.", this);
            }
        }

        private void Inject()
        {
            if (_targetComponent == null || string.IsNullOrEmpty(_fieldName) || !_key.IsValid || _reflectionFailed) return;

            if (_cachedField == null && _cachedProperty == null) InitializeReflection();

            string targetValue = _key.Value;

            try
            {
                if (_cachedField != null)
                {
                    var current = (string)_cachedField.GetValue(_targetComponent);
                    if (current != targetValue)
                    {
                        _cachedField.SetValue(_targetComponent, targetValue);
                        // CRITICAL: Mark dirty so Unity saves the change to the Scene/Prefab
                        UnityEditor.EditorUtility.SetDirty(_targetComponent);
                    }
                }
                else if (_cachedProperty != null && _cachedProperty.CanWrite)
                {
                    if (_cachedProperty.CanRead)
                    {
                        var current = (string)_cachedProperty.GetValue(_targetComponent);
                        if (current == targetValue) return;
                    }

                    _cachedProperty.SetValue(_targetComponent, targetValue);
                    UnityEditor.EditorUtility.SetDirty(_targetComponent);
                }
            }
            catch (System.Exception)
            {
                _reflectionFailed = true;
            }
        }
#endif
    }
}