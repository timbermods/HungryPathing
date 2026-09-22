using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Rules for the mod's Harmony hooks, checked on the source because the hooks need the game's assemblies to compile
// and these checks run without them. Comments and string contents are blanked first, so a rule that is only written
// in a comment does not count.
internal static class HarmonyRules
{
    // A static method returning bool, with the attributes written directly above it.
    private static readonly Regex BoolMethod = new Regex(
        @"(?<attrs>(?:\[[^\[\]]*\]\s*)*)" +
        @"(?<mods>(?:\b(?:public|private|protected|internal|static|unsafe|new|extern)\s+)*)" +
        @"\b(?:bool|Boolean|System\.Boolean)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)");
    // Harmony fills parameters named __instance, __result, __state, ___field and so on; plain methods have none.
    private static readonly Regex InjectedParameter = new Regex(@"\b__\w+");
    private static readonly Regex HarmonyPrefix = new Regex(@"\bHarmonyPrefix(?:Attribute)?\b");
    private static readonly Regex OtherPatchKind = new Regex(@"(?:Postfix|Finalizer|Transpiler)$");
    private static readonly Regex RunsLast = new Regex(
        @"\bHarmonyPriority(?:Attribute)?\s*\(\s*(?:HarmonyLib\.)?Priority\.Last\s*\)");
    private static readonly Regex UnpatchAll = new Regex(@"\bUnpatchAll\s*\(");
    private static readonly Regex Unpatch = new Regex(@"\.\s*Unpatch\s*\(");

