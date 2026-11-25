using OpenGothic.Vobs;
using OpenGothic.ZenKit;

namespace OpenGothic.Scripts;

/// <summary>
/// Links one Daedalus C_NPC instance to its runtime vob. The vob owns the values shown to the
/// game (name, guild, level, hit points); the binding only adds the script identity and the
/// waypoint the instance was inserted at.
/// </summary>
internal sealed class NpcBinding
{
	public int SymbolIndex;
	public NpcInstance Instance;
	public NpcVob Vob;
	public string SpawnPoint;
}
