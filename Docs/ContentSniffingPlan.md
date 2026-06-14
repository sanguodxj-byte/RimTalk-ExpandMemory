# Content Sniffing Implementation Plan

## Background
Currently, the `RimTalk-ExpandMemory` mod automatically injects a default `PromptEntry` containing memory and knowledge context into the active RimTalk preset. 
However, when users switch to highly customized or advanced presets that already incorporate memory/knowledge variables (e.g., `{{pawn.memory}}` or `{{knowledge}}`), the automatic injection becomes redundant, leading to wasted tokens and potential AI confusion.

## Objective
Implement a "Content Heuristic Sniffing" mechanism. Before injecting the default entry, the mod will scan existing entries in the active preset. If it detects that the preset already uses memory or knowledge variables, it will safely abort the auto-injection process.

## Target File
`Source/API/RimTalkAPIIntegration.cs`

## Implementation Details

### 1. Define Sniffing Keywords
We will define an array of critical substrings that indicate the preset is already handling memory/knowledge.
```csharp
private static readonly string[] SniffingKeywords = new string[]
{
    "pawn.memory", "p.memory",
    "pawn.ABM", "p.ABM",
    "pawn.ELS", "p.ELS",
    "pawn.CLPA", "p.CLPA",
    "{{knowledge}}", "{{ knowledge }}",
    "knowledge_grouped", "knowledge_rules", 
    "knowledge_lore", "knowledge_status", 
    "knowledge_history"
};
```

### 2. Modify `RegisterPromptEntry` Logic
In the `RegisterPromptEntry` method, we will add a step to retrieve all entries from the `ActivePreset` and perform the sniffing scan.

**Current Logic:**
1. Get ActivePreset.
2. Try to find the existing mod entry by deterministic ID.
3. If found, update its content and return.
4. If not found, create and insert the new entry.

**New Logic:**
1. Get ActivePreset.
2. Try to find the existing mod entry by deterministic ID.
3. If found, update its content and return.
4. **[NEW]** If not found, retrieve the list of all existing entries (`Entries` property/field) from the ActivePreset.
5. **[NEW]** Iterate through each entry's `Content` (ignoring null or empty strings).
6. **[NEW]** Check if the `Content` contains any of the `SniffingKeywords`.
7. **[NEW]** If a keyword is found, output a message (e.g., `"[MemoryPatch] Detected custom memory variables in active preset. Skipping auto-injection."`) and `return` to abort injection.
8. If no keywords are found, proceed with creating and inserting the new entry as usual.

### 3. Reflection Implementation for Compatibility
Since RimTalk types are resolved via reflection (`_promptAPIType`, `_promptPresetType`, etc.), the sniffing logic must also use reflection to access the `Entries` list and the `Content` of each entry.

```csharp
// Example conceptual code for sniffing via reflection:
var entriesField = preset.GetType().GetField("Entries");
if (entriesField != null)
{
    var entriesList = entriesField.GetValue(preset) as System.Collections.IEnumerable;
    if (entriesList != null)
    {
        foreach (var entry in entriesList)
        {
            var contentProp = entry.GetType().GetProperty("Content") ?? entry.GetType().GetField("Content");
            string content = contentProp?.GetValue(entry) as string;
            
            if (!string.IsNullOrEmpty(content))
            {
                foreach (var keyword in SniffingKeywords)
                {
                    if (content.Contains(keyword))
                    {
                        // Match found, skip injection
                        Log.Message($"[MemoryPatch] Detected custom memory variable '{keyword}' in preset. Skipping auto-injection.");
                        return;
                    }
                }
            }
        }
    }
}
```

## Benefits
- **Zero Configuration:** Players do not need to manually toggle settings when switching between basic and advanced presets.
- **High Compatibility:** Operates purely on string checking, agnostic to the specific structure or naming conventions of custom presets.
- **Non-Destructive:** If the user removes the custom entries containing the variables, the mod will detect their absence upon the next restart and automatically provide the fallback default entry.
