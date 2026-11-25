using System;
using System.Collections.Generic;

namespace OpenGothic.ZenKit;

/// <summary>One Daedalus class and the value slots its instances allocate.</summary>
internal sealed class DaedalusClassLayout
{
	public DaedalusClassLayout(DaedalusSymbol classSymbol)
	{
		ClassSymbol = classSymbol;
	}

	public DaedalusSymbol ClassSymbol { get; }

	public Dictionary<string, DaedalusSymbol> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

	public int IntCount { get; private set; }

	public int FloatCount { get; private set; }

	public int StringCount { get; private set; }

	public int InstanceCount { get; private set; }

	public void Add(DaedalusSymbol member)
	{
		var name = member.Name;
		var prefix = ClassSymbol.Name + ".";
		if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			name = name.Substring(prefix.Length);
		}

		Members[name] = member;
		member.Layout = this;

		switch (member.Type)
		{
			case DaedalusDataType.Float:
				member.FloatSlot = FloatCount;
				FloatCount += (int)member.Count;
				break;
			case DaedalusDataType.String:
				member.StringSlot = StringCount;
				StringCount += (int)member.Count;
				break;
			case DaedalusDataType.Instance:
				member.InstanceSlot = InstanceCount;
				InstanceCount += (int)member.Count;
				break;
			default:
				member.IntSlot = IntCount;
				IntCount += (int)member.Count;
				break;
		}
	}

	public DaedalusSymbol FindMember(string name)
	{
		return Members.TryGetValue(name, out var member) ? member : null;
	}
}

/// <summary>
/// A script instance. Values live in per-class slot arrays so the VM and the typed wrappers
/// read and write the same storage without copying.
/// </summary>
public class DaedalusInstance
{
	public int Index { get; internal set; } = -1;

	public object UserData { get; set; }

	/// <summary>Class the instance was initialized from, e.g. <c>C_NPC</c>.</summary>
	public string ClassName => ClassSymbol?.Name ?? string.Empty;

	internal DaedalusVm Vm { get; set; }

	internal DaedalusSymbol ClassSymbol { get; set; }

	internal DaedalusClassLayout Layout { get; set; }

	internal DaedalusSymbol InstanceSymbol { get; set; }

	internal int[] Ints = Array.Empty<int>();

	internal float[] Floats = Array.Empty<float>();

	internal string[] Strings = Array.Empty<string>();

	internal DaedalusInstance[] Instances = Array.Empty<DaedalusInstance>();

	internal virtual string ExpectedClassName => null;

	internal void Allocate(DaedalusVm vm, DaedalusSymbol instanceSymbol, DaedalusSymbol classSymbol,
		DaedalusClassLayout layout)
	{
		Vm = vm;
		InstanceSymbol = instanceSymbol;
		ClassSymbol = classSymbol;
		Layout = layout;
		Index = (int)instanceSymbol.Index;

		Ints = layout.IntCount > 0 ? new int[layout.IntCount] : Array.Empty<int>();
		Floats = layout.FloatCount > 0 ? new float[layout.FloatCount] : Array.Empty<float>();
		if (layout.StringCount > 0)
		{
			Strings = new string[layout.StringCount];
			Array.Fill(Strings, string.Empty);
		}
		else
		{
			Strings = Array.Empty<string>();
		}

		Instances = layout.InstanceCount > 0
			? new DaedalusInstance[layout.InstanceCount]
			: Array.Empty<DaedalusInstance>();
	}

	internal DaedalusSymbol Member(string name)
	{
		var member = Layout?.FindMember(name);
		if (member == null)
		{
			throw new DaedalusScriptException($"Class '{ClassName}' has no member '{name}'.");
		}

		return member;
	}

	protected int GetInt(string name, int index = 0)
	{
		return GetInt(Member(name), index);
	}

	protected float GetFloat(string name, int index = 0)
	{
		return GetFloat(Member(name), index);
	}

	protected string GetString(string name, int index = 0)
	{
		return GetString(Member(name), index);
	}

	protected void SetInt(string name, int value, int index = 0)
	{
		SetInt(Member(name), index, value);
	}

	protected void SetFloat(string name, float value, int index = 0)
	{
		SetFloat(Member(name), index, value);
	}

