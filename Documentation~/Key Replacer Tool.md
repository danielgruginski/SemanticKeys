# **Key Replacer Tool**

## **Overview**

The **Key Replacer Tool** is a powerful refactoring utility that allows you to swap all references of one Semantic Key with another across your entire project.

This is particularly useful when:

* **Merging Keys:** You want to replace all instances of "FireDamage" with "BurnDamage" before deleting "FireDamage".  
* **Refactoring Domains:** You are moving keys from a generic "Tags" domain to a specific "CombatTags" domain.  
* **Fixing Mistakes:** You accidentally used a placeholder key in many prefabs and need to swap it to the final key.

## **Usage**

1. Open the tool via **Tools** \> **SemanticKeys** \> **Key Replacer Tool**.  
2. **From (Source):** Select the key you want to replace.  
3. **To (Target):** Select the key you want to use instead.  
4. Click **Replace All**.

## **Safety Features**

### **Domain Mismatch Warning**

The tool detects if the Source Key and Target Key belong to different domains (e.g., replacing a key from `Audio` with a key from `Stats`).

If a mismatch is detected, a warning dialog appears.

* **Why this matters:** If your C\# code uses `[SemanticKeyFilter("Audio")]` on a field, and you replace the value with a key from the `Stats` domain, that field will technically hold the new value, but the Inspector might show it as "None" or invalid because it doesn't match the filter.  
* **Recommendation:** Only proceed with cross-domain replacements if you are sure the target fields are not strictly filtered, or if you plan to update the code filters as well.

### **Scope of Operation**

The tool performs a **Two-Pass Scan**:

1. **Pass 1: Assets on Disk**  
   * Scans all **Prefabs** and **ScriptableObjects** in the project.  
   * Files are modified and marked dirty. You must save the project to commit these changes.  
2. **Pass 2: Scene Objects**  
   * Scans all MonoBehaviours and ScriptableObjects in the **currently open scene(s)**.  
   * **Limitation:** It **does not** scan scenes that are closed. If you have keys serialized in many different scene files, you must open them and run the tool (or write a custom batch script).

## **Undo / Redo**

* **Scene Objects:** Replacements in the scene are registered with the Undo system. You can standard Undo (`Ctrl+Z`).  
* **Project Assets:** Replacements in Prefabs/SOs are modified directly via SerializedObjects. While Unity often tracks these writes, for large batch operations, it is recommended to **back up your project** or ensure you are using Version Control (Git) before running a massive replace operation.

