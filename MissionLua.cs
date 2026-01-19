using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;

namespace MizEdit.Core;

public sealed class MissionLua
{
    public Table MissionTable { get; private set; } = null!;

    private readonly Script _lua = new Script(CoreModules.Preset_Complete);

    public void LoadFromMissionFile(string missionPath)
    {
        if (!File.Exists(missionPath))
            throw new FileNotFoundException("Не найден файл mission", missionPath);

        var text = File.ReadAllText(missionPath).TrimStart('\uFEFF'); // убрать BOM
        var code = BuildMissionWrapper(text);

        try
        {
            _lua.DoString(code);
        }
        catch (SyntaxErrorException ex)
        {
            throw new InvalidOperationException($"Lua parse error: {ex.DecoratedMessage}", ex);
        }

        var missionDyn = _lua.Globals.Get("mission");
        if (missionDyn.Type != DataType.Table)
            throw new InvalidOperationException("Lua-объект mission не является таблицей.");

        MissionTable = missionDyn.Table;
    }

    private static string BuildMissionWrapper(string text)
    {
        var trimmed = text.TrimStart();

        // Если уже начинается с "mission" или "local mission" — оставляем как есть.
        if (trimmed.StartsWith("mission", StringComparison.Ordinal) ||
            trimmed.StartsWith("local mission", StringComparison.Ordinal))
        {
            return text;
        }

        // Если начинается с return { ... }
        if (trimmed.StartsWith("return", StringComparison.Ordinal))
        {
            var rest = trimmed.Substring("return".Length).TrimStart();
            return "mission = " + rest;
        }

        // Если начинается с таблицы
        if (trimmed.StartsWith("{"))
        {
            return "mission = " + trimmed;
        }

        // Фоллбек — всё равно заворачиваем
        return "mission = " + trimmed;
    }

    public void SaveToMissionFile(string missionPath)
    {
        if (MissionTable == null)
            throw new InvalidOperationException("MissionTable is null");

        var luaText = LuaTableSerializer.SerializeTable(MissionTable);
        File.WriteAllText(missionPath, luaText);
    }

    // Удобные геттеры/сеттеры под Briefing
    public string GetString(params string[] keys)
    {
        // попробуем несколько вариантов ключей (в миссиях они иногда разные)
        foreach (var key in keys)
        {
            var v = MissionTable.Get(key);
            if (v.Type == DataType.String)
                return v.String;
        }
        return "";
    }

    public void SetString(string key, string value)
    {
        MissionTable.Set(key, DynValue.NewString(value ?? ""));
    }

    public List<string> GetPictureFileNames()
    {
        var pictures = new List<string>();
        
        // Читаем из pictureFileNameB, pictureFileNameR, pictureFileNameN, pictureFileNameServer
        foreach (var key in new[] { "pictureFileNameB", "pictureFileNameR", "pictureFileNameN", "pictureFileNameServer" })
        {
            var val = MissionTable.Get(key);
            if (val.Type == DataType.Table)
            {
                foreach (var pair in val.Table.Pairs)
                {
                    if (pair.Value.Type == DataType.String && !string.IsNullOrWhiteSpace(pair.Value.String))
                    {
                        pictures.Add(pair.Value.String);
                    }
                }
            }
        }

        return pictures;
    }

    public void AddPictures(List<string> pictures)
    {
        // Сохраняем обновленные картинки обратно в mission
        var keys = new[] { "pictureFileNameB", "pictureFileNameR", "pictureFileNameN", "pictureFileNameServer" };
        
        // Очищаем старые значения
        foreach (var key in keys)
        {
            var val = MissionTable.Get(key);
            if (val.Type == DataType.Table)
            {
                var table = val.Table;
                var keysToRemove = table.Pairs.Select(p => p.Key).ToList();
                foreach (var k in keysToRemove)
                {
                    table.Remove(k);
                }
            }
        }

        // Добавляем новые значения в pictureFileNameB (или создаем если нет)
        var pictureKey = "pictureFileNameB";
        var pictureVal = MissionTable.Get(pictureKey);
        
        if (pictureVal.Type != DataType.Table)
        {
            // Создаем новую таблицу
            pictureVal = DynValue.NewTable(new Table(new Script()));
            MissionTable.Set(pictureKey, pictureVal);
        }

        var pictureTable = pictureVal.Table;
        int index = 1;
        foreach (var pic in pictures)
        {
            pictureTable.Set(index, DynValue.NewString(pic));
            index++;
        }
    }