	protected void SetString(string name, string value, int index = 0)
	{
		SetString(Member(name), index, value);
	}

	internal int GetInt(DaedalusSymbol member, int index)
	{
		CheckMember(member, index);
		return Ints[member.IntSlot + index];
	}

	internal float GetFloat(DaedalusSymbol member, int index)
	{
		CheckMember(member, index);
		return Floats[member.FloatSlot + index];
	}

	internal string GetString(DaedalusSymbol member, int index)
	{
		CheckMember(member, index);
		return Strings[member.StringSlot + index];
	}

	internal DaedalusInstance GetInstance(DaedalusSymbol member, int index)
	{
		CheckMember(member, index);
		return Instances[member.InstanceSlot + index];
	}

	internal void SetInt(DaedalusSymbol member, int index, int value)
	{
		CheckMember(member, index);
		Ints[member.IntSlot + index] = value;
	}

	internal void SetFloat(DaedalusSymbol member, int index, float value)
	{
		CheckMember(member, index);
		Floats[member.FloatSlot + index] = value;
	}

	internal void SetString(DaedalusSymbol member, int index, string value)
	{
		CheckMember(member, index);
		Strings[member.StringSlot + index] = value ?? string.Empty;
	}

	internal void SetInstance(DaedalusSymbol member, int index, DaedalusInstance value)
	{
		CheckMember(member, index);
		Instances[member.InstanceSlot + index] = value;
	}

	private void CheckMember(DaedalusSymbol member, int index)
	{
		if (member == null || !member.IsMember || member.Layout != Layout)
		{
			throw new DaedalusScriptException($"Member '{member?.Name}' does not belong to '{ClassName}'.");
		}

		if (index < 0 || index >= member.Count)
		{
			throw new DaedalusScriptException($"Index {index} is out of range for '{member.Name}'.");
		}
	}
}

public enum NpcType
{
	G1Ambient = 0,
	G1Main = 1,
	G1Guard = 2,
	G1Friend = 3,
	G1MineAmbient = 4,
	G1MineGuard = 5,
	G1OwAmbient = 6,
	G1OwGuard = 7,
	G1Rogue = 8,
	G2Ambient = 0,
	G2Main = 1,
	G2Friend = 2,
	G2OcAmbient = 3,
	G2OcMain = 4,
	G2BlAmbient = 5,
	G2TalAmbient = 6,
	G2BlMain = 7
}

public enum NpcNameSlot
{
	Slot0 = 0,
	Slot1 = 1,
	Slot2 = 2,
	Slot3 = 3,
	Slot4 = 4
}

public enum NpcMissionSlot
{
	Slot0 = 0,
	Slot1 = 1,
	Slot2 = 2,
	Slot3 = 3,
	Slot4 = 4
}

public enum NpcAttribute
{
	HitPoints = 0,
	HitPointsMax = 1,
	Mana = 2,
	ManaMax = 3,
	Strength = 4,
	Dexterity = 5,
	RegenerateHp = 6,
	RegenerateMana = 7
}

public enum NpcTalent
{
	Unknown = 0,
	OneHanded = 1,
	TwoHanded = 2,
	Bow = 3,
	Crossbow = 4
}

public enum DamageType
{
	Barrier = 0,
	Blunt = 1,
	Edge = 2,
	Fire = 3,
	Fly = 4,
	Magic = 5,
	Point = 6,
	Fall = 7
}

/// <summary>Typed view over a <c>C_NPC</c> instance.</summary>
public sealed class NpcInstance : DaedalusInstance
{
	internal override string ExpectedClassName => "C_NPC";

	public int Id
	{
		get => GetInt("ID");
		set => SetInt("ID", value);
	}

	public string Slot
	{
		get => GetString("SLOT");
		set => SetString("SLOT", value);
	}

	public string Effect
	{
		get => GetString("EFFECT");
		set => SetString("EFFECT", value);
	}

	public NpcType NpcType
	{
		get => (NpcType)GetInt("NPCTYPE");
		set => SetInt("NPCTYPE", (int)value);
	}

	public int Flags
	{
		get => GetInt("FLAGS");
		set => SetInt("FLAGS", value);
	}

