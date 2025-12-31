# **Semantic Key Injector**

## **Overview**

The SemanticKeyInjector is a utility component designed to bridge the gap between **Semantic Keys** and third-party assets or legacy code that relies on standard string fields.

It uses C\# Reflection to inject the value of a SemanticKey into a target component's string field at runtime and in the Editor, ensuring your KeyDomain remains the Source of Truth without requiring you to modify external source code.

## **Features**

### **1\. Universal Bridging**

Instead of writing specific bridge scripts for every third-party asset (e.g., TopDownEngineBridge, InventoryBridge), this single component handles generic string injection for any component.

### **2\. Editor "Soft Lock"**

The component is marked with \[ExecuteAlways\].

* It continuously monitors the target field in the Editor.  
* If you try to change the target string manually in the Inspector, the Injector immediately overwrites it back to the selected Semantic Key value.  
* This provides visual feedback that the field is managed externally and prevents data desynchronization.

### **3\. Smart Reflection (Fuzzy Matching)**

The injection logic handles common naming discrepancies (like Inspector display names vs. actual variable names). It searches in this order:

1. **Public/Private Field** (Exact match)  
2. **Public/Private Property** (Exact match)  
3. **Field** (Case-insensitive match)  
4. **Property** (Case-insensitive match)

## **Usage Guide**

1. Select the GameObject containing the third-party script (e.g., a LootBox with a public string LootTableID field).  
2. Add the SemanticKeyInjector component.  
3. Drag the LootBox component into the **Target Component** slot.  
4. Type LootTableID (or lootTableId) into the **Field Name** slot.  
   * If the field is valid, the console remains silent.  
   * If the field cannot be found, a warning will appear in the Console.  
5. Select your desired key (e.g., Loot.Epic) in the **Source Key** dropdown.

The LootTableID string on the LootBox will now automatically lock to the value of Loot.Epic.

## **Technical Details**

### **Execution Order**

This script is set to \[DefaultExecutionOrder(-1000)\].

* **Goal:** Ensure the string is injected **before** the target component's Awake or Start methods run.  
* **Result:** Third-party assets that cache string hashes in Awake (like Animator parameters) will receive the correct value immediately.

### **Performance**

* **Editor:** Runs in the Update loop to provide the "Soft Lock" UX. Reflection objects (FieldInfo/PropertyInfo) are cached to prevent Garbage Collection.  
* **Runtime:** Injection occurs in Editor Mode. The component is disabled for game builds but the injection should be baked into the prefab.

### **Limitations**

* **Variable Shadowing:** If a class inherits from another and both declare a variable with the same name, Reflection will typically target the most derived (child) version.  
* **Read-Only Properties:** The injector cannot write to properties that do not have a set accessor.