    // The mod's source files (everything under source\ except build output), comments and strings blanked. Empty when
    // the folder cannot be found from where the checks were built or run.
    public static List<(string Name, string Code)> ReadModSources()
    {
        List<(string Name, string Code)> files = new List<(string Name, string Code)>();
        string folder = FindSourceFolder();
        if (folder == null)
        {
            return files;
        }
        string[] paths = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            string relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal) ||
                relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }
            files.Add((relative, CodeOnly(File.ReadAllText(path))));
        }
        return files;
    }

    // Every Harmony prefix that returns bool, as "File.cs: Method". Such a prefix can return false and replace the
    // original, so where it runs among other mods' prefixes on the same method decides what happens. Without an
    // explicit priority that order is the order the mods patched in, which is each player's mod load order; lockstep
    // co-op needs one order for everyone, and the rule is that a replacing prefix runs last. A prefix is a method named
    // *Prefix, marked [HarmonyPrefix], or taking a parameter Harmony fills (and not named as another kind of patch).
    // Those without [HarmonyPriority(Priority.Last)] are listed in notLast.
    public static List<string> ReplacingPrefixes(List<(string Name, string Code)> files, out List<string> notLast)
    {
        List<string> prefixes = new List<string>();
        notLast = new List<string>();
        foreach ((string file, string code) in files)
        {
            foreach (Match method in BoolMethod.Matches(code))
            {
                string name = method.Groups["name"].Value;
                string attributes = method.Groups["attrs"].Value;
                if (!Regex.IsMatch(method.Groups["mods"].Value, @"\bstatic\b"))
                {
                    continue;
                }
                bool prefix = name.EndsWith("Prefix", StringComparison.Ordinal) || HarmonyPrefix.IsMatch(attributes) ||
                              (InjectedParameter.IsMatch(method.Groups["params"].Value) && !OtherPatchKind.IsMatch(name));
                if (!prefix)
                {
                    continue;
                }
                prefixes.Add(file + ": " + name);
                if (!RunsLast.IsMatch(attributes))
                {
                    notLast.Add(file + ": " + name);
                }
            }
        }
        return prefixes;
    }

    // Unpatch calls that could take off other mods' patches, as "File.cs: call": any UnpatchAll (the rule is never to
    // call it; with no id it removes every mod's patches), and Unpatch(method, HarmonyPatchType) without an owner id,
    // which defaults to "*", every owner.
    public static List<string> UnscopedUnpatches(List<(string Name, string Code)> files)
    {
        List<string> found = new List<string>();
        foreach ((string file, string code) in files)
        {
            foreach (Match call in UnpatchAll.Matches(code))
            {
                found.Add(file + ": UnpatchAll");
            }
            foreach (Match call in Unpatch.Matches(code))
            {
                List<string> arguments = Arguments(code, call.Index + call.Length);
                if (arguments.Count == 2 && arguments[1].StartsWith("HarmonyPatchType.", StringComparison.Ordinal))
                {
                    found.Add(file + ": Unpatch(" + string.Join(", ", arguments) + ") with no owner");
                }
            }
        }
        return found;
    }

    // The source with comments removed and every string or character literal reduced to "" or ' '.
    public static string CodeOnly(string source)
    {
        StringBuilder code = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                int end = source.IndexOf('\n', i);
                i = end < 0 ? source.Length : end;
            }
            else if (c == '/' && next == '*')
            {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 2;
                code.Append(' ');
            }
            else if (c == '"' || ((c == '@' || c == '$') && (next == '"' || next == '@' || next == '$')))
            {
                i = SkipString(source, i);
                code.Append("\"\"");
            }
            else if (c == '\'')
            {
                i = SkipCharacter(source, i);
                code.Append("' '");
            }
            else
            {
                code.Append(c);
                i++;
            }
        }
        return code.ToString();
    }

    // The index just past the string literal starting at start (at its @ or $ prefix, or its opening quote).
    private static int SkipString(string s, int start)
    {
        int i = start;
        bool verbatim = false;
        bool interpolated = false;
        while (i < s.Length && (s[i] == '@' || s[i] == '$'))
        {
            verbatim |= s[i] == '@';
            interpolated |= s[i] == '$';
            i++;
        }
        int quotes = 0;
        while (i + quotes < s.Length && s[i + quotes] == '"')
        {
            quotes++;
        }
        if (quotes >= 3)
        {
            // A raw string literal ends at the same run of quotes that opened it.
            int end = s.IndexOf(new string('"', quotes), i + quotes, StringComparison.Ordinal);
            return end < 0 ? s.Length : end + quotes;
        }
        i++;
        while (i < s.Length)
        {
            char c = s[i];
            if (interpolated && c == '{')
            {
                if (i + 1 < s.Length && s[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }
                i = SkipHole(s, i + 1);
                continue;
            }
            if (verbatim)
            {
                if (c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }
                    return i + 1;
                }
                i++;
                continue;
            }
            if (c == '\\')
            {
                i += 2;
                continue;
            }
            if (c == '"' || c == '\n')
            {
                return i + 1;
            }
            i++;
        }
        return s.Length;
    }

    // The index just past the } that closes an interpolation hole whose code starts at start. The hole's code can hold
    // strings and braces of its own.
    private static int SkipHole(string s, int start)
    {
        int depth = 1;
        int i = start;
        while (i < s.Length)
        {
            char c = s[i];
            char next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '"' || ((c == '@' || c == '$') && (next == '"' || next == '@' || next == '$')))
            {
                i = SkipString(s, i);
                continue;
            }
            if (c == '\'')
            {
                i = SkipCharacter(s, i);
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                return i + 1;
            }
            i++;
        }
        return s.Length;
    }

    private static int SkipCharacter(string s, int start)
    {
        int i = start + 1;
        while (i < s.Length)
        {
            if (s[i] == '\\')
            {
                i += 2;
                continue;
            }
            if (s[i] == '\'' || s[i] == '\n')
            {
                return i + 1;
            }
            i++;
        }
        return s.Length;
    }

    // The top-level arguments of the call whose argument list starts at start (just past its opening parenthesis),
    // trimmed. Code comes from CodeOnly, so no literal holds a bracket or a comma.
    private static List<string> Arguments(string code, int start)
    {
        List<string> arguments = new List<string>();
        int depth = 0;
        int from = start;
        for (int i = start; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                if (depth == 0)
                {
                    string last = code.Substring(from, i - from).Trim();
                    if (last.Length > 0 || arguments.Count > 0)
                    {
                        arguments.Add(last);
                    }
                    return arguments;
                }
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                arguments.Add(code.Substring(from, i - from).Trim());
                from = i + 1;
            }
        }
        return arguments;
    }

    // source\ next to tests\, found by walking up from the build output (tests\bin\<config>\net8.0) or from the
    // working directory.
    private static string FindSourceFolder()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                string folder = Path.Combine(directory.FullName, "source");
                if (File.Exists(Path.Combine(folder, "HungryPathing.csproj")))
                {
                    return folder;
                }
            }
        }
        return null;
    }
}