    public void RemovePicture(string pictureName)
    {
        var pictures = GetPictureFileNames();
        pictures.RemoveAll(p => p.Equals(pictureName, StringComparison.OrdinalIgnoreCase));
        AddPictures(pictures);
    }

    public List<string> GetAudioFileNames()
    {
        // Audio обычно не хранится в mission как массив, а через mapResource
        // Пока возвращаем пустой список, т.к. звуки надо брать из mapResource
        return new List<string>();
    }

    public List<string> GetTriggers()
    {
        var triggers = new List<string>();
        
        var trig = MissionTable.Get("trig");
        if (trig.Type != DataType.Table) return triggers;

        // Читаем conditions, actions, func
        var conditions = trig.Table.Get("conditions");
        var actions = trig.Table.Get("actions");
        var func = trig.Table.Get("func");
        var funcStartup = trig.Table.Get("funcStartup");

        if (conditions.Type == DataType.Table)
        {
            foreach (var pair in conditions.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                {
                    triggers.Add($"Condition[{pair.Key}]: {pair.Value.String}");
                }
            }
        }

        if (actions.Type == DataType.Table)
        {
            foreach (var pair in actions.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                {
                    triggers.Add($"Action[{pair.Key}]: {pair.Value.String}");
                }
            }
        }

        if (func.Type == DataType.Table)
        {
            foreach (var pair in func.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                {
                    triggers.Add($"Func[{pair.Key}]: {pair.Value.String}");
                }
            }
        }

        if (funcStartup.Type == DataType.Table)
        {
            foreach (var pair in funcStartup.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                {
                    triggers.Add($"FuncStartup[{pair.Key}]: {pair.Value.String}");
                }
            }
        }

        return triggers;
    }

    public List<string> GetTrigRules()
    {
        var rules = new List<string>();
        
        var trigrules = MissionTable.Get("trigrules");
        if (trigrules.Type != DataType.Table) return rules;

        // trigrules это массив правил триггеров
        foreach (var pair in trigrules.Table.Pairs)
        {
            if (pair.Value.Type == DataType.Table)
            {
                var rule = pair.Value.Table;
                var initValue = rule.Get("init");
                var flag = rule.Get("flag");
                var actions = rule.Get("actions");
                var conditions = rule.Get("conditions");

                var description = $"[Rule #{pair.Key}]";
                
                if (initValue.Type != DataType.Nil)
                    description += $" Init: {initValue}";
                
                if (flag.Type != DataType.Nil)
                    description += $" Flag: {flag}";

                rules.Add(description);

                // Добавляем условия
                if (conditions.Type == DataType.Table)
                {
                    foreach (var cond in conditions.Table.Pairs)
                    {
                        rules.Add($"  Condition: {cond.Value}");
                    }
                }

                // Добавляем действия
                if (actions.Type == DataType.Table)
                {
                    foreach (var action in actions.Table.Pairs)
                    {
                        rules.Add($"  Action: {action.Value}");
                    }
                }
            }
        }

        return rules;
    }

    public class RadioMessage
    {
        public string GroupName { get; set; } = "";
        public int TaskIndex { get; set; }
        public int ActionIndex { get; set; }
        public string SubtitleKey { get; set; } = "";
        public string FileKey { get; set; } = "";
        public int Duration { get; set; }
        public string DisplayText { get; set; } = "";
    }

