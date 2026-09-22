using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Rules for the mod's Harmony hooks, checked on the source because the hooks need the game's assemblies to compile
// and these checks run without them. Comments and string contents are blanked first, so a rule that is only written
// in a comment does not count. The scan is a heuristic: it knows the usual ways to declare and install a hook (listed
// on each rule), and the sample in Program.cs pins down each one.
internal static class HarmonyRules
{
    // A method returning bool or void (the only return types a Harmony prefix can have), with the attributes written
    // directly above it, up to the parenthesis that opens its parameters (read with Arguments, so they can span lines).
    private static readonly Regex HookMethod = new Regex(
        @"(?<attrs>(?:\[[^\[\]]*\]\s*)*)" +
        @"(?<mods>(?:\b(?:public|private|protected|internal|static|unsafe|new|extern)\s+)*)" +
        @"\b(?<returns>bool|Boolean|System\.Boolean|void)\s+(?<name>\w+)\s*\(");
    // Any method declared with an access or static modifier, up to the parenthesis that opens its parameters.
    private static readonly Regex MethodDeclaration = new Regex(
        @"\b(?:public|private|protected|internal|static)\s+[\w.<>\[\],?]+\s+(?<name>\w+)\s*\(");
    private static readonly Regex Static = new Regex(@"\bstatic\b");
    private static readonly Regex ReturnsBool = new Regex(@"^(?:bool|Boolean|System\.Boolean)$");
    // Harmony fills parameters named __instance, __result, __state, ___field and so on; plain methods have none.
    private static readonly Regex InjectedParameter = new Regex(@"\b__\w+");
    // A prefix of either return type skips the original when it sets this parameter to false.
    private static readonly Regex SetsRunOriginal = new Regex(
        @"\bref\s+(?:bool|Boolean|System\.Boolean)\s+__runOriginal\b");
    private static readonly Regex HarmonyPrefix = new Regex(@"\bHarmonyPrefix(?:Attribute)?\b");
    private static readonly Regex OtherPatchKind = new Regex(@"(?:Postfix|Finalizer|Transpiler)$");
    private static readonly Regex RunsLast = new Regex(
        @"\bHarmonyPriority(?:Attribute)?\s*\(\s*(?:HarmonyLib\.)?Priority\.Last\s*\)");
    private static readonly Regex UnpatchAll = new Regex(@"\bUnpatchAll\s*\(");
    private static readonly Regex Unpatch = new Regex(@"\.\s*Unpatch\s*\(");
    // Harmony.Patch(original, prefix, postfix, ...), and PatchProcessor.AddPrefix(prefix).
    private static readonly Regex HarmonyPatch = new Regex(@"\.\s*Patch\s*\(");
    private static readonly Regex AddPrefix = new Regex(@"\bAddPrefix\s*\(");
    private static readonly Regex NameOf = new Regex(@"\bnameof\s*\(\s*(?:[\w.]+\.)?(?<name>\w+)\s*\)");
    // A parameter named prefix, as in Harmony.Patch or a helper that wraps it.
    private static readonly Regex PrefixParameter = new Regex(@"\s@?prefix\s*(?:=.*)?$");
    // A named argument ("prefix: x"), but not an alias qualifier ("global::x").
    private static readonly Regex NamedArgument = new Regex(@"^@?(?<name>\w+)\s*:(?!:)\s*");
    // Harmony's owner id for every owner. CodeOnly keeps this one literal so the Unpatch rule can see it.
    private const string EveryOwner = "\"*\"";

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

