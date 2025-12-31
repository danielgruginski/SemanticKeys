# **Semantic Key**

## **Overview**

The `SemanticKey` is the core data type of the package. It is a **Struct (Value Type)** designed to replace `string` literals in your codebase with a GUID-backed identity.

It acts as a hybrid:

* **At Runtime:** It behaves like a `string`, providing zero-overhead access to its cached value.  
* **In Logic:** It behaves like a unique identifier, ensuring that renaming a key in the database does not break references in your code or prefabs.

## **Properties**

| Property | Type | Description |
| ----- | ----- | ----- |
| **Value** | `string` | The cached string value (e.g., "Attack"). **Access is O(1).** If the key is uninitialized, returns `string.Empty` (never null). |
| **Guid** | `string` | The immutable identity of the key. This persists even if the `Value` is renamed in the Domain. |
| **IsValid** | `bool` | Returns `true` if the key has a valid GUID and Value. Returns `false` for default/empty keys. |

## **Usage**

### **1\. Implicit String Conversion**

The struct includes an implicit operator to `string`. You can pass a `SemanticKey` directly into any Unity API that expects a string.

    [SerializeField] private SemanticKey _animParameter;  
    [SerializeField] private Animator _animator;
    
    private void Update()  
    {  
        // Implicitly converts _animParameter to its string Value  
        _animator.SetTrigger(_animParameter);   
    }

### **2\. Null Safety (The "None" Pattern)**

`SemanticKey` enforces the **Null Object Pattern**.

* A default struct `new SemanticKey()` is not `null`.  
* Its `Value` property returns `""` (empty string).  
* To check for "null", compare against `SemanticKey.None` or check `.IsValid`.

        public void PlaySound(SemanticKey soundId)  
        {  
            // Safe check - no NullReferenceException possible  
            if (!soundId.IsValid) return;   
              
            // OR  
            if (soundId == SemanticKey.None) return;
        
            AudioManager.Play(soundId);  
        }

### **3\. Equality & Identity**

Equality comparisons (`==`, `!=`, `Equals()`) use the **GUID**, not the string value.

**Scenario:**

1. You have a key named "Fireball" with GUID `A1`.  
2. You rename it in the Domain to "FireBlast".  
3. Your player prefab still has the stale cache: `Value="Fireball"`, `Guid="A1"`.  
4. Your code references the static class: `Spells.FireBlast` (which has `Value="FireBlast"`, `Guid="A1"`).

        // Returns TRUE because GUIDs match (A1 == A1)  
        if (playerPrefab.spellKey == Spells.FireBlast)   
        {  
            // Logic works correctly even though the strings ("Fireball" vs "FireBlast") differ.  
        }

### **4\. Dictionary Keys**

`SemanticKey` implements `IEquatable<T>` and overrides `GetHashCode()`. It is highly efficient as a key in `Dictionary` or `HashSet`.

    private Dictionary<SemanticKey, float> _cooldowns = new();
    
    public void StartCooldown(SemanticKey skill)  
    {  
        _cooldowns[skill] = Time.time + 5.0f;  
    }

## **Performance Characteristics**

* **Memory:** As a struct, `SemanticKey` is allocated on the Stack (or inline in arrays). It generates **zero garbage** (GC) when passed between methods.  
* **Speed:** Accessing `.Value` returns a reference to the interned string. There is no dictionary lookup or search process at runtime.  
* **Comparison:** Equality checks compare the GUID strings.

