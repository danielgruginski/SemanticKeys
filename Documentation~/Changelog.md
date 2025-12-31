# **Changelog**

All notable changes to this project will be documented in this file.

## **\[0.6.5\] \- 2025-12-30**

### **Added**

* **SemanticKeyInjector**: New component to bridge Semantic Keys with third-party string fields using Reflection. Supports Editor-time "Soft Locking" of values.  
* **Reference Replacer Tool**: New Editor Window (`Tools > SemanticKeys > Key Replacer Tool`) to safely swap all references from one key to another across Prefabs, ScriptableObjects, and Scenes.  
* **Strict Inspector**: `KeyDomain` now uses a custom inspector preventing accidental edits. Renaming and Deleting are now explicit actions.  
* **Find References**: Added a "Magnifying Glass" button in the KeyDomain Inspector to find all usages of a key before deleting it.

### **Changed**

* **Optimization**: `KeyDomain` now uses a Dictionary cache for O(1) runtime lookups, replacing the previous O(N) list iteration.  
* **Safety**: `SemanticKey.None` logic updated to handle `null` vs `""` inconsistencies in Unity serialization.  
* **UX**: Renaming a Domain now properly handles file renaming and cleaning up old generated code.

### **Fixed**

* Fixed an issue where the `SemanticKeyReferenceUpdater` would crash when scanning scene files.  
* Fixed `NullReferenceException` when converting a default struct `SemanticKey` to string.