	public int DamageType
	{
		get => GetInt("DAMAGETYPE");
		set => SetInt("DAMAGETYPE", value);
	}

	public int Guild
	{
		get => GetInt("GUILD");
		set => SetInt("GUILD", value);
	}

	public int Level
	{
		get => GetInt("LEVEL");
		set => SetInt("LEVEL", value);
	}

	public int FightTactic
	{
		get => GetInt("FIGHT_TACTIC");
		set => SetInt("FIGHT_TACTIC", value);
	}

	public int Weapon
	{
		get => GetInt("WEAPON");
		set => SetInt("WEAPON", value);
	}

	public int Voice
	{
		get => GetInt("VOICE");
		set => SetInt("VOICE", value);
	}

	public int VoicePitch
	{
		get => GetInt("VOICEPITCH");
		set => SetInt("VOICEPITCH", value);
	}

	public int BodyMass
	{
		get => GetInt("BODYMASS");
		set => SetInt("BODYMASS", value);
	}

	public int DailyRoutine
	{
		get => GetInt("DAILY_ROUTINE");
		set => SetInt("DAILY_ROUTINE", value);
	}

	public int StartAiState
	{
		get => GetInt("START_AISTATE");
		set => SetInt("START_AISTATE", value);
	}

	public string SpawnPoint
	{
		get => GetString("SPAWNPOINT");
		set => SetString("SPAWNPOINT", value);
	}

	public int SpawnDelay
	{
		get => GetInt("SPAWNDELAY");
		set => SetInt("SPAWNDELAY", value);
	}

	public int Senses
	{
		get => GetInt("SENSES");
		set => SetInt("SENSES", value);
	}

	public int SensesRange
	{
		get => GetInt("SENSES_RANGE");
		set => SetInt("SENSES_RANGE", value);
	}

	public string Wp
	{
		get => GetString("WP");
		set => SetString("WP", value);
	}

	public int Exp
	{
		get => GetInt("EXP");
		set => SetInt("EXP", value);
	}

	public int ExpNext
	{
		get => GetInt("EXP_NEXT");
		set => SetInt("EXP_NEXT", value);
	}

	public int Lp
	{
		get => GetInt("LP");
		set => SetInt("LP", value);
	}

	public int BodyStateInterruptableOverride
	{
		get => GetInt("BODYSTATEINTERRUPTABLEOVERRIDE");
		set => SetInt("BODYSTATEINTERRUPTABLEOVERRIDE", value);
	}

	public int NoFocus
	{
		get => GetInt("NOFOCUS");
		set => SetInt("NOFOCUS", value);
	}

	public string GetName(NpcNameSlot slot) => GetString("NAME", (int)slot);

	public void SetName(NpcNameSlot slot, string value) => SetString("NAME", value, (int)slot);

	public int GetMission(NpcMissionSlot slot) => GetInt("MISSION", (int)slot);

	public void SetMission(NpcMissionSlot slot, int value) => SetInt("MISSION", value, (int)slot);

	public int GetAttribute(NpcAttribute attribute) => GetInt("ATTRIBUTE", (int)attribute);

	public void SetAttribute(NpcAttribute attribute, int value) => SetInt("ATTRIBUTE", value, (int)attribute);

	public int GetHitChance(NpcTalent talent) => GetInt("HITCHANCE", (int)talent);

	public void SetHitChance(NpcTalent talent, int value) => SetInt("HITCHANCE", value, (int)talent);

	public int GetProtection(DamageType type) => GetInt("PROTECTION", (int)type);

	public void SetProtection(DamageType type, int value) => SetInt("PROTECTION", value, (int)type);

	public int GetDamage(DamageType type) => GetInt("DAMAGE", (int)type);

	public void SetDamage(DamageType type, int value) => SetInt("DAMAGE", value, (int)type);

	public int GetAiVar(int index) => GetInt("AIVAR", index);

	public void SetAiVar(int index, int value) => SetInt("AIVAR", value, index);
}

/// <summary>Typed view over a <c>C_ITEM</c> instance.</summary>
public sealed class ItemInstance : DaedalusInstance
{
	internal override string ExpectedClassName => "C_ITEM";

	public int Id
	{
		get => GetInt("ID");
		set => SetInt("ID", value);
	}

