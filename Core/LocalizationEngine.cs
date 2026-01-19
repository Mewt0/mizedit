using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;

namespace MizEdit.Core;

public sealed class LocalizationEngine
{
    public LocalizationEngine(string workDir)
    {
        WorkDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string WorkDir { get; }

    private string L10nRoot => Path.Combine(WorkDir, "l10n");

    public Dictionary<string, string> LoadMapResource(string locale)
    {
        var path = Path.Combine(L10nRoot, locale, "mapResource");
        var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
            return resources;

        try
        {
            var text = File.ReadAllText(path).TrimStart('\uFEFF');
            var code = BuildMapResourceWrapper(text);

            var script = new Script(CoreModules.Preset_Complete);
            script.DoString(code);

            var dyn = script.Globals.Get("mapResource");
            if (dyn.Type != DataType.Table) return resources;

            foreach (var pair in dyn.Table.Pairs)
            {
                if (pair.Key.Type == DataType.String && pair.Value.Type == DataType.String)
                {
                    resources[pair.Key.String] = pair.Value.String;
                }
            }
        }
        catch
        {
            // Если не удалось прочитать mapResource — вернём пустой словарь
        }

        return resources;
    }

    private static string BuildMapResourceWrapper(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("mapResource", StringComparison.Ordinal))
            return text;
        if (trimmed.StartsWith("return", StringComparison.Ordinal))
        {
            var rest = trimmed.Substring("return".Length).TrimStart();
            return "mapResource = " + rest;
        }
        if (trimmed.StartsWith("{"))
            return "mapResource = " + trimmed;
        return "mapResource = " + trimmed;
    }

    public IReadOnlyList<string> GetLocales()
    {
        var locales = new List<string>();

        if (Directory.Exists(L10nRoot))
        {
            locales.AddRange(Directory.GetDirectories(L10nRoot)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Select(name => name!));
        }

        if (!locales.Contains("DEFAULT"))
            locales.Insert(0, "DEFAULT");

        return locales;
    }

    public void AddLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return;
        Directory.CreateDirectory(Path.Combine(L10nRoot, locale));
    }

    public void DeleteLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return;
        if (locale.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase)) return;

        var dir = Path.Combine(L10nRoot, locale);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public string ResolveBriefingString(MissionLua mission, string missionKey, string locale)
    {
        var raw = mission.GetString(missionKey);
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        if (raw.StartsWith("DictKey_", StringComparison.OrdinalIgnoreCase))
        {
            var dictVal = GetFromDictionary(raw, locale);
            if (!string.IsNullOrEmpty(dictVal)) return dictVal;

            // fallback на DEFAULT
            dictVal = GetFromDictionary(raw, "DEFAULT");
            if (!string.IsNullOrEmpty(dictVal)) return dictVal;

            // Если нигде не нашли — вернем сам ключ, чтобы было видно
            return raw;
        }

        return raw;
    }

    public void SetBriefingString(MissionLua mission, string missionKey, string value, string locale)
    {
        var current = mission.GetString(missionKey);
        if (current.StartsWith("DictKey_", StringComparison.OrdinalIgnoreCase))
        {
            // обновляем dictionary по ключу
            WriteToDictionary(current, locale, value);
        }
        else
        {
            // прямое значение в mission
            mission.SetString(missionKey, value);
        }
    }

    public void ExportTxt(MissionLua mission, string locale, string outPath)
    {
        var lines = new[]
        {
            $"locale={locale}",
            $"name={ResolveBriefingString(mission, "name", locale)}",
            $"descriptionText={ResolveBriefingString(mission, "descriptionText", locale)}",
            $"descriptionRedTask={ResolveBriefingString(mission, "descriptionRedTask", locale)}",
            $"descriptionBlueTask={ResolveBriefingString(mission, "descriptionBlueTask", locale)}",
        };

        File.WriteAllLines(outPath, lines);
    }

    public void ImportTxt(MissionLua mission, string locale, string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("TXT not found", path);

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..];

            switch (key)
            {
                case "name":
                    SetBriefingString(mission, "name", value, locale);
                    break;
                case "descriptionText":
                    SetBriefingString(mission, "descriptionText", value, locale);
                    break;
                case "descriptionRedTask":
                    SetBriefingString(mission, "descriptionRedTask", value, locale);
                    break;
                case "descriptionBlueTask":
                    SetBriefingString(mission, "descriptionBlueTask", value, locale);
                    break;
                default:
                    break;
            }
        }
    }

    private string GetFromDictionary(string dictKey, string locale)
    {
        var dict = LoadDictionary(locale);
        return dict.TryGetValue(dictKey, out var val) ? val : string.Empty;
    }

    private void WriteToDictionary(string dictKey, string locale, string value)
    {
        var dict = LoadDictionary(locale);
        dict[dictKey] = value ?? string.Empty;
        SaveDictionary(locale, dict);
    }

    public string? GetDictionaryValue(string locale, string dictKey)
    {
        return GetFromDictionary(dictKey, locale);
    }

    public void UpdateDictionaryEntry(string locale, string dictKey, string value)
    {
        WriteToDictionary(dictKey, locale, value);
    }

    private Dictionary<string, string> LoadDictionary(string locale)
    {
        var path = Path.Combine(L10nRoot, locale, "dictionary");
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
            return dict;

        try
        {
            var text = File.ReadAllText(path).TrimStart('\uFEFF');
            var code = BuildDictionaryWrapper(text);

            var script = new Script(CoreModules.Preset_Complete);
            script.DoString(code);

            var dyn = script.Globals.Get("dictionary");
            if (dyn.Type != DataType.Table) return dict;

            foreach (var pair in dyn.Table.Pairs)
            {
                if (pair.Key.Type == DataType.String && pair.Value.Type == DataType.String)
                {
                    dict[pair.Key.String] = pair.Value.String;
                }
            }
        }
        catch
        {
            // Если не удалось прочитать dictionary — вернём пустой словарь
        }

        return dict;
    }

    private void SaveDictionary(string locale, Dictionary<string, string> dict)
    {
        Directory.CreateDirectory(Path.Combine(L10nRoot, locale));
        var path = Path.Combine(L10nRoot, locale, "dictionary");

        var table = new Table(new Script());
        foreach (var kv in dict)
        {
            table.Set(kv.Key, DynValue.NewString(kv.Value));
        }

        var serialized = LuaTableSerializer.SerializeTable(table);
        var text = "dictionary = " + serialized;
        File.WriteAllText(path, text);
    }

    private static string BuildDictionaryWrapper(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("dictionary", StringComparison.Ordinal))
            return text;
        if (trimmed.StartsWith("return", StringComparison.Ordinal))
        {
            var rest = trimmed.Substring("return".Length).TrimStart();
            return "dictionary = " + rest;
        }
        if (trimmed.StartsWith("{"))
            return "dictionary = " + trimmed;
        return "dictionary = " + trimmed;
    }
}
