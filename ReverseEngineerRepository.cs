using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore
{
    public class ReverseEngineerRepository
    {

        private readonly Dictionary<string, Dictionary<string, ModRecord>>_mergedByTypeAndId = new(StringComparer.Ordinal);
        private static ReverseEngineerRepository? _instance;

        private static readonly HashSet<string> _ignoredModNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "-KenshiFixer_Fix-.mod"
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

        public bool HasMergedRecord(string recordType, string stringId)
        {
            GetAllRecordsMerged(recordType); // ensure cache populated
            return _mergedByTypeAndId[recordType].ContainsKey(stringId);
        }
        // Dictionary keyed by mod name
        private readonly Dictionary<string, ReverseEngineer> _reverseEngineers = new();

        private ReverseEngineerRepository() { }

        // Add or replace a ReverseEngineer
        public bool AddOrUpdate(string modName, ReverseEngineer re)
        {
            if (_ignoredModNames.Contains(modName))
                return false; // silently ignore

            _reverseEngineers[modName] = re;
            return true;
        }
        public Dictionary<string, ReverseEngineer>  getCache()
        {
            return _reverseEngineers;
        }

        // Remove a ReverseEngineer by name
        public bool Remove(string modName)
        {
            return _reverseEngineers.Remove(modName);
        }

        // Get all ReverseEngineers
        public IReadOnlyCollection<ReverseEngineer> GetAll() => _reverseEngineers.Values;

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
            foreach (var re in selected)
            {
                foreach (var record in re.GetRecordsByTypeINMUTABLE(recordType))
                    collected.Add((record, re.modname));
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
            foreach (var kvp in _reverseEngineers)
            {
                var record = kvp.Value.searchModRecordByStringId(stringId);
                if (record != null)
                    sb.AppendLine($"{kvp.Key} => {record}");
            }
            return sb.ToString();
        }

        // Clear all loaded ReverseEngineers
        public void Clear()
        {
            _reverseEngineers.Clear();
        }
        public List<string> GetAssumedRequiredRecords()
        {
            List<string> baseMods = new List<string> { "gamedata.base", "rebirth.mod", "Newwworld.mod", "Dialogue.mod" };
            var assumedReqs = new List<string>();

            foreach (var modName in baseMods)
            {
                if (TryGet(modName, out var re))
                {
                    assumedReqs.AddRange(re!.GetModsNewRecords());
                }
            }

            // Deduplicate (case-sensitive)
            return assumedReqs.Distinct(StringComparer.Ordinal).ToList();
        }
        public List<ReverseEngineer> ParseModSelector(string selector)
        {
            if (selector.Equals("all", StringComparison.Ordinal))
                return _reverseEngineers.Values.ToList();

            bool isExclude = selector.StartsWith("*", StringComparison.Ordinal);
            selector = isExclude ? selector.Substring(1) : selector;

            var names = CoreUtils.SplitModList(selector)
                                 .ToHashSet(StringComparer.Ordinal);

            var result = new List<ReverseEngineer>();

            foreach (var kvp in _reverseEngineers)
            {
                string modName = kvp.Key;
                ReverseEngineer re = kvp.Value;

                if (names.Contains(modName) != isExclude)
                    result.Add(re);
            }

            CoreUtils.Print($"Parsed mod selector '{selector}' to {result.Count} mods.");
            return result;
        }
    }
}
