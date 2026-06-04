using System.Collections.Generic;
using System.Text;

// Minimal, dependency-free JSON for the SDK's wire formats only:
// a flat object {string: string|bool|number}, and arrays of such objects.
// Not a general-purpose JSON parser.
internal static class OctopusJson
{
    public static Dictionary<string, string> ParseObject(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json)) return result;
        int i = 0;
        SkipWs(json, ref i);
        if (i >= json.Length || json[i] != '{') return result;
        i++; // {
        SkipWs(json, ref i);
        if (i < json.Length && json[i] == '}') return result;
        while (i < json.Length)
        {
            SkipWs(json, ref i);
            string key = ParseString(json, ref i);
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == ':') i++;
            SkipWs(json, ref i);
            string value = ParseValue(json, ref i);
            result[key] = value;
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == ',') { i++; continue; }
            break; // '}' or end
        }
        return result;
    }

    public static List<Dictionary<string, string>> ParseArray(string json)
    {
        var list = new List<Dictionary<string, string>>();
        if (string.IsNullOrEmpty(json)) return list;
        int i = 0;
        SkipWs(json, ref i);
        if (i >= json.Length || json[i] != '[') return list;
        i++; // [
        SkipWs(json, ref i);
        if (i < json.Length && json[i] == ']') return list;
        while (i < json.Length)
        {
            SkipWs(json, ref i);
            if (json[i] != '{') break;
            int objStart = i;
            SkipObject(json, ref i);          // advance past the object
            list.Add(ParseObject(json.Substring(objStart, i - objStart)));
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == ',') { i++; continue; }
            break;
        }
        return list;
    }

    static string ParseValue(string s, ref int i)
    {
        if (i >= s.Length) return "";
        char c = s[i];
        if (c == '"') return ParseString(s, ref i);
        // bool / null / number: read until , } ] or whitespace
        int start = i;
        while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i])) i++;
        string tok = s.Substring(start, i - start);
        if (tok == "null") return "";
        return tok; // "true"/"false" stay as-is; numbers stay as-is
    }

    static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        if (i >= s.Length || s[i] != '"') return "";
        i++; // opening quote
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') break;
            if (c == '\\' && i < s.Length)
            {
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '/': sb.Append('/'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)System.Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    static void SkipObject(string s, ref int i)
    {
        // assumes s[i] == '{'; advances i past the matching '}', respecting strings
        int depth = 0;
        bool inStr = false;
        while (i < s.Length)
        {
            char c = s[i++];
            if (inStr)
            {
                if (c == '\\') { if (i < s.Length) i++; }
                else if (c == '"') inStr = false;
            }
            else
            {
                if (c == '"') inStr = true;
                else if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return; }
            }
        }
    }

    static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    // Writes an array of flat objects. Keys listed in `rawKeys` are emitted
    // unquoted (booleans/numbers); all other values are emitted as JSON strings.
    public static string WriteArray(List<Dictionary<string, string>> rows, HashSet<string> rawKeys)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0) sb.Append(',');
            sb.Append('{');
            bool first = true;
            foreach (var kv in rows[r])
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":");
                if (rawKeys.Contains(kv.Key)) sb.Append(kv.Value);          // true/false/number
                else sb.Append('"').Append(Escape(kv.Value)).Append('"');
            }
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
