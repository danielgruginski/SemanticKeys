using UnityEngine;
using System.Reflection;

namespace SemanticKeys
{
    /// <summary>
    /// A universal bridge that injects a SemanticKey value into a target Component's string field via Reflection.
    /// Usage: Add this to the GameObject, drag the target component in, type the field name, and select your Key.
    /// </summary>
    [ExecuteAlways] // Runs in Editor to provide immediate visual feedback
    [DefaultExecutionOrder(-1000)] // Run before mostly everything
    public class SemanticKeyInjector : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The component containing the string field you want to control.")]
        [SerializeField] private Component _targetComponent;

        [Tooltip("The exact name of the public string field (or property) in the target component.")]
        [SerializeField] private string _fieldName;

        [Header("Source")]
        [SerializeField] private SemanticKey _key;

        // Cache reflection info to avoid GC in Update
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
            // Reset cache if configuration changes
            _cachedField = null;
            _cachedProperty = null;
            _reflectionFailed = false;

            InitializeReflection();
            Inject();
        }

        private void Update()
        {
            // In Editor Mode: continuously enforce the value to prevent manual desync ("Soft Lock").
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

            // 1. Exact Match (Field)
            _cachedField = type.GetField(_fieldName, flags);

            // 2. Exact Match (Property)
            if (_cachedField == null)
            {
                _cachedProperty = type.GetProperty(_fieldName, flags);
            }

            // --- FALLBACK: Case Insensitive Search ---
            // Handles cases where Inspector says "Test" but variable is "test"
            if (_cachedField == null && _cachedProperty == null)
            {
                _cachedField = type.GetField(_fieldName, flags | BindingFlags.IgnoreCase);
            }

            if (_cachedField == null && _cachedProperty == null)
            {
                _cachedProperty = type.GetProperty(_fieldName, flags | BindingFlags.IgnoreCase);
            }

            // --- Validation Logic ---
            if (_cachedField == null && _cachedProperty == null)
            {
                _reflectionFailed = true;
                // Only log if we have fully configured the injector to avoid spam while typing
                if (_key.IsValid) 
                {
                    //Debug.LogWarning($"[SemanticKeyInjector] Could not find field/property '{_fieldName}' (or case-insensitive match) on type '{type.Name}'.", this);
                }
            }
        }

        public void Inject()
        {
            // Guard clauses with specific logging for debugging
            if (_targetComponent == null) return;
            if (string.IsNullOrEmpty(_fieldName)) return;

            if (!_key.IsValid)
            {
                // Only warn if we are trying to run and the user hasn't selected a key yet
                if (Application.isPlaying) Debug.LogWarning($"[SemanticKeyInjector] Semantic Key on {name} is invalid (None).", this);
                return;
            }

            if (_reflectionFailed) return;

            // Ensure reflection cache is ready
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
                        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(_targetComponent);
                    }
                }
                else if (_cachedProperty != null && _cachedProperty.CanWrite)
                {
                    // For properties, we check readability before comparing to avoid errors
                    if (_cachedProperty.CanRead)
                    {
                        var current = (string)_cachedProperty.GetValue(_targetComponent);
                        if (current == targetValue) return;
                    }

                    _cachedProperty.SetValue(_targetComponent, targetValue);
                    if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(_targetComponent);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SemanticKeyInjector] Injection failed: {e.Message}", this);
                _reflectionFailed = true;
            }
        }
    }
}