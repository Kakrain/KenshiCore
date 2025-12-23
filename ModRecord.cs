using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore
{
    public class ModRecord
    {
        public int InstanceCount { get; set; }
        public int RecordType { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string StringId { get; set; } = "";
        public int ChangeType { get; set; }

        private HashSet<string>? changed = null;
        private string sep = ":";

        public Dictionary<string, bool> BoolFields { get; set; } = new();
        public Dictionary<string, float> FloatFields { get; set; } = new();
        public Dictionary<string, int> LongFields { get; set; } = new();
        public Dictionary<string, float[]> Vec3Fields { get; set; } = new();
        public Dictionary<string, float[]> Vec4Fields { get; set; } = new();
        public Dictionary<string, string> StringFields { get; set; } = new();
        public Dictionary<string, string> FilenameFields { get; set; } = new();
        public Dictionary<string, Dictionary<string, int[]>> ExtraDataFields { get; set; } = new();
        public List<ModInstance> InstanceFields { get; set; } = new();

        public static readonly Dictionary<string, Func<ModRecord, string>> additionalGetters = new Dictionary<string, Func<ModRecord, string>>
        {
            { "_stringId_", r => r.StringId },
            { "_name_", r => r.Name }
        };
        public static readonly Dictionary<string, Action<ModRecord, object?>> additionalSetters =
        new()
        {
            { "_stringId_", (r, v) => r.StringId = Convert.ToString(v)! },
            { "_name_",     (r, v) => r.Name     = Convert.ToString(v)! }
        };
        public override string ToString()
        {
            return this.Name + " | " + this.StringId + " | " + this.getRecordType() + " | " + this.getChangeType();

        }
        public Dictionary<string, int[]>? GetExtraData(string category)
        {
            ExtraDataFields.TryGetValue(category, out var cat);
            return cat;
        }
        public void DeleteExtraData(string category, string key)
        {
            if (ExtraDataFields == null)
                ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            if (!ExtraDataFields.ContainsKey(category))
                ExtraDataFields.Add(category, new Dictionary<string, int[]>());
            if (ExtraDataFields[category].ContainsKey(key))
                ExtraDataFields[category].Remove(key);
            ExtraDataFields[category].Add(key, new int[] { ReverseEngineer.DELETED, ReverseEngineer.DELETED, ReverseEngineer.DELETED });
        }
        public bool EnsureFieldExist(ModRecord source, string field)
        {
            if (source.BoolFields.TryGetValue(field, out bool bVal) && !this.BoolFields.ContainsKey(field))
            {
                this.BoolFields[field] = bVal;
                return true;
            }
            else if (source.FloatFields.TryGetValue(field, out float fVal) && !this.FloatFields.ContainsKey(field))
            {
                this.FloatFields[field] = fVal;
                return true;
            }
            else if (source.LongFields.TryGetValue(field, out int lVal) && !this.LongFields.ContainsKey(field))
            {
                this.LongFields[field] = lVal;
                return true;
            }
            else if (source.StringFields.TryGetValue(field, out string? sVal) && !this.StringFields.ContainsKey(field))
            {
                this.StringFields[field] = sVal;
                return true;
            }
            else if (source.FilenameFields.TryGetValue(field, out string? fnVal) && !this.FilenameFields.ContainsKey(field))
            {
                this.FilenameFields[field] = fnVal;
                return true;
            }
            else if (source.Vec3Fields.TryGetValue(field, out var v3Val) && !this.Vec3Fields.ContainsKey(field))
            {
                this.Vec3Fields[field] = (float[])v3Val.Clone();
                return true;
            }
            else if (source.Vec4Fields.TryGetValue(field, out var v4Val) && !this.Vec4Fields.ContainsKey(field))
            {
                this.Vec4Fields[field] = (float[])v4Val.Clone();
                return true;
            }
            return false;
        }
        public void ForceEnsureFieldExist(string field, string type)
        {
            switch (type)
            {
                case "bool":
                    this.BoolFields[field] = true;
                    break;
                case "float":
                    this.FloatFields[field] = 0.0f;
                    break;
                case "int":
                    this.LongFields[field] = 0;
                    break;
                case "string":
                    this.StringFields[field] = "";
                    break;
                case "filename":
                    this.FilenameFields[field] = "";
                    break;
                case "vec3field":
                    this.Vec3Fields[field] = new float[] { 0.0f, 0.0f, 0.0f };
                    break;
                case "vec4field":
                    this.Vec4Fields[field] = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
                    break;
                default:
                    throw new ArgumentException($"Unknown record type: {type} available types are: bool,float,int,string,filename,vec3field and vec4field");
            }
        }
        public IEnumerable<string> GetAllFieldNames()
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);

            if (BoolFields != null)
                foreach (var kv in BoolFields)
                    fields.Add(kv.Key);

            if (FloatFields != null)
                foreach (var kv in FloatFields)
                    fields.Add(kv.Key);

            if (LongFields != null)
                foreach (var kv in LongFields)
                    fields.Add(kv.Key);

            if (StringFields != null)
                foreach (var kv in StringFields)
                    fields.Add(kv.Key);

            if (FilenameFields != null)
                foreach (var kv in FilenameFields)
                    fields.Add(kv.Key);
            return fields;
        }
        public ModRecord deepClone()
        {
            var copy = new ModRecord
            {
                StringId = this.StringId,
                InstanceCount = this.InstanceCount,
                RecordType = this.RecordType,
                Id = this.Id,
                Name = this.Name,
                ChangeType = this.ChangeType
            };
            copy.BoolFields = this.BoolFields != null
                ? new Dictionary<string, bool>(this.BoolFields)
                : new Dictionary<string, bool>();
            copy.FloatFields = this.FloatFields != null
                ? new Dictionary<string, float>(this.FloatFields)
                : new Dictionary<string, float>();
            copy.LongFields = this.LongFields != null
                ? new Dictionary<string, int>(this.LongFields)
                : new Dictionary<string, int>();
            copy.StringFields = this.StringFields != null
                ? this.StringFields.ToDictionary(kv => kv.Key, kv => kv.Value)
                : new Dictionary<string, string>();
            copy.FilenameFields = this.FilenameFields != null
                ? this.FilenameFields.ToDictionary(kv => kv.Key, kv => kv.Value)
                : new Dictionary<string, string>();
            copy.Vec3Fields = this.Vec3Fields != null
                ? this.Vec3Fields.ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone())
                : new Dictionary<string, float[]>();
            copy.Vec4Fields = this.Vec4Fields != null
                ? this.Vec4Fields.ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone())
                : new Dictionary<string, float[]>();

            copy.ExtraDataFields = this.ExtraDataFields != null
                ? this.ExtraDataFields.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value != null
                        ? kv.Value.ToDictionary(kv2 => kv2.Key, kv2 => (int[])kv2.Value.Clone())
                        : new Dictionary<string, int[]>()
                  )
                : new Dictionary<string, Dictionary<string, int[]>>();

            copy.InstanceFields = this.InstanceFields != null
                ? this.InstanceFields.Select(inst => new ModInstance
                {
                    Id = inst.Id,
                    Target = inst.Target,
                    Tx = inst.Tx,
                    Ty = inst.Ty,
                    Tz = inst.Tz,
                    Rw = inst.Rw,
                    Rx = inst.Rx,
                    Ry = inst.Ry,
                    Rz = inst.Rz,
                    StateCount = inst.StateCount,
                    States = inst.States != null ? new List<string>(inst.States) : new List<string>()
                }).ToList()
                : new List<ModInstance>();
            return copy;
        }

        public bool isTheSameRecord(ModRecord other)
        {
            return this.StringId == other.StringId;
        }
        public int GetRecordCompleteness()
        {
            if (this == null) return 0;
            int count = 0;
            // Count number of populated fields across the collections you care about
            if (this.BoolFields != null) count += this.BoolFields.Count;
            if (this.FloatFields != null) count += this.FloatFields.Count;
            if (this.LongFields != null) count += this.LongFields.Count;
            if (this.Vec3Fields != null) count += this.Vec3Fields.Count;
            if (this.Vec4Fields != null) count += this.Vec4Fields.Count;
            if (this.StringFields != null) count += this.StringFields.Count;
            if (this.FilenameFields != null) count += this.FilenameFields.Count;
            if (this.ExtraDataFields != null) count += this.ExtraDataFields.Sum(kv => kv.Value?.Count() ?? 0);
            if (this.InstanceFields != null) count += this.InstanceFields.Count;
            return count;
        }
        public void applyChangesFrom(ModRecord other)
        {
            foreach (var kv in other.BoolFields)
                this.BoolFields[kv.Key] = kv.Value;
            foreach (var kv in other.FloatFields)
                this.FloatFields[kv.Key] = kv.Value;
            foreach (var kv in other.LongFields)
                this.LongFields[kv.Key] = kv.Value;
            foreach (var kv in other.Vec3Fields)
                this.Vec3Fields[kv.Key] = (float[])kv.Value.Clone();
            foreach (var kv in other.Vec4Fields)
                this.Vec4Fields[kv.Key] = (float[])kv.Value.Clone();
            foreach (var kv in other.StringFields)
                this.StringFields[kv.Key] = kv.Value;
            foreach (var kv in other.FilenameFields)
                this.FilenameFields[kv.Key] = kv.Value;

            foreach (var kv in other.ExtraDataFields)
            {
                if (!this.ExtraDataFields.ContainsKey(kv.Key))
                    this.ExtraDataFields[kv.Key] = new Dictionary<string, int[]>();

                foreach (var itemKv in kv.Value)
                {
                    this.ExtraDataFields[kv.Key][itemKv.Key] = (int[])itemKv.Value.Clone();
                }
            }
        }
        private void getChangedSpecificFields<TValue>(Dictionary<string, TValue>? fields, string name)
        {
            if (fields == null)
                return;
            foreach (var f in fields)
            {
                changed!.Add(name + sep + f.Key);
            }
        }
        public bool isExtraDataOfThis(ModRecord other, string? category = null, int[]? variables = null)
        {
            IEnumerable<Dictionary<string, int[]>> dicts;

            if (category == null)
                dicts = ExtraDataFields.Values;
            else if (ExtraDataFields.TryGetValue(category, out var cat))
                dicts = new[] { cat };
            else
                return false;
            foreach (var d in dicts)
            {
                if (!d.TryGetValue(other.StringId, out var storedVars))
                    continue;

                if (variables == null || storedVars.SequenceEqual(variables))
                    return true;
            }

            return false;
        }
        public bool hasThisAsExtraData(ModRecord other, string? category = null, int[]? variables = null)
        {
            IEnumerable<Dictionary<string, int[]>> dicts;

            if (category == null)
                dicts = other.ExtraDataFields.Values;
            else if (other.ExtraDataFields.TryGetValue(category, out var cat))
                dicts = new[] { cat };
            else
                return false;

            foreach (var d in dicts)
            {
                if (!d.TryGetValue(this.StringId, out var storedVars))
                    continue;

                if (variables == null || storedVars.SequenceEqual(variables))
                    return true;
            }

            return false;
        }

        public HashSet<string> getChangedFields()
        {
            if (changed != null)
            {
                return changed;
            }
            changed = new HashSet<string>();


            getChangedSpecificFields(this.BoolFields, "bool");
            getChangedSpecificFields(this.FloatFields, "float");
            getChangedSpecificFields(this.LongFields, "long");
            getChangedSpecificFields(this.Vec3Fields, "vec3");
            getChangedSpecificFields(this.Vec4Fields, "vec4");
            getChangedSpecificFields(this.StringFields, "string");
            getChangedSpecificFields(this.FilenameFields, "filename");
            getChangedSpecificFields(this.ExtraDataFields, "extradata");

            return changed;
        }
        public static readonly Dictionary<int, string> ModTypeCodes = new Dictionary<int, string>
        {
            { 0, "BUILDING" },{ 1, "CHARACTER" },{ 2, "WEAPON" },{ 3, "ARMOUR" },{ 4, "ITEM" },
            { 5, "ANIMAL_ANIMATION" },{ 6, "ATTACHMENT" },{ 7, "RACE" },{ 9, "NATURE" },{ 10, "FACTION" },{ 12, "ZONE_MAP" },
            { 13, "TOWN" },{ 16, "LOCATIONAL_DAMAGE" },{ 17, "COMBAT_TECHNIQUE" },{ 18, "DIALOGUE" },{ 19, "DIALOGUE_LINE" },
            { 21, "RESEARCH" },{ 22, "AI_TASK" },{ 24, "ANIMATION" },{ 25, "STATS" },{ 26, "PERSONALITY" },
            { 27, "CONSTANTS" },{ 28, "BIOMES" },{ 29, "BUILDING_PART" },{ 30, "INSTANCE_COLLECTION" },{ 31, "DIALOG_ACTION" },
            { 34, "PLATOON" },{ 36, "GAMESTATE_CHARACTER" },{ 37, "GAMESTATE_FACTION" },{ 38, "GAMESTATE_TOWN_INSTANCE_LIST" },{ 41, "INVENTORY_STATE" },
            { 42, "INVENTORY_ITEM_STATE" },{ 43, "REPEATABLE_BUILDING_PART_SLOT" },{ 44, "MATERIAL_SPEC" },{ 45, "MATERIAL_SPECS_COLLECTION" },{ 46, "CONTAINER" },
            { 47, "MATERIAL_SPECS_CLOTHING" },{ 49, "VENDOR_LIST" },{ 50, "MATERIAL_SPECS_WEAPON" },{ 51, "WEAPON_MANUFACTURER" },{ 52, "SQUAD_TEMPLATE" },
            { 53, "ROAD" },{ 55, "COLOR_DATA" },{ 56, "CAMERA" },{ 57, "MEDICAL_STATE" },{ 59, "FOLIAGE_LAYER" },
            { 60, "FOLIAGE_MESH" },{ 61, "GRASS" },{ 62, "BUILDING_FUNCTIONALITY" },{ 63, "DAY_SCHEDULE" },{ 64, "NEW_GAME_STARTOFF" },
            { 66, "CHARACTER_APPEARANCE" },{ 67, "GAMESTATE_AI" },{ 68, "WILDLIFE_BIRDS" },{ 69, "MAP_FEATURES" },{ 70, "DIPLOMATIC_ASSAULTS" },
            { 71, "SINGLE_DIPLOMATIC_ASSAULT" },{ 72, "AI_PACKAGE" },{ 73, "DIALOGUE_PACKAGE" },{ 74, "GUN_DATA" },{ 76, "ANIMAL_CHARACTER" },
            { 77, "UNIQUE_SQUAD_TEMPLATE" },{ 78, "FACTION_TEMPLATE" },{ 80, "WEATHER" },{ 81, "SEASON" },{ 82, "EFFECT" },
            { 83, "ITEM_PLACEMENT_GROUP" },{ 84, "WORD_SWAPS" },{ 86, "NEST_ITEM" },{ 87, "CHARACTER_PHYSICS_ATTACHMENT" },{ 88, "LIGHT" },
            { 89, "HEAD" },{ 92, "FOLIAGE_BUILDING" },{ 93, "FACTION_CAMPAIGN" },{ 94, "GAMESTATE_TOWN" },{ 95, "BIOME_GROUP" },
            { 96, "EFFECT_FOG_VOLUME" },{ 97, "FARM_DATA" },{ 98, "FARM_PART" },{ 99, "ENVIRONMENT_RESOURCES" },{ 100, "RACE_GROUP" },
            { 101, "ARTIFACTS" },{ 102, "MAP_ITEM" },{ 103, "BUILDINGS_SWAP" },{ 104, "ITEMS_CULTURE" },{ 105, "ANIMATION_EVENT" },
            { 107, "CROSSBOW" },{ 109, "AMBIENT_SOUND" },{ 110, "WORLD_EVENT_STATE" },{ 111, "LIMB_REPLACEMENT" },{112,"ANIMATION_FILE"}
        };
        public static readonly Dictionary<string, int> ModTypeNames = ModTypeCodes.ToDictionary(kv => kv.Value, kv => kv.Key);
        public string getRecordType()
        {
            return ModTypeCodes.GetValueOrDefault(this.RecordType, $"UNKNOWN:{this.RecordType.ToString()}");
        }
        public List<(string, Color)> getNameOnlyAsBlock()
        {
            var blocks = new List<(string, Color)>();
            blocks.Add(($"--- RECORD: {this.Name} ({this.getRecordType()}) ---", Color.Orange));
            blocks.Add(($"ID: {this.Id}, StringID: {this.StringId}, ChangeType: {this.getChangeType()}", Color.Gray));
            return blocks;
        }
        public List<(string, Color)> getDataAsBlock(List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();
            // Record header
            blocks.Add(($"--- RECORD: {this.Name} ({this.getRecordType()}) ---", Color.Orange));
            blocks.Add(($"ID: {this.Id}, StringID: {this.StringId}, ChangeType: {this.getChangeType()}", Color.Gray));

            bool filterActive = fieldFilter != null && fieldFilter.Count > 0;

            bool ShouldInclude(string fieldName) =>
            !filterActive || fieldFilter!.Any(f => f.Equals(fieldName, StringComparison.Ordinal));

            // Basic fields
            foreach (var kv in this.BoolFields)
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Bool: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in this.FloatFields)
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Float: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in this.LongFields)
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Long: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in this.StringFields)
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"String: {kv.Key} = {kv.Value}", Color.LightYellow));

            foreach (var kv in this.FilenameFields)
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Filename: {kv.Key} = {kv.Value}", Color.LightPink));

            if (fieldFilter != null)
                return blocks;
            // ExtraData
            if (this.ExtraDataFields != null)
            {
                foreach (var cat in this.ExtraDataFields)
                {
                    blocks.Add(($"ExtraData Category: {cat.Key}", Color.LightSalmon));
                    foreach (var item in cat.Value)
                        blocks.Add(($"  {item.Key} = [{string.Join(",", item.Value)}]", Color.LightSalmon));
                }
            }

            // Instances
            if (this.InstanceFields != null)
            {
                foreach (var inst in this.InstanceFields)
                {
                    blocks.Add(($"Instance: Id={inst.Id}, Target={inst.Target}, Pos=({inst.Tx},{inst.Ty},{inst.Tz}), Rot=({inst.Rx},{inst.Ry},{inst.Rz},{inst.Rw})", Color.LightGray));
                    if (inst.States != null && inst.States.Count > 0)
                        blocks.Add(($"  States: {string.Join(",", inst.States)}", Color.LightGray));
                }
            }
            return blocks;
        }
        public string getDataAsString(List<string>? fieldFilter = null)
        {
            StringBuilder sb = new StringBuilder();
            // Record header
            sb.AppendLine($"--- RECORD: {this.Name} ({this.getRecordType()}) ---");
            sb.AppendLine($"ID: {this.Id}, StringID: {this.StringId}, ChangeType: {this.getChangeType()}");

            bool filterActive = fieldFilter != null && fieldFilter.Count > 0;

            bool ShouldInclude(string fieldName) =>
            !filterActive || fieldFilter!.Any(f => f.Equals(fieldName, StringComparison.Ordinal));

            // Basic fields
            foreach (var kv in this.BoolFields)
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Bool: {kv.Key} = {kv.Value}");

            foreach (var kv in this.FloatFields)
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Float: {kv.Key} = {kv.Value}");

            foreach (var kv in this.LongFields)
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Long: {kv.Key} = {kv.Value}");

            foreach (var kv in this.StringFields)
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"String: {kv.Key} = {kv.Value}");

            foreach (var kv in this.FilenameFields)
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Filename: {kv.Key} = {kv.Value}");

            if (fieldFilter != null)
                return sb.ToString();
            // ExtraData
            if (this.ExtraDataFields != null)
            {
                foreach (var cat in this.ExtraDataFields)
                {
                    sb.AppendLine($"ExtraData Category: {cat.Key}");
                    foreach (var item in cat.Value)
                        sb.AppendLine($"  {item.Key} = [{string.Join(",", item.Value)}]");
                }
            }

            // Instances
            if (this.InstanceFields != null)
            {
                foreach (var inst in this.InstanceFields)
                {
                    sb.AppendLine($"Instance: Id={inst.Id}, Target={inst.Target}, Pos=({inst.Tx},{inst.Ty},{inst.Tz}), Rot=({inst.Rx},{inst.Ry},{inst.Rz},{inst.Rw})");
                    if (inst.States != null && inst.States.Count > 0)
                        sb.AppendLine($"  States: {string.Join(",", inst.States)}");
                }
            }
            return sb.ToString();
        }
        public bool isNew()
        {
            return (ChangeType & 1) == 0;
        }
        public string getChangeType()
        {
            // Convert ModDataType to 32-bit binary string
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');

            // First 4 groups (first 20 bits) in groups of 4
            string first3Groups = string.Join(" | ", Enumerable.Range(0, 3)
                .Select(i => binary.Substring(i * 4, 4)));

            // Groups 6 + 7 (bits 20–27) = ChangeCounter
            string changeCounterBits = binary.Substring(12, 16);
            int changeCounter = Convert.ToInt32(changeCounterBits, 2);

            // Group 8 (bits 28–31) = NewRecordFlag (keep as bits)
            string newRecordFlagBits = binary.Substring(28, 4);
            bool isExistingRecord = newRecordFlagBits[3] == '1'; // last bit = 1 if existing

            string? unknownBits = null;
            unknownBits = binary.Substring(0, 12);
            // Format result
            string result = $"{(unknownBits == null ? "" : ($"Unknown Bits:{unknownBits}|"))} Change Counter: {changeCounter} | {newRecordFlagBits} ({(isExistingRecord ? "Existing" : "New")})";

            if (newRecordFlagBits == "0011")
                result += " (Name Changed)";

            // Append REMOVED if applicable
            if (this.BoolFields.TryGetValue("REMOVED", out var value) && value)
                result += " REMOVED";

            return result;
        }
        public void SetChangeCounter(int newValue)
        {
            // Clamp between 0–65535 (16 bits)
            newValue = Math.Clamp(newValue, 0, 65535);

            // Convert ModDataType to binary string
            char[] binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0').ToCharArray();

            // Replace bits 12–27 (the 16-bit change counter)
            string newBits = Convert.ToString(newValue, 2).PadLeft(16, '0');
            for (int i = 0; i < 16; i++)
                binary[12 + i] = newBits[i];

            // Convert back to int
            ChangeType = Convert.ToInt32(new string(binary), 2);
        }
        public void AddToChangeCounter(int delta)
        {
            // Extract current counter
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');
            string changeCounterBits = binary.Substring(12, 16);
            int current = Convert.ToInt32(changeCounterBits, 2);

            // Add and clamp to 0–65535
            int newValue = Math.Clamp(current + delta, 0, 65535);

            // Reuse the SetChangeCounter logic
            SetChangeCounter(newValue);
        }
        public void SetRecordStatus(int fileType, string status)
        {
            // Convert to 32-bit binary string
            char[] binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0').ToCharArray();

            // Extract the last 4 bits (bits 28–31)
            string lastGroup = new string(binary[28..32]);

            string newLastGroup = lastGroup; // default keep existing

            switch (status.ToLower())
            {
                case "existing":
                    newLastGroup = "0001";
                    break;

                case "new":
                    newLastGroup = fileType == 16 ? "0010" : "0000";
                    break;

                case "namechanged":
                    // Can only apply if NOT new
                    bool isCurrentlyNew =
                        (fileType == 16 && lastGroup == "0010") ||
                        (fileType == 17 && lastGroup == "0000");

                    if (!isCurrentlyNew)
                        newLastGroup = "0011";
                    break;

                default:
                    throw new ArgumentException($"Unknown record status: {status}");
            }

            // Replace last 4 bits
            for (int i = 0; i < 4; i++)
                binary[28 + i] = newLastGroup[i];

            // Convert back to int
            ChangeType = Convert.ToInt32(new string(binary), 2);
        }
        public string GetModName()
        {
            if (string.IsNullOrEmpty(StringId))
                return string.Empty;

            // Expected format: "number-modname.mod"
            int dashIndex = StringId.IndexOf('-');
            if (dashIndex == -1 || dashIndex >= StringId.Length - 1)
                return string.Empty;

            string modPart = StringId.Substring(dashIndex + 1);

            // Ensure it ends with ".mod"
            if (modPart.EndsWith(".mod", StringComparison.Ordinal))
                return modPart;

            return string.Empty;
        }
        public bool ValidateDataTypeAssumptions()
        {
            return ModTypeCodes.ContainsKey(this.RecordType);

        }
        public bool ValidateChangeTypeAssumptions(int fileType)
        {
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');

            if (fileType == 16)
            {
                string firstGroup = binary.Substring(0, 4);
                string lastGroup = binary.Substring(28, 4);

                bool firstOk = firstGroup == "1000";
                bool lastOk = lastGroup == "0001" || lastGroup == "0010" || lastGroup == "0011";

                return firstOk && lastOk;
            }
            else if (fileType == 17)
            {
                string first3Groups = binary.Substring(0, 12);
                string lastGroup = binary.Substring(28, 4);

                bool firstOk = first3Groups.All(c => c == '0');
                bool lastOk = lastGroup == "0000" || lastGroup == "0001" || lastGroup == "0011";

                return firstOk && lastOk;
            }
            return false;
        }

        public bool HasField(string field)
        {
            return BoolFields.ContainsKey(field) || FloatFields.ContainsKey(field) ||
                   LongFields.ContainsKey(field) || Vec3Fields.ContainsKey(field) ||
                   Vec4Fields.ContainsKey(field) || StringFields.ContainsKey(field) ||
                   FilenameFields.ContainsKey(field);
        }
        private bool TrySetVector(Dictionary<string, float[]> dict, string key, string value, int length)
        {
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != length) return false;

            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (!float.TryParse(parts[i], out result[i]))
                    return false;
            }
            dict[key] = result;
            return true;
        }
        private void TrySet<T>(Dictionary<string, T> dict, string key, string value, TryParseHandler<T> parser)
        {
            if (parser(value, out T result))
            {
                dict[key] = result;
                return;
            }
            throw new FormatException($"invalid value for field: {key}={value} on record {this.Name} ({this.StringId})");
        }
        public object? GetFieldAsObject(string field)
        {
            additionalGetters.TryGetValue(field, out var fieldfunc);
            if (fieldfunc != null)
                return fieldfunc(this);
            if (this.FloatFields.TryGetValue(field, out float f)) return f;
            if (this.LongFields.TryGetValue(field, out int l)) return l;
            if (this.BoolFields.TryGetValue(field, out bool b)) return b;
            if (this.StringFields.TryGetValue(field, out string? s)) return s;
            if (this.FilenameFields.TryGetValue(field, out string? fn)) return fn;
            if (this.Vec3Fields.TryGetValue(field, out var v3)) return v3.Length > 0 ? v3[0] : 0f;
            if (this.Vec4Fields.TryGetValue(field, out var v4)) return v4.Length > 0 ? v4[0] : 0f;

            return null;
        }
        public void SetField(string field, string value)
        {

            if (additionalSetters.TryGetValue(field, out var setter))
            {
                setter(this, value);
                return;
            }

            if (BoolFields.ContainsKey(field))
            {
                TrySet(BoolFields, field, value, bool.TryParse);
                return;
            }
            if (FloatFields.ContainsKey(field))
            {
                TrySet(FloatFields, field, value.Replace(".", ","), float.TryParse);
                return;
            }
            if (LongFields.ContainsKey(field))
            {
                TrySet(LongFields, field, value, int.TryParse);
                return;
            }
            if (Vec3Fields.ContainsKey(field))
            {
                TrySetVector(Vec3Fields, field, value, 3);
                return;
            }
            if (Vec4Fields.ContainsKey(field))
            {
                TrySetVector(Vec4Fields, field, value, 4);
                return;
            }
            if (StringFields.ContainsKey(field))
            {
                StringFields[field] = value;
                return;
            }
            if (FilenameFields.ContainsKey(field))
            {
                FilenameFields[field] = value;
                return;
            }
            throw new FormatException($"field not found: {field}={value} on record {this.Name} ({this.StringId})");
        }
        private delegate bool TryParseHandler<T>(string s, out T result);
        public string getStringId()
        {
            return this.StringId;
        }
        //TODO
        /*public bool isPathBroken(string basePath)
        {

        }*/
        public string? GetFieldAsString(string field)
        {
            additionalGetters.TryGetValue(field, out var fieldfunc);
            if (fieldfunc != null)
                return fieldfunc(this);
            if (BoolFields.ContainsKey(field))
                return BoolFields.GetValueOrDefault(field).ToString();
            if (FloatFields.ContainsKey(field))
                return FloatFields.GetValueOrDefault(field).ToString();
            if (LongFields.ContainsKey(field))
                return LongFields.GetValueOrDefault(field).ToString();
            if (Vec3Fields.ContainsKey(field))
                return Vec3Fields.GetValueOrDefault(field)!.ToString();
            if (Vec4Fields.ContainsKey(field))
                return Vec4Fields.GetValueOrDefault(field)!.ToString();
            if (StringFields.ContainsKey(field))
                return StringFields.GetValueOrDefault(field)!.ToString();
            if (FilenameFields.ContainsKey(field))
                return FilenameFields.GetValueOrDefault(field)!.ToString();
            return null;
        }
    }
    public class ModInstance
    {
        public string? Id { get; set; }
        public string? Target { get; set; }
        public float Tx { get; set; }
        public float Ty { get; set; }
        public float Tz { get; set; }
        public float Rw { get; set; }
        public float Rx { get; set; }
        public float Ry { get; set; }
        public float Rz { get; set; }
        public int StateCount { get; set; }
        public List<string>? States { get; set; }
    }
}