	public string Name
	{
		get => GetString("NAME");
		set => SetString("NAME", value);
	}

	public string NameId
	{
		get => GetString("NAMEID");
		set => SetString("NAMEID", value);
	}

	public int Hp
	{
		get => GetInt("HP");
		set => SetInt("HP", value);
	}

	public int HpMax
	{
		get => GetInt("HP_MAX");
		set => SetInt("HP_MAX", value);
	}

	public int MainFlag
	{
		get => GetInt("MAINFLAG");
		set => SetInt("MAINFLAG", value);
	}

	public int Flags
	{
		get => GetInt("FLAGS");
		set => SetInt("FLAGS", value);
	}

	public int Weight
	{
		get => GetInt("WEIGHT");
		set => SetInt("WEIGHT", value);
	}

	public int Value
	{
		get => GetInt("VALUE");
		set => SetInt("VALUE", value);
	}

	public int DamageType
	{
		get => GetInt("DAMAGETYPE");
		set => SetInt("DAMAGETYPE", value);
	}

	public int DamageTotal
	{
		get => GetInt("DAMAGETOTAL");
		set => SetInt("DAMAGETOTAL", value);
	}

	public int Wear
	{
		get => GetInt("WEAR");
		set => SetInt("WEAR", value);
	}

	public int Nutrition
	{
		get => GetInt("NUTRITION");
		set => SetInt("NUTRITION", value);
	}

	public string Visual
	{
		get => GetString("VISUAL");
		set => SetString("VISUAL", value);
	}

	public string VisualChange
	{
		get => GetString("VISUAL_CHANGE");
		set => SetString("VISUAL_CHANGE", value);
	}

	public string Effect
	{
		get => GetString("EFFECT");
		set => SetString("EFFECT", value);
	}

	public int VisualSkin
	{
		get => GetInt("VISUAL_SKIN");
		set => SetInt("VISUAL_SKIN", value);
	}

	public string SchemeName
	{
		get => GetString("SCEMENAME");
		set => SetString("SCEMENAME", value);
	}

	public int Material
	{
		get => GetInt("MATERIAL");
		set => SetInt("MATERIAL", value);
	}

	public int Munition
	{
		get => GetInt("MUNITION");
		set => SetInt("MUNITION", value);
	}

	public int Spell
	{
		get => GetInt("SPELL");
		set => SetInt("SPELL", value);
	}

	public int Range
	{
		get => GetInt("RANGE");
		set => SetInt("RANGE", value);
	}

	public int MagCircle
	{
		get => GetInt("MAG_CIRCLE");
		set => SetInt("MAG_CIRCLE", value);
	}

	public string Description
	{
		get => GetString("DESCRIPTION");
		set => SetString("DESCRIPTION", value);
	}

	public int GetDamage(DamageType type) => GetInt("DAMAGE", (int)type);

	public void SetDamage(DamageType type, int value) => SetInt("DAMAGE", value, (int)type);

	public int GetProtection(DamageType type) => GetInt("PROTECTION", (int)type);

	public void SetProtection(DamageType type, int value) => SetInt("PROTECTION", value, (int)type);
}

/// <summary>Typed view over a <c>C_INFO</c> instance.</summary>
public sealed class InfoInstance : DaedalusInstance
{
	internal override string ExpectedClassName => "C_INFO";

	public int Npc
	{
		get => GetInt("NPC");
		set => SetInt("NPC", value);
	}

	public int Nr
	{
		get => GetInt("NR");
		set => SetInt("NR", value);
	}

	public int Important
	{
		get => GetInt("IMPORTANT");
		set => SetInt("IMPORTANT", value);
	}

	public int Condition
	{
		get => GetInt("CONDITION");
		set => SetInt("CONDITION", value);
	}

	public int Information
	{
		get => GetInt("INFORMATION");
		set => SetInt("INFORMATION", value);
	}

	public string Description
	{
		get => GetString("DESCRIPTION");
		set => SetString("DESCRIPTION", value);
	}

	public int Trade
	{
		get => GetInt("TRADE");
		set => SetInt("TRADE", value);
	}

	public int Permanent
	{
		get => GetInt("PERMANENT");
		set => SetInt("PERMANENT", value);
	}
}
