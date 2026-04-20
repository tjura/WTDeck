using WTDeck.Core.Models;

namespace WTDeck.Core.KeyBindings;

public static class BlkParser
{
    public static Dictionary<string, KeyBinding> Parse(TextReader reader)
    {
        var result = new Dictionary<string, KeyBinding>();
        var blockStack = new Stack<string>();
        var currentActionId = (string?)null;
        var currentChordKeys = new List<int>();
        // Track all chords per action ID across multiple blocks
        var actionChords = new Dictionary<string, List<KeyChord>>();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Closing brace
            if (trimmed == "}")
            {
                if (currentActionId is not null)
                {
                    // Exiting an action block - save any collected keys as a chord
                    if (currentChordKeys.Count > 0)
                    {
                        if (!actionChords.TryGetValue(currentActionId, out var chords))
                        {
                            chords = [];
                            actionChords[currentActionId] = chords;
                        }
                        chords.Add(new KeyChord(currentChordKeys.ToList()));
                        currentChordKeys.Clear();
                    }
                    currentActionId = null;
                }
                else if (blockStack.Count > 0)
                {
                    blockStack.Pop();
                }
                continue;
            }

            // Opening brace on same line: "blockName{"
            // Or "blockName {" with space
            var braceIdx = trimmed.IndexOf('{');
            if (braceIdx >= 0)
            {
                var blockName = trimmed[..braceIdx].Trim();

                if (IsInHotkeysBlock(blockStack) && blockName.StartsWith("ID_", StringComparison.Ordinal))
                {
                    currentActionId = blockName;
                    currentChordKeys.Clear();
                }
                else
                {
                    blockStack.Push(blockName);
                }

                // Check if closing brace on same line: "block{}"
                if (trimmed.EndsWith('}'))
                {
                    if (currentActionId is not null)
                    {
                        currentActionId = null;
                    }
                    else if (blockStack.Count > 0)
                    {
                        blockStack.Pop();
                    }
                }
                continue;
            }

            // Key-value pair inside an action block
            if (currentActionId is not null && trimmed.Contains(":i=", StringComparison.Ordinal))
            {
                if (trimmed.StartsWith("keyboardKey:i=", StringComparison.Ordinal))
                {
                    var valueStr = trimmed["keyboardKey:i=".Length..];
                    if (int.TryParse(valueStr, out var scanCode))
                    {
                        currentChordKeys.Add(scanCode);
                    }
                }
            }
        }

        // Convert actionChords to KeyBinding dictionary
        foreach (var (actionStr, chords) in actionChords)
        {
            var actionId = ActionIdMapping.FromBlkId(actionStr);
            result[actionStr] = new KeyBinding(actionId, chords.AsReadOnly());
        }

        return result;
    }

    private static bool IsInHotkeysBlock(Stack<string> blockStack)
    {
        // We need to be inside controls > hotkeys
        var items = blockStack.ToArray();
        // Stack is LIFO, so items[0] is the innermost
        return items.Length >= 2
               && items[0].Equals("hotkeys", StringComparison.OrdinalIgnoreCase)
               && items[1].Equals("controls", StringComparison.OrdinalIgnoreCase);
    }
}