    // Every Harmony prefix that can skip the original, as "File.cs: Method". Such a prefix can replace the original, so
    // where it runs among other mods' prefixes on the same method decides what happens. Without an explicit priority
    // that order is the order the mods patched in, which is each player's mod load order; lockstep co-op needs one order
    // for everyone, and the rule is that a replacing prefix runs last. A replacing prefix is a static method that takes
    // ref bool __runOriginal, or a static bool method that is a prefix: installed as one (see InstalledPrefixes), named
    // *Prefix, marked [HarmonyPrefix], or taking a parameter Harmony fills (and not named as another kind of patch).
    // Those without [HarmonyPriority(Priority.Last)] are listed in notLast.
    public static List<string> ReplacingPrefixes(List<(string Name, string Code)> files, out List<string> notLast)
    {
        List<string> installed = InstalledPrefixes(files);
        List<string> prefixes = new List<string>();
        notLast = new List<string>();
        foreach ((string file, string code) in files)
        {
            foreach (Match method in HookMethod.Matches(code))
            {
                string name = method.Groups["name"].Value;
                string attributes = method.Groups["attrs"].Value;
                if (!Static.IsMatch(method.Groups["mods"].Value))
                {
                    continue;
                }
                string parameters = string.Join(", ", Arguments(code, method.Index + method.Length));
                bool prefix = installed.Contains(name) || name.EndsWith("Prefix", StringComparison.Ordinal) ||
                              HarmonyPrefix.IsMatch(attributes) ||
                              (InjectedParameter.IsMatch(parameters) && !OtherPatchKind.IsMatch(name));
                bool replacing = SetsRunOriginal.IsMatch(parameters) ||
                                 (prefix && ReturnsBool.IsMatch(method.Groups["returns"].Value));
                if (!replacing)
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

    // The methods the files install as prefixes, sorted: every nameof(...) in the prefix argument of an install call.
    // Those are Harmony.Patch (its second argument, or the one named prefix), PatchProcessor.AddPrefix, and any helper
    // declared in the files with a parameter named prefix, such as the mod's own Patches.Patch. A name passed as a
    // plain string is not seen.
    public static List<string> InstalledPrefixes(List<(string Name, string Code)> files)
    {
        // Helper name -> position of its prefix parameter.
        SortedDictionary<string, int> helpers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach ((string file, string code) in files)
        {
            foreach (Match method in MethodDeclaration.Matches(code))
            {
                List<string> parameters = Arguments(code, method.Index + method.Length);
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (PrefixParameter.IsMatch(parameters[i]))
                    {
                        helpers[method.Groups["name"].Value] = i;
                    }
                }
            }
        }
        SortedSet<string> installed = new SortedSet<string>(StringComparer.Ordinal);
        foreach ((string file, string code) in files)
        {
            foreach (Match call in HarmonyPatch.Matches(code))
            {
                AddNames(installed, PrefixArgument(Arguments(code, call.Index + call.Length), 1));
            }
            foreach (Match call in AddPrefix.Matches(code))
            {
                AddNames(installed, string.Join(", ", Arguments(code, call.Index + call.Length)));
            }
            foreach (KeyValuePair<string, int> helper in helpers)
            {
                // The helper called by its own name; a member call such as harmony.Patch is Harmony's, read above.
                Regex call = new Regex(@"(?<!\.\s*)\b" + Regex.Escape(helper.Key) + @"\s*\(");
                foreach (Match match in call.Matches(code))
                {
                    AddNames(installed, PrefixArgument(Arguments(code, match.Index + match.Length), helper.Value));
                }
            }
        }
        return new List<string>(installed);
    }

    // The argument given for the prefix parameter: the one named prefix, or else the one at position.
    private static string PrefixArgument(List<string> arguments, int position)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            Match named = NamedArgument.Match(arguments[i]);
            if (!named.Success ? i == position : named.Groups["name"].Value == "prefix")
            {
                return named.Success ? arguments[i].Substring(named.Length) : arguments[i];
            }
        }
        return "";
    }

    private static void AddNames(SortedSet<string> names, string argument)
    {
        foreach (Match name in NameOf.Matches(argument))
        {
            names.Add(name.Groups["name"].Value);
        }
    }

    // Unpatch calls that could take off other mods' patches, as "File.cs: call": any UnpatchAll (the rule is never to
    // call it; with no id it removes every mod's patches), and Unpatch(method, HarmonyPatchType, owner) whose owner is
    // left out, which defaults to "*", or is "*" itself: both mean every owner. Named arguments are read by name.
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
                bool byType = false;
                string owner = null;
                for (int i = 0; i < arguments.Count; i++)
                {
                    Match named = NamedArgument.Match(arguments[i]);
                    string name = named.Success ? named.Groups["name"].Value : null;
                    string value = named.Success ? arguments[i].Substring(named.Length) : arguments[i];
                    if (name == "type" ||
                        (name == null && i == 1 && Regex.IsMatch(value, @"^(?:HarmonyLib\.)?HarmonyPatchType\.")))
                    {
                        byType = true;
                    }
                    else if (name == "harmonyID" || (name == null && i == 2))
                    {
                        owner = value;
                    }
                }
                if (byType && (owner == null || owner == EveryOwner))
                {
                    found.Add(file + ": Unpatch(" + string.Join(", ", arguments) + ")" +
                              (owner == null ? " with no owner" : " for every owner"));
                }
            }
        }
        return found;
    }

    // The source with comments removed and every string or character literal reduced to "" or ' ', except "*", which
    // stays (see EveryOwner).
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
                int start = i;
                i = SkipString(source, i);
                code.Append(source.Substring(start, i - start) == EveryOwner ? EveryOwner : "\"\"");
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
