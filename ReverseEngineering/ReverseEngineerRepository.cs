using KenshiCore.Mods;
using KenshiCore.UI;
using KenshiCore.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace KenshiCore.ReverseEngineering
{
    public class ReverseEngineerRepository
    {

        private readonly Dictionary<string, Dictionary<string, ModRecord>>_mergedByTypeAndId = new(StringComparer.Ordinal);
        private static ReverseEngineerRepository? _instance;
        public bool busy { get; private set; } = false;

        private static readonly HashSet<string> _ignoredModNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "-KenshiFixer_Fix-.mod",
                //"-KenshiFixer_Bridge-.mod"
            };
        
        public static ReverseEngineerRepository Instance
        {
            get
            {
                if (_instance == null) _instance = new ReverseEngineerRepository();
                return _instance;
            }
        }
        public IReadOnlyDictionary<string, ModRecord> GetAllRecordsMerged(string recordType)
        {
            if (!_mergedByTypeAndId.TryGetValue(recordType, out var cached))
            {
                var (_, merged) = MergeRecords(
                    modNames: null,
                    recordType: recordType,
                    predicate: null,
                    maxRecords: int.MaxValue
                );
                _mergedByTypeAndId[recordType] = merged.ToDictionary(r => r.StringId, StringComparer.Ordinal);
            }
             return _mergedByTypeAndId[recordType];
        }
        public List<string> GetAllSuspiciousStringIds()
        {
            Dictionary<string, Dictionary<string, bool>> SnapshotsDictionary=new();
            HashSet<string> suspiciousStringIds = new();
            foreach (var modName in _loadOrder.AsEnumerable().Reverse())
            {
                if (_reverseEngineers.TryGetValue(modName, out var re))
                {
                    foreach (ModRecord record in re.modData.Records!)
                    {
                        if (suspiciousStringIds.Contains(record.StringId))
                        {
                            continue;
                        }
                        Dictionary<string, bool> snapshot = record.GetFilenameFieldSnapshot();
                        if (SnapshotsDictionary.TryGetValue(record.StringId, out var new_snapshot))
                        {
                            foreach (var kv in snapshot)
                            {
                                if (new_snapshot.ContainsKey(kv.Key) && !new_snapshot[kv.Key] && kv.Value)
                                {
                                    suspiciousStringIds.Add(record.StringId);
                                    break;
                                }
                            }
                            foreach (var kv in new_snapshot)
                            {
                                snapshot[kv.Key] = snapshot.TryGetValue(kv.Key, out bool v)? v: kv.Value;
                            }
                            SnapshotsDictionary[record.StringId] =new Dictionary<string, bool>(snapshot);
                        }
                        else
                        {
                            SnapshotsDictionary[record.StringId] = snapshot;
                        }
                    }
                }
            }
            return suspiciousStringIds.ToList();
        }
        /*public List<string> GetAllStringIds()
        {
            var stringIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modName in _loadOrder)
            {
                if (_reverseEngineers.TryGetValue(modName, out var re))
                {
                    foreach (string stringid in re.GetStringIdsNewRecords())
                    {
                        stringIds.Add(stringid);
                    }
                }
            }
            return stringIds.ToList();
        }*/

        public bool HasMergedRecord(string recordType, string stringId)
        {
            GetAllRecordsMerged(recordType); // ensure cache populated
            return _mergedByTypeAndId[recordType].ContainsKey(stringId);
        }
        // Dictionary keyed by mod name
        public readonly ConcurrentDictionary<string, ReverseEngineer> _reverseEngineers = new();
        public readonly List<string> _loadOrder = new();

        private ReverseEngineerRepository() { }

        // Add or replace a ReverseEngineer
        public bool AddOrUpdate(string modName, ReverseEngineer re)
        {
            if (_ignoredModNames.Contains(modName))
                return false; // silently ignore
            _reverseEngineers[modName] = re;
            _loadOrder.Add(modName);
            return true;
        }
        public ReverseEngineer? GetReverseEngineer(string modName)
        {
            if (_reverseEngineers.TryGetValue(modName, out var re))
                return re;
            return null;
        }
        public ConcurrentDictionary<string, ReverseEngineer>  getCache()
        {
            return _reverseEngineers;
        }

        // Get a specific ReverseEngineer by mod name
        public bool TryGet(string modName, out ReverseEngineer? re) => _reverseEngineers.TryGetValue(modName, out re);

        // Merge records from selected mods
        // modNames = null => all mods
        public (List<string> modNames, List<ModRecord> records) MergeRecords(
            IEnumerable<string>? modNames = null,
            string recordType = "",
            Func<ModRecord, bool>? predicate = null,
            int maxRecords = int.MaxValue)
        {
            IEnumerable<ReverseEngineer> selected;

            if (modNames == null)
                selected = _reverseEngineers.Values;
            else
                selected = modNames
                    .Where(n => _reverseEngineers.ContainsKey(n))
                    .Select(n => _reverseEngineers[n]);

            // Collect all records
            var collected = new List<(ModRecord record, string sourceModName)>();
            foreach (var modName in _loadOrder)
            {
                if (!_reverseEngineers.TryGetValue(modName, out var re))
                    continue;

                if (modNames != null && !modNames.Contains(modName))
                    continue;

                foreach (var record in re.GetRecordsByTypeINMUTABLE(recordType))
                    collected.Add((record, modName));
            }
            // Merge duplicates preferring "new" records
            var mergedNames = new List<string>();
            var mergedRecords = new List<ModRecord>();

            foreach (var group in collected.GroupBy(x => x.record.StringId))
            {
                var recList = group.ToList();
                var creatorPair = recList.FirstOrDefault(x => x.record.isNew());

                if (creatorPair != default)
                {
                    var merged = creatorPair.record.deepClone();
                    string creatorMod = creatorPair.sourceModName;

                    foreach (var (record, _) in recList)
                    {
                        if (!object.ReferenceEquals(record, merged))
                            merged.applyChangesFrom(record);
                    }

                    if (predicate == null || predicate(merged))
                    {
                        mergedRecords.Add(merged);
                        mergedNames.Add(creatorMod);

                        if (mergedRecords.Count >= maxRecords)
                            break;
                    }
                }
            }

            return (mergedNames, mergedRecords);
        }

        // Get evolution of a specific record across mods
        public string GetRecordEvolution(string stringId)
        {
            var sb = new StringBuilder();
            foreach (var modName in _loadOrder)
            {
                if (_reverseEngineers.TryGetValue(modName, out var re))
                {
                    var record = re.searchModRecordByStringId(stringId);
                    if (record != null)
                        sb.AppendLine($"{modName} => {record}");
                }
            }
            return sb.ToString();
        }
        public ReverseEngineer? GetCreatorReverseEngineer(string stringId)
        {
            foreach (var modName in _loadOrder)
            {
                if (_reverseEngineers.TryGetValue(modName, out var re))
                {
                    var record = re.searchModRecordByStringId(stringId);
                    if (record != null && record.isNew())
                        return re;
                }
            }
            return null;
        }
        public ReverseEngineer? GetLastModifierReverseEngineer(string stringId)
        {
            foreach (var modName in _loadOrder.AsEnumerable().Reverse())
            {
                if (_reverseEngineers.TryGetValue(modName, out var re))
                {
                    var record = re.searchModRecordByStringId(stringId);
                    if (record != null)
                        return re;
                }
            }
            return null;
        }

        // Clear all loaded ReverseEngineers
        public void Clear()
        {
            _reverseEngineers.Clear();
            _mergedByTypeAndId.Clear();
            _loadOrder.Clear();
        }
        public List<ReverseEngineer> ParseModSelector(string selector)
        {
            if (selector.Equals("all", StringComparison.Ordinal))
            {
                return _loadOrder
                    .Where(name => _reverseEngineers.ContainsKey(name))
                    .Select(name => _reverseEngineers[name])
                    .ToList();
            }
            bool isExclude = selector.StartsWith("*", StringComparison.Ordinal);
            selector = isExclude ? selector.Substring(1) : selector;
            var names = CoreUtils.SplitModList(selector).ToHashSet(StringComparer.Ordinal);
            var result = new List<ReverseEngineer>();

            foreach (var modName in _loadOrder)
            {
                if (!_reverseEngineers.TryGetValue(modName, out var re))
                    continue;

                if (names.Contains(modName) != isExclude)
                    result.Add(re);
            }

            CoreUtils.Print($"Parsed mod selector '{selector}' to {result.Count} mods.");
            return result;
        }
        
        public ModRecord? searchModRecordByStringIdGlobally(string id, bool getEarly)
        {
            ModRecord? result = null;
            int found_index = -1;
            for (int i = _loadOrder.Count - 1; i >= 0; i--)
            {
                string modName = _loadOrder[i];

                if (!_reverseEngineers.TryGetValue(modName, out var re))
                    continue;

                var record = re.searchModRecordByStringIdLocally(id);
                if (record == null)
                    continue;
                if (record.isNew())
                {
                    result = record.deepClone();
                    found_index= i;
                    if (getEarly)
                        return result;
                    break;
                }
            }
            if(result==null)
                return null;
            for (int i = found_index+1; i < _loadOrder.Count; i++)
            {
                string modName = _loadOrder[i];

                if (!_reverseEngineers.TryGetValue(modName, out var re))
                    continue;

                var record = re.searchModRecordByStringIdLocally(id); 
                if (record == null)
                    continue;
                result.applyChangesFrom(record);
            }
            return result;
        }
        /*public ModRecord? getModRecordIfDirty(string id)
        {
            ModRecord? result = null;
            bool dirty = false;
            foreach (var modName in _loadOrder)
            {
                if (!_reverseEngineers.TryGetValue(modName, out var re))
                    continue;

                var record = re.searchModRecordByStringIdLocally(id);
                if (record == null)
                    continue;

                if (record.isNew() && result == null)
                {
                    result = record.deepClone();
                    continue;
                }

                if (result != null)
                {
                    dirty = result.applyChangesCarefully(record);
                    if (dirty)
                    {
                        CoreUtils.Print($"Dirty Record {id} modified by {modName}");
                    }
                }
            }

            return dirty?result:null;
        }*/

        public string? FindLastModifierMod(string id,string field)
        {
            foreach (var kvp in _reverseEngineers.Reverse())
            {
                var record =
                    kvp.Value.searchModRecordByStringIdLocally(id);
                if (record == null)
                    continue;
                if (record.isFieldChanged(field,"filename"))
                    return kvp.Value.modname;
            }

            return null;
        }
        public void LoadFromMods(Dictionary<string, ModItem> mods, Func<ModItem, string?> pathSelector)
        {
            busy = true;
            Clear();
            ProgressController progress = ProgressController.Instance;
            progress.Initialize(mods.Count);
            int i = 0;
            foreach (var kv in mods)
            {
                string? path = pathSelector(kv.Value);
                if (string.IsNullOrEmpty(path))
                    continue;

                var re = new ReverseEngineer();
                try
                {
                    re.LoadModFile(path);

                }
                catch (UnsupportedModFileException ex)
                {
                    CoreUtils.Print($"Error loading mod file for {kv.Key} at {path}: {ex.Message}");
                    continue;
                        //re.LoadModFile(path);
                }
                AddOrUpdate(kv.Key, re);

                i++;
                progress.Report(i, $"Engineered mod {i}");
            }
            progress.Finish();
            busy = false;
        }
    }
}