    public List<RadioMessage> GetRadioTransmissions()
    {
        var messages = new List<RadioMessage>();
        var debugLog = new System.Text.StringBuilder();
        
        debugLog.AppendLine("=== GetRadioTransmissions DEBUG ===");
        
        // Правильная структура: mission.coalition.blue/red.country[N].plane/helicopter.group[N].route.points[N].tasks[N]
        var coalition = MissionTable.Get("coalition");
        debugLog.AppendLine($"coalition: {coalition.Type}");
        if (coalition.Type != DataType.Table) 
        {
            System.IO.File.WriteAllText("radio_debug.txt", debugLog.ToString());
            return messages;
        }
        
        var coalitionNames = new[] { "blue", "red", "neutrals" };
        
        foreach (var coalName in coalitionNames)
        {
            var coal = coalition.Table.Get(coalName);
            debugLog.AppendLine($"Coalition '{coalName}': {coal.Type}");
            if (coal.Type != DataType.Table) continue;
            
            var countries = coal.Table.Get("country");
            debugLog.AppendLine($"  countries: {countries.Type}");
            if (countries.Type != DataType.Table) continue;
            
            int countryCount = 0;
            foreach (var countryPair in countries.Table.Pairs)
            {
                countryCount++;
                if (countryPair.Value.Type != DataType.Table) continue;
                var country = countryPair.Value.Table;
                
                // Проверяем plane и helicopter
                foreach (var vehicleType in new[] { "plane", "helicopter" })
                {
                    var vehicles = country.Get(vehicleType);
                    if (vehicles.Type != DataType.Table) continue;
                    
                    var groups = vehicles.Table.Get("group");
                    debugLog.AppendLine($"    {vehicleType}.group: {groups.Type}");
                    if (groups.Type != DataType.Table) continue;
                    
                    int groupCount = 0;
                    foreach (var groupPair in groups.Table.Pairs)
                    {
                        groupCount++;
                        if (groupPair.Value.Type != DataType.Table) continue;
                        var group = groupPair.Value.Table;
                        
                        var groupName = group.Get("name");
                        var groupNameStr = groupName.Type == DataType.String ? groupName.String : $"Group {groupPair.Key}";
                        
                        // ИСПРАВЛЕНИЕ: Ищем в route.points[N].tasks, а не в task.params.tasks
                        var route = group.Get("route");
                        debugLog.AppendLine($"      Group '{groupNameStr}' route: {route.Type}");
                        if (route.Type != DataType.Table) continue;
                        
                        var points = route.Table.Get("points");
                        debugLog.AppendLine($"        route.points: {points.Type}");
                        if (points.Type != DataType.Table) continue;
                        
                        int pointCount = 0;
                        foreach (var pointPair in points.Table.Pairs)
                        {
                            pointCount++;
                            debugLog.AppendLine($"          >> Processing Point[{pointPair.Key}], Type: {pointPair.Value.Type}");
                            if (pointPair.Value.Type != DataType.Table) 
                            {
                                debugLog.AppendLine($"          >> Point[{pointPair.Key}] is not a table, skipping");
                                continue;
                            }
                            var point = pointPair.Value.Table;
                            
                            // DEBUG: показываем все ключи в точке
                            var pointKeys = string.Join(", ", point.Keys.Select(k => k.Type == DataType.String ? k.String : k.ToString()));
                            debugLog.AppendLine($"          >> Point[{pointPair.Key}] keys: {pointKeys}");
                            
                            // Поддерживаем ДВА формата:
                            // 1) point.tasks (прямо в точке) - miss2
                            // 2) point.task.params.tasks - CI_00_v2_NSC
                            
                            DynValue tasksList;
                            
                            // Сначала пробуем прямой доступ к tasks
                            var directTasks = point.Get("tasks");
                            if (directTasks.Type == DataType.Table)
                            {
                                debugLog.AppendLine($"          >> Point[{pointPair.Key}].tasks (direct): Table");
                                tasksList = directTasks;
                            }
                            else
                            {
                                // Если нет прямого tasks, ищем через task.params.tasks
                                var pointTask = point.Get("task");
                                debugLog.AppendLine($"          >> Point[{pointPair.Key}].task: {pointTask.Type}");
                                if (pointTask.Type != DataType.Table) 
                                {
                                    debugLog.AppendLine($"          >> No task/tasks in point[{pointPair.Key}], skipping");
                                    continue;
                                }
                                
                                var pointTaskParams = pointTask.Table.Get("params");
                                if (pointTaskParams.Type != DataType.Table) continue;
                                
                                tasksList = pointTaskParams.Table.Get("tasks");
                                debugLog.AppendLine($"          >> Point[{pointPair.Key}].task.params.tasks: {tasksList.Type}");
                                if (tasksList.Type != DataType.Table) continue;
                            }
                            
                            int taskCount = 0;
                            foreach (var taskPair in tasksList.Table.Pairs)
                            {
                                taskCount++;
                                if (taskPair.Value.Type != DataType.Table) continue;
                                var task = taskPair.Value.Table;
                                
                                var taskParams = task.Get("params");
                                if (taskParams.Type != DataType.Table) continue;
                                
                                var action = taskParams.Table.Get("action");
                                if (action.Type != DataType.Table) continue;
                                
                                var actionId = action.Table.Get("id");
                                debugLog.AppendLine($"            Task[{taskPair.Key}] action.id: {actionId.Type} = '{(actionId.Type == DataType.String ? actionId.String : "N/A")}'");
                                
                                if (actionId.Type == DataType.String && actionId.String == "TransmitMessage")
                                {
                                    debugLog.AppendLine($"              *** FOUND TransmitMessage! ***");
                                    var actionParams = action.Table.Get("params");
                                    if (actionParams.Type == DataType.Table)
                                    {
                                        var subtitle = actionParams.Table.Get("subtitle");
                                        var file = actionParams.Table.Get("file");
                                        var duration = actionParams.Table.Get("duration");
                                        
                                        var msg = new RadioMessage
                                        {
                                            GroupName = groupNameStr,
                                            TaskIndex = Convert.ToInt32(taskPair.Key.Number),
                                            ActionIndex = Convert.ToInt32(pointPair.Key.Number),
                                            SubtitleKey = subtitle.Type == DataType.String ? subtitle.String : "",
                                            FileKey = file.Type == DataType.String ? file.String : "",
                                            Duration = duration.Type == DataType.Number ? (int)duration.Number : 0,
                                            DisplayText = $"{groupNameStr} [Point {pointPair.Key}, Task {taskPair.Key}]"
                                        };
                                        
                                        messages.Add(msg);
                                        debugLog.AppendLine($"              Added: {msg.SubtitleKey}");
                                    }
                                }
                            }
                            debugLog.AppendLine($"          Tasks in point[{pointPair.Key}]: {taskCount}");
                        }
                        debugLog.AppendLine($"        Points in group: {pointCount}");
                    }
                    debugLog.AppendLine($"      Groups in {vehicleType}: {groupCount}");
                }
            }
            debugLog.AppendLine($"  Countries: {countryCount}");
        }
        
        debugLog.AppendLine($"=== TOTAL MESSAGES FOUND: {messages.Count} ===");
        
        // Записываем лог в файл для анализа
        try
        {
            System.IO.File.WriteAllText("radio_debug.txt", debugLog.ToString());
        }
        catch { }
        
        return messages;
    }

    public List<string> GetTriggerPictures()
    {
        var trigPics = new List<string>();
        
        var trigPicData = MissionTable.Get("triggerPictures");
        if (trigPicData.Type != DataType.Table) return trigPics;

        foreach (var pair in trigPicData.Table.Pairs)
        {
            if (pair.Value.Type == DataType.String && !string.IsNullOrWhiteSpace(pair.Value.String))
            {
                trigPics.Add(pair.Value.String);
            }
        }

        return trigPics;
    }
}
