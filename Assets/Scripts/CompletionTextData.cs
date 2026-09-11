using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CompletionTextData", menuName = "ATC/Completion Text Data")]
public class CompletionTextData : ScriptableObject
{
    [TextArea(2, 4)]
    public List<string> lines = new List<string>();

    public string GetRandom()
    {
        if (lines == null || lines.Count == 0) return string.Empty;
        return lines[Random.Range(0, lines.Count)];
    }
}