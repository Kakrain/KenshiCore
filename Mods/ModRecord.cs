using KenshiCore.ReverseEngineering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Mods
{
    public class ModRecord
    {
        public int UnknownValue { get; set; }
        public int RecordType { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string StringId { get; set; } = "";
        public int ChangeType { get; set; }
        private const string sep = ":";

        public Dictionary<string, bool>? BoolFields = null;//new(); { get; set; }
        public Dictionary<string, float>? FloatFields = null;//new(); { get; set; }
        public Dictionary<string, int>? LongFields = null;//new(); { get; set; }
        public Dictionary<string, float[]>? Vec3Fields  = null;//new();{ get; set; }
        public Dictionary<string, float[]>? Vec4Fields= null;//new(); { get; set; } 
        public Dictionary<string, string>? StringFields = null;//new(); { get; set; }
        public Dictionary<string, string>? FilenameFields = null;//new(); { get; set; }
        public Dictionary<string, Dictionary<string, int[]>>? ExtraDataFields  = null;//new();{ get; set; }
        public List<ModInstance>? InstanceFields = null;//new();{ get; set; } 

        public static readonly Dictionary<string, Func<ModRecord, string>> additionalGetters = new Dictionary<string, Func<ModRecord, string>>
        {
            { "_stringId_", r => r.StringId },
            { "_name_", r => r.Name },
            { "text_", r => 
                {
                    StringBuilder sb = new();
                    for (int i = 0; ; i++)
                    {
                        string field = "text" + i;
                        string? textValue = r.GetFieldAsString(field);
                        if (textValue == null || textValue  == "")
                            break;
                        if (i > 0)
                            sb.Append('|');
                        sb.Append(textValue);
                    }
                    return sb.ToString();
                }
            },
            { "_first_creator_mod_", r =>
                {
                    ReverseEngineerRepository RER=ReverseEngineerRepository.Instance;
                    ReverseEngineer? creator = RER.GetCreatorReverseEngineer(r.StringId);
                    if(creator == null)
                        return "NOT FOUND";
                    else
                        return creator.modname;
                }
            },
            { "_last_changer_mod_", r =>
                {
                    ReverseEngineerRepository RER=ReverseEngineerRepository.Instance;
                    ReverseEngineer? changer = RER.GetLastModifierReverseEngineer(r.StringId);
                    if(changer == null)
                        return "NOT FOUND";
                    else
                        return changer.modname;
                }
            }

        };
        public static readonly Dictionary<string, Action<ModRecord, object?>> additionalSetters =
        new()
        {
            { "_stringId_", (r, v) => r.StringId = Convert.ToString(v)! },
            { "_name_",     (r, v) => r.Name     = Convert.ToString(v)! },
            { "text_",     (r, v) => 
                {
                    string textes=Convert.ToString(v)!;
                    var textes_list=textes.Split('|');

                    StringBuilder sb = new();
                    for (int i = 0; ; i++)
                    {
                        string field = "text" + i;
                        if(textes_list.Length > i)
                        {
                            r.ForceEnsureFieldExist(field, "string");
                            r.SetField(field,textes_list[i]);
                        }else if(r.fieldExist(field,"string")){
                            r.SetField(field,"");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        };
        public override string ToString()
        {
            return this.Name + " | " + this.StringId + " | " + this.getRecordType() + " | " + this.getChangeType();
        }
        public Dictionary<string, int[]>? GetExtraData(string? category)
        {
            if (ExtraDataFields==null || ExtraDataFields.Count == 0)
                return null;
            if (category != null)
            {
                ExtraDataFields.TryGetValue(category, out var cat);
                return cat;
            }
            // merge all categories
            var merged = new Dictionary<string, int[]>();

            foreach (var kvp in ExtraDataFields)
            {
                foreach (var entry in kvp.Value)
                {
                    merged[entry.Key] = entry.Value;
                }
            }

            return merged;
        }
        public bool ExtraDataExists(string category, string key)
        {
            if (ExtraDataFields != null && ExtraDataFields.ContainsKey(category) && ExtraDataFields[category].ContainsKey(key))
                return !IsDeleted(ExtraDataFields[category][key]);
            return false;
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
            if (source.BoolFields!=null&&source.BoolFields.TryGetValue(field, out bool bVal) && (this.BoolFields==null||!this.BoolFields.ContainsKey(field)))
            {
                if(this.BoolFields == null)
                    this.BoolFields = new Dictionary<string, bool>();
                this.BoolFields[field] = bVal;
                return true;
            }
            else if (source.FloatFields!=null&&source.FloatFields.TryGetValue(field, out float fVal) && (this.FloatFields==null||!this.FloatFields.ContainsKey(field)))
            {
                if(this.FloatFields == null)
                    this.FloatFields = new Dictionary<string, float>();
                this.FloatFields[field] = fVal;
                return true;
            }
            else if (source.LongFields!=null&&source.LongFields.TryGetValue(field, out int lVal) && (this.LongFields==null||!this.LongFields.ContainsKey(field)))
            {
                if(this.LongFields == null)
                    this.LongFields = new Dictionary<string, int>();
                this.LongFields[field] = lVal;
                return true;
            }
            else if (source.StringFields!=null&&source.StringFields.TryGetValue(field, out string? sVal) && (this.StringFields==null||!this.StringFields.ContainsKey(field)))
            {
                if(this.StringFields == null)
                    this.StringFields = new Dictionary<string, string>();
                this.StringFields[field] = sVal;
                return true;
            }
            else if (source.FilenameFields!=null&&source.FilenameFields.TryGetValue(field, out string? fnVal) && (this.FilenameFields==null||!this.FilenameFields.ContainsKey(field)))
            {
                if(this.FilenameFields == null)
                    this.FilenameFields = new Dictionary<string, string>();
                this.FilenameFields[field] = fnVal;
                return true;
            }
            else if (source.Vec3Fields!=null&&source.Vec3Fields.TryGetValue(field, out var v3Val) && (this.Vec3Fields==null||!this.Vec3Fields.ContainsKey(field)))
            {
                if(this.Vec3Fields == null)
                    this.Vec3Fields = new Dictionary<string, float[]>();
                this.Vec3Fields[field] = (float[])v3Val.Clone();
                return true;
            }
            else if (source.Vec4Fields!=null&&source.Vec4Fields.TryGetValue(field, out var v4Val) && (this.Vec4Fields==null||!this.Vec4Fields.ContainsKey(field)))
            {
                if(this.Vec4Fields == null)
                    this.Vec4Fields = new Dictionary<string, float[]>();
                this.Vec4Fields[field] = (float[])v4Val.Clone();
                return true;
            }
            return false;
        }
        public void DeleteField(string field)
        {
            if (this.BoolFields!=null&&this.BoolFields.ContainsKey(field))
            {
                this.BoolFields.Remove(field);
                return;
            }
            else if (this.FloatFields!=null&&this.FloatFields.ContainsKey(field))
            {
                this.FloatFields.Remove(field);
                return;
            }
            else if (this.LongFields!=null&&this.LongFields.ContainsKey(field))
            {
                this.LongFields.Remove(field);
                return;
            }
            else if (this.StringFields!=null&&this.StringFields.ContainsKey(field))
            {
                this.StringFields.Remove(field);
                return;
            }
            else if (this.FilenameFields!=null&&this.FilenameFields.ContainsKey(field))
            {
                this.FilenameFields.Remove(field);
                return;
            }
            else if (this.Vec3Fields!=null&&this.Vec3Fields.ContainsKey(field))
            {
                this.Vec3Fields.Remove(field);
                return;
            }
            else if (this.Vec4Fields!=null&&this.Vec4Fields.ContainsKey(field))
            {
                this.Vec4Fields.Remove(field);
                return;
            }
        }
        public void ForceEnsureFieldExist(string field, string type)
        {
            switch (type)
            {
                case "bool":
                    if (this.BoolFields == null)
                        this.BoolFields = new Dictionary<string, bool>();
                    this.BoolFields[field] = true;
                    break;
                case "float":
                    if (this.FloatFields == null)
                        this.FloatFields = new Dictionary<string, float>();
                    this.FloatFields[field] = 0.0f;
                    break;
                case "int":
                    if (this.LongFields == null)
                        this.LongFields = new Dictionary<string, int>();
                    this.LongFields[field] = 0;
                    break;
                case "string":
                    if (this.StringFields == null)
                        this.StringFields = new Dictionary<string, string>();
                    this.StringFields[field] = "";
                    break;
                case "filename":
                    if (this.FilenameFields == null)
                        this.FilenameFields = new Dictionary<string, string>();
                    this.FilenameFields[field] = "";
                    break;
                case "vec3field":
                    if (this.Vec3Fields == null)
                        this.Vec3Fields = new Dictionary<string, float[]>();
                    this.Vec3Fields[field] = new float[] { 0.0f, 0.0f, 0.0f };
                    break;
                case "vec4field":
                    if (this.Vec4Fields == null)
                        this.Vec4Fields = new Dictionary<string, float[]>();    
                    this.Vec4Fields[field] = new float[] { 0.0f, 0.0f, 0.0f, 0.0f };
                    break;
                default:
                    throw new ArgumentException($"Unknown record type: {type} available types are: bool,float,int,string,filename,vec3field and vec4field");
            }
        }
        public bool fieldExist(string field, string type)
        {
            switch (type)
            {
                case "bool":
                    return this.BoolFields != null && this.BoolFields.ContainsKey(field);
                case "float":
                    return this.FloatFields != null && this.FloatFields.ContainsKey(field);
                case "int":
                    return this.LongFields != null && this.LongFields.ContainsKey(field);
                case "string":
                    return this.StringFields != null && this.StringFields.ContainsKey(field);
                case "filename":
                    return this.FilenameFields != null && this.FilenameFields.ContainsKey(field);
                case "vec3field":
                    return this.Vec3Fields != null && this.Vec3Fields.ContainsKey(field);
                case "vec4field":
                    return this.Vec4Fields != null && this.Vec4Fields.ContainsKey(field);
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
            if (Vec3Fields != null)
                foreach (var kv in Vec3Fields)
                    fields.Add(kv.Key);

            if (Vec4Fields != null)
                foreach (var kv in Vec4Fields)
                    fields.Add(kv.Key);

            return fields;
        }
        public ModRecord deepClone()
        {
            var copy = new ModRecord
            {
                StringId = this.StringId,
                UnknownValue = this.UnknownValue,
                RecordType = this.RecordType,
                Id = this.Id,
                Name = this.Name,
                ChangeType = this.ChangeType
            };
            copy.BoolFields = this.BoolFields != null
                ? new Dictionary<string, bool>(this.BoolFields)
                : null;
            copy.FloatFields = this.FloatFields != null
                ? new Dictionary<string, float>(this.FloatFields)
                : null;
            copy.LongFields = this.LongFields != null
                ? new Dictionary<string, int>(this.LongFields)
                : null;
            copy.StringFields = this.StringFields != null
                ? this.StringFields.ToDictionary(kv => kv.Key, kv => kv.Value)
                : null;
            copy.FilenameFields = this.FilenameFields != null
                ? this.FilenameFields.ToDictionary(kv => kv.Key, kv => kv.Value)
                : null;
            copy.Vec3Fields = this.Vec3Fields != null
                ? this.Vec3Fields.ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone())
                : null;
            copy.Vec4Fields = this.Vec4Fields != null
                ? this.Vec4Fields.ToDictionary(kv => kv.Key, kv => (float[])kv.Value.Clone())
                : null;

            copy.ExtraDataFields = this.ExtraDataFields != null
                ? this.ExtraDataFields.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value != null
                        ? kv.Value.ToDictionary(kv2 => kv2.Key, kv2 => (int[])kv2.Value.Clone())
                        : new Dictionary<string, int[]>()
                  )
                : null;

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
                    States = inst.States != null ? new List<string>(inst.States) : null
                }).ToList()
                : null; 
            return copy;
        }

        public bool isTheSameRecord(ModRecord other)
        {
            return this.StringId == other.StringId;
        }
        public int GetRecordCompleteness()
        {
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
        public void CompleteFieldsFrom(ModRecord other)
        {
            if (other.BoolFields != null && other.BoolFields.Count > 0)
            {
                if (this.BoolFields == null)
                    this.BoolFields = new Dictionary<string, bool>();
                foreach (var kv in other.BoolFields)
                {
                    if (!this.BoolFields.ContainsKey(kv.Key))
                        this.BoolFields[kv.Key] = kv.Value;
                }
            }
            if (other.FloatFields != null && other.FloatFields.Count > 0)
            {
                if (this.FloatFields == null)
                    this.FloatFields = new Dictionary<string, float>();
                foreach (var kv in other.FloatFields)
                {
                    if (!this.FloatFields.ContainsKey(kv.Key))
                        this.FloatFields[kv.Key] = kv.Value;
                }
            }
            if (other.LongFields != null && other.LongFields.Count > 0)
            {
                if (this.LongFields == null)
                    this.LongFields = new Dictionary<string, int>();
                foreach (var kv in other.LongFields)
                {
                    if (!this.LongFields.ContainsKey(kv.Key))
                        this.LongFields[kv.Key] = kv.Value;
                }
            }
            if (other.Vec3Fields != null && other.Vec3Fields.Count > 0)
            {
                if (this.Vec3Fields == null)
                    this.Vec3Fields = new Dictionary<string, float[]>();
                foreach (var kv in other.Vec3Fields)
                {
                    if (!this.Vec3Fields.ContainsKey(kv.Key))
                        this.Vec3Fields[kv.Key] = kv.Value;
                }
            }
            if (other.Vec4Fields != null && other.Vec4Fields.Count > 0)
            {
                if (this.Vec4Fields == null)
                    this.Vec4Fields = new Dictionary<string, float[]>();
                foreach (var kv in other.Vec4Fields)
                {
                    if (!this.Vec4Fields.ContainsKey(kv.Key))
                        this.Vec4Fields[kv.Key] = kv.Value;
                }
            }
            if (other.StringFields != null && other.StringFields.Count > 0)
            {
                if (this.StringFields == null)
                    this.StringFields = new Dictionary<string, string>();
                foreach (var kv in other.StringFields)
                {
                    if (!this.StringFields.ContainsKey(kv.Key))
                        this.StringFields[kv.Key] = kv.Value;
                }
            }
            if (other.FilenameFields != null && other.FilenameFields.Count > 0)
            {
                if (this.FilenameFields == null)
                    this.FilenameFields = new Dictionary<string, string>();
                foreach (var kv in other.FilenameFields)
                {
                    if (!this.FilenameFields.ContainsKey(kv.Key))
                        this.FilenameFields[kv.Key] = kv.Value;
                }
            }
        }
        public void applyChangesFrom(ModRecord other)
        {
            if(other.BoolFields!=null&&other.BoolFields.Count > 0)
            {
                if(this.BoolFields == null)
                    this.BoolFields = new Dictionary<string, bool>();
                foreach (var kv in other.BoolFields)
                    this.BoolFields[kv.Key] = kv.Value;
            }
            if(other.FloatFields != null && other.FloatFields.Count > 0)
            {
                if (this.FloatFields == null)
                    this.FloatFields = new Dictionary<string, float>();
                foreach (var kv in other.FloatFields)
                    this.FloatFields[kv.Key] = kv.Value;
            }
            if(other.LongFields != null && other.LongFields.Count > 0)
            {
                if (this.LongFields == null)
                    this.LongFields = new Dictionary<string, int>();
                foreach (var kv in other.LongFields)
                    this.LongFields[kv.Key] = kv.Value;
            }
            if(other.Vec3Fields != null && other.Vec3Fields.Count > 0)
            {
                if (this.Vec3Fields == null)
                    this.Vec3Fields = new Dictionary<string, float[]>();
                foreach (var kv in other.Vec3Fields)
                    this.Vec3Fields[kv.Key] = (float[])kv.Value.Clone();
            }
            if(other.Vec4Fields != null && other.Vec4Fields.Count > 0)
            {
                if (this.Vec4Fields == null)
                    this.Vec4Fields = new Dictionary<string, float[]>();
                foreach (var kv in other.Vec4Fields)
                    this.Vec4Fields[kv.Key] = (float[])kv.Value.Clone();
            }
            if(other.StringFields != null && other.StringFields.Count > 0)
            {
                if (this.StringFields == null)
                    this.StringFields = new Dictionary<string, string>();
                foreach (var kv in other.StringFields)
                    this.StringFields[kv.Key] = kv.Value;
            }
            if(other.FilenameFields != null && other.FilenameFields.Count > 0)
            {
                if (this.FilenameFields == null)
                    this.FilenameFields = new Dictionary<string, string>();
                foreach (var kv in other.FilenameFields)
                    this.FilenameFields[kv.Key] = kv.Value;
            }
            if(other.ExtraDataFields != null && other.ExtraDataFields.Count > 0)
            {
                if(this.ExtraDataFields == null)
                    this.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();

                foreach (var kv in other.ExtraDataFields)
                {
                    if (!this.ExtraDataFields.ContainsKey(kv.Key))
                        this.ExtraDataFields[kv.Key] = new Dictionary<string, int[]>();

                    foreach (var itemKv in kv.Value)
                    {
                        if (IsDeleted(itemKv.Value))
                        {
                            this.ExtraDataFields[kv.Key].Remove(itemKv.Key);
                        }
                        else
                        {
                            this.ExtraDataFields[kv.Key][itemKv.Key] = (int[])itemKv.Value.Clone();
                        }
                    }
                }
            }
            if (other.InstanceFields != null && other.InstanceFields.Count > 0)
            {
                if (this.InstanceFields == null)
                    this.InstanceFields = new List<ModInstance>();

                this.InstanceFields.AddRange(
                    other.InstanceFields.Select(inst => new ModInstance
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
                        States = inst.States != null
                            ? new List<string>(inst.States)
                            : new List<string>()
                    })
                );
            }
        }
        public static bool IsDeleted(int[] value)
        {
            return value.Length == 3 &&
                   value[0] == ReverseEngineer.DELETED &&
                   value[1] == ReverseEngineer.DELETED &&
                   value[2] == ReverseEngineer.DELETED;
        }
        public bool isExtraDataOfThis(ModRecord other, string? category = null, int[]? variables = null)
        {
            IEnumerable<Dictionary<string, int[]>> dicts;
            if(ExtraDataFields == null || ExtraDataFields.Count == 0)
                return false;
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
            if(other.ExtraDataFields == null || other.ExtraDataFields.Count == 0)
                return false;

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

        public bool isFieldChanged(string field,string? type=null)
        {
            if (type == "bool")
            {
                return this.BoolFields!=null&&this.BoolFields.ContainsKey(field);
            }
            if (type == "float")
            {
                return this.FloatFields!=null&&this.FloatFields.ContainsKey(field);
            }
            if (type == "long")
            {
                return this.LongFields!=null&&this.LongFields.ContainsKey(field);
            }
            if (type == "vec3")
            {
                return this.Vec3Fields!=null&&this.Vec3Fields.ContainsKey(field);
            }
            if (type == "vec4")
            {
                return this.Vec4Fields!=null&&this.Vec4Fields.ContainsKey(field);
            }
            if (type == "string")
            {
                return this.StringFields!=null&&this.StringFields.ContainsKey(field);
            }
            if (type == "filename")
            {
                return this.FilenameFields!=null&&this.FilenameFields.ContainsKey(field);
            }
            if (type == null)
            {
                return isFieldChanged(field,"bool")|| isFieldChanged(field,"float")|| isFieldChanged(field,"long")
                    || isFieldChanged(field, "vec3") || isFieldChanged(field, "vec4") || isFieldChanged(field, "string")
                    || isFieldChanged(field, "filename");
            }
            throw new ArgumentException($"Unknown field type: {type} available types are: bool,float,long,vec3,vec4,string and filename");

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
        public static string getRecordTypeName(int code)
        {
            return ModTypeCodes.GetValueOrDefault(code, $"UNKNOWN:{code.ToString()}");
        }
        public static int getRecordTypeInt(string name)
        {
            return ModTypeNames.GetValueOrDefault(name, -1);
        }
        public string getRecordType()
        {
            return getRecordTypeName(this.RecordType);
        }
        public int getRecordTypeCode()
        {
            return this.RecordType;
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
            //string first3Groups = string.Join(" | ", Enumerable.Range(0, 3).Select(i => binary.Substring(i * 4, 4)));

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
            if(this.isRemoved())
                result += " REMOVED";


            return result;
        }
        public bool isRemoved()
        {
            return this.BoolFields!=null&&this.BoolFields.TryGetValue("REMOVED", out var value) && value;
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
        public void SetAsNew(int filetype)
        {
            SetRecordStatus(filetype, "new");
        }
        public void SetAsExisting(int filetype)
        {
            SetRecordStatus(filetype, "existing");
        }
        public void SetRecordStatus(int fileType, string status)
        {
            // Convert to 32-bit binary string
            char[] binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0').ToCharArray();

            // Extract the last 4 bits (bits 28–31)
            string lastGroup = new string(binary[28..32]);

            string newLastGroup = lastGroup; // default keep existing

            switch (status.ToLowerInvariant())
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
            ChangeType = unchecked((int)Convert.ToUInt32(new string(binary), 2));
            //ChangeType = Convert.ToInt32(new string(binary), 2);
        }
        public static string GetModNameFromId(string stringId)
        {
            if (string.IsNullOrEmpty(stringId))
                return string.Empty;

            // Expected format: "number-modname.mod"
            int dashIndex = stringId.IndexOf('-');
            if (dashIndex == -1 || dashIndex >= stringId.Length - 1)
                return string.Empty;

            string modPart = stringId.Substring(dashIndex + 1);

            // Ensure it ends with ".mod"
            if (modPart.EndsWith(".mod", StringComparison.Ordinal))
                return modPart;

            return string.Empty;
        }
        public string GetModName()
        {
            return GetModNameFromId(this.StringId);
        }
        public bool ValidateDataTypeAssumptions()
        {
            return ModTypeCodes.ContainsKey(this.RecordType);

        }
        public static bool isTypeCode(string s)
        {
            return ModTypeNames.ContainsKey(s);
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
            return (BoolFields!=null&&BoolFields.ContainsKey(field)) || (FloatFields!=null&&FloatFields.ContainsKey(field)) ||
                   (LongFields!=null&&LongFields.ContainsKey(field)) || (Vec3Fields!=null&&Vec3Fields.ContainsKey(field)) ||
                   (Vec4Fields!=null&&Vec4Fields.ContainsKey(field)) || (StringFields!=null&&StringFields.ContainsKey(field)) ||
                   (FilenameFields!=null&&FilenameFields.ContainsKey(field));
        }
        private bool TrySetVector(Dictionary<string, float[]> dict, string key, string value, int length)
        {
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != length) return false;

            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (!float.TryParse(parts[i],NumberStyles.Float,CultureInfo.InvariantCulture, out result[i]))
                    return false;
                //if (!float.TryParse(parts[i], out result[i]))

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
            if (this.FloatFields!=null&&this.FloatFields.TryGetValue(field, out float f)) return f;
            if (this.LongFields!=null&&this.LongFields.TryGetValue(field, out int l)) return l;
            if (this.BoolFields!=null&&this.BoolFields.TryGetValue(field, out bool b)) return b;
            if (this.StringFields!=null&&this.StringFields.TryGetValue(field, out string? s)) return s;
            if (this.FilenameFields!=null&&this.FilenameFields.TryGetValue(field, out string? fn)) return fn;
            if (this.Vec3Fields != null && this.Vec3Fields.TryGetValue(field, out var v3)) return v3; //v3.Length > 0 ? v3[0] : 0f;
            if (this.Vec4Fields != null && this.Vec4Fields.TryGetValue(field, out var v4)) return v4;//v4.Length > 0 ? v4[0] : 0f;

            return null;
        }
        public void SetField(string field, string value)
        {

            if (additionalSetters.TryGetValue(field, out var setter))
            {
                setter(this, value);
                return;
            }

            if (BoolFields!=null&&BoolFields.ContainsKey(field))
            {
                TrySet(BoolFields, field, value, bool.TryParse);
                return;
            }
            if (FloatFields!=null&&FloatFields.ContainsKey(field))
            {
                TrySet(FloatFields, field, value, (string s, out float result) =>float.TryParse(s,NumberStyles.Float, CultureInfo.InvariantCulture,out result));
                return;
            }
            if (LongFields!=null&&LongFields.ContainsKey(field))
            {
                TrySet(LongFields, field, value, int.TryParse);
                return;
            }
            if (Vec3Fields != null && Vec3Fields.ContainsKey(field))
            {
                //TrySetVector(Vec3Fields, field, value, 3);
                if (!TrySetVector(Vec3Fields, field, value, 3))
                    throw new FormatException($"invalid value for field: {field}={value} on record {this.Name} ({this.StringId})");
                return;
            }
            if (Vec4Fields != null && Vec4Fields.ContainsKey(field))
            {
                //TrySetVector(Vec4Fields, field, value, 4);
                if (!TrySetVector(Vec4Fields, field, value, 4))
                    throw new FormatException($"invalid value for field: {field}={value} on record {this.Name} ({this.StringId})");
                return;
            }
            if (StringFields != null && StringFields.ContainsKey(field))
            {
                StringFields[field] = value;
                return;
            }
            if (FilenameFields != null && FilenameFields.ContainsKey(field))
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
        public string? GetFieldAsString(string field)
        {
            additionalGetters.TryGetValue(field, out var fieldfunc);
            if (fieldfunc != null)
                return fieldfunc(this);
            if (BoolFields != null && BoolFields.ContainsKey(field))
                return BoolFields.GetValueOrDefault(field).ToString();
            if (FloatFields != null && FloatFields.TryGetValue(field, out var f))
                return f.ToString(CultureInfo.InvariantCulture);
            if (LongFields != null && LongFields.ContainsKey(field))
                return LongFields.GetValueOrDefault(field).ToString();
            if (Vec3Fields != null && Vec3Fields.TryGetValue(field, out var v3))
                return string.Join(",", v3.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            if (Vec4Fields != null && Vec4Fields.TryGetValue(field, out var v4))
                return string.Join(",", v4.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            if (StringFields != null && StringFields.ContainsKey(field))
                return StringFields.GetValueOrDefault(field)!.ToString();
            if (FilenameFields != null && FilenameFields.ContainsKey(field))
                return FilenameFields.GetValueOrDefault(field)!.ToString();
            return null;
        }

        public bool isExtraDataEmpty(string? category)
        {
            if (ExtraDataFields == null) return true;
            return category == null
                ? ExtraDataFields.All(cat => cat.Value.Count == 0)
                : !ExtraDataFields.TryGetValue(category, out var values) || values.Count == 0;
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
