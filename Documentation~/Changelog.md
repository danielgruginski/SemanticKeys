# **Changelog**

All notable changes to this project will be documented in this file.

## **\[Unreleased\]**

### **Changed**

* **Generated Code Sync**: Once a domain's static class exists, adding, renaming or deleting keys in the Inspector regenerates it (new `KeyDomain.UpdateGeneratedCode()`). It no longer keeps old names or deleted keys, so code that uses them stops compiling.  
* **Code Generation**: A class file that is already up to date is left untouched, so regenerating doesn't trigger a recompile.

### **Fixed**

* **\+ Add Key** in a key field's dropdown assigned the placeholder GUID `temp` when the typed name matched an existing key in different case (e.g., `health` when `Health` exists). It now selects the existing key.  
* The **Key Replacer** could have its text edits overwritten: it saved assets after editing their files on disk, before reimporting them. It now saves pending changes first and reimports the edited files afterwards.  
* A domain's GUID was regenerated every session. It is now saved with the asset; existing domains get one the next time they are loaded in the Editor.  
* Generated code failed to compile for some names. Quotes, backslashes and control characters in key names are now escaped; C\# keywords get an `@`; class names starting with a digit get a leading `_`; a key named like its class, or with no letters or digits, gets a valid field name. A domain name with no letters or digits logs an error instead of writing an invalid file.  
* The key drawer could fail to apply a dropdown selection (e.g., in lists, or after **\+ Add Key**) because it kept the Inspector's properties until the selection arrived. It now finds the property again when applying it, and shows a dash when the selected objects hold different keys.  
* Documentation: the settings asset is not created automatically; create it with **Create \> SemanticKeys \> Settings**. The Key Replacer docs described a domain mismatch warning that doesn't exist.

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

