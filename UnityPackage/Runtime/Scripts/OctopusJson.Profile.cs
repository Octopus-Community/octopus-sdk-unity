using System;
using System.Collections.Generic;

internal static partial class OctopusJson
{
    // Preserve raw value types for the nested profile wire format. Existing flat readers are unchanged.
    internal static Dictionary<string, string> ParseRawObject(string json)
    {
        var fields = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json)) return fields;
        int i = 0;
        SkipWs(json, ref i);
        if (i == json.Length || json[i++] != '{') return fields;
        while (i < json.Length)
        {
            SkipWs(json, ref i);
            if (i == json.Length || json[i] != '"') break;
            string key = ParseString(json, ref i);
            SkipWs(json, ref i);
            if (i == json.Length || json[i++] != ':') break;
            SkipWs(json, ref i);
            fields[key] = ReadRawValue(json, ref i);
            SkipWs(json, ref i);
            if (i == json.Length || json[i++] != ',') break;
        }
        return fields;
    }

    internal static List<string> ParseStringArray(string json)
    {
        var values = new List<string>();
        if (string.IsNullOrEmpty(json)) return values;
        int i = 0;
        SkipWs(json, ref i);
        if (i == json.Length || json[i++] != '[') return values;
        while (i < json.Length)
        {
            SkipWs(json, ref i);
            if (i == json.Length || json[i] == ']') break;
            string raw = ReadRawValue(json, ref i);
            string value = StringFromRaw(raw);
            if (value != null) values.Add(value);
            SkipWs(json, ref i);
            if (i == json.Length || json[i++] != ',') break;
        }
        return values;
    }

    internal static string StringFromRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw[0] != '"') return null;
        int i = 0;
        return ParseString(raw, ref i);
    }

    private static string ReadRawValue(string json, ref int i)
    {
        int start = i;
        int depth = 0;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"') { ParseString(json, ref i); continue; }
            if (c == '[' || c == '{') depth++;
            else if (c == ']' || c == '}')
            {
                if (depth == 0) break;
                depth--;
            }
            else if (c == ',' && depth == 0) break;
            i++;
        }
        return json.Substring(start, i - start).Trim();
    }
}
