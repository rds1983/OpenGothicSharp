using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenGothic.ZenKit;

public sealed class WayPoint
{
	public string Name { get; internal set; } = string.Empty;

	public int WaterDepth { get; internal set; }

	public bool UnderWater { get; internal set; }

	public Vector3 Position { get; internal set; }

	public Vector3 Direction { get; internal set; }

	public bool FreePoint { get; internal set; }
}

public sealed class WayNet
{
	public List<WayPoint> Points { get; } = new List<WayPoint>();

	public List<(int A, int B)> Edges { get; } = new List<(int A, int B)>();
}

/// <summary>The parts of a ZEN world the client consumes.</summary>
public sealed class WorldData
{
	public Mesh Mesh { get; internal set; }

	public BspTree Bsp { get; internal set; }

	public WayNet WayNet { get; internal set; }

	public List<VirtualObject> RootObjects { get; } = new List<VirtualObject>();
}

/// <summary>
/// World reader. The world is a BIN_SAFE ZenGin archive: the mesh and BSP are stored as a
/// raw chunk block inside `MeshAndBsp`, while the vob tree, waynet and markers are ordinary
/// archive objects.
/// </summary>
public static class WorldReader
{
	private const ushort MeshEnd = 0xB060;

	public static WorldData Load(Read reader)
	{
		var archive = ZenArchive.Open(reader);
		MeshAndBsp meshAndBsp = null;

		archive.ObjectBodyReader = (a, obj) =>
		{
			if (obj.Name != "MeshAndBsp")
			{
				return false;
			}

			meshAndBsp = MeshAndBsp.Read(a.Reader);
			return true;
		};

		var root = archive.ReadObject();
		if (root == null)
		{
			throw new ZenArchiveException("The world archive has no root object.");
		}

		var objects = new Dictionary<uint, ZenObject>();
		IndexObjects(root, objects);

		var world = new WorldData { Mesh = meshAndBsp?.Mesh, Bsp = meshAndBsp?.Bsp };

		var vobTree = root.Children.FirstOrDefault(c => c.Name == "VobTree");
		if (vobTree != null)
		{
			foreach (var vob in VobReader.CreateTree(vobTree, objects))
			{
				world.RootObjects.Add(vob);
			}
		}

		var wayNetHolder = root.Children.FirstOrDefault(c => c.Name == "WayNet");
		var wayNet = wayNetHolder?.Children.FirstOrDefault(c => c.ClassName == "zCWayNet");
		if (wayNet != null)
		{
			world.WayNet = WayNetReader.Load(wayNet, objects);
		}

		return world;
	}

	private static void IndexObjects(ZenObject obj, Dictionary<uint, ZenObject> objects)
	{
		if (!obj.IsReference && !obj.IsEmpty && !objects.ContainsKey(obj.Index))
		{
			objects[obj.Index] = obj;
		}

		foreach (var child in obj.Children)
		{
			IndexObjects(child, objects);
		}
	}

	private sealed class MeshAndBsp
	{
		public Mesh Mesh { get; private set; }

		public BspTree Bsp { get; private set; }

		public static MeshAndBsp Read(Read reader)
		{
			var bspVersion = reader.ReadUInt();
			_ = reader.ReadUInt(); // section size

			var meshStart = reader.Position;
			while (!reader.Eof)
			{
				var type = reader.ReadUShort();
				var size = reader.ReadUInt();
				if (type == MeshEnd)
				{
					break;
				}

				reader.Skip(size);
			}

			var bsp = BspTree.Load(reader, bspVersion);
			var bspEnd = reader.Position;

			reader.Seek(meshStart);
			var mesh = Mesh.Load(reader);
			reader.Seek(bspEnd);

			return new MeshAndBsp { Mesh = mesh, Bsp = bsp };
		}
	}
}

internal static class WayNetReader
{
	public static WayNet Load(ZenObject source, IReadOnlyDictionary<uint, ZenObject> objects)
	{
		var result = new WayNet();
		var pointByObject = new Dictionary<uint, WayPoint>();
		var indexByPoint = new Dictionary<WayPoint, int>();

		WayPoint GetPoint(ZenObject obj)
		{
			if (obj.IsReference && objects.TryGetValue(obj.Index, out var target))
			{
				obj = target;
			}

			if (pointByObject.TryGetValue(obj.Index, out var existing))
			{
				return existing;
			}

			var point = new WayPoint
			{
				Name = obj.GetString("wpName"),
				WaterDepth = obj.GetInt("waterDepth"),
				UnderWater = obj.GetBool("underWater"),
				Position = obj.GetVec3("position"),
				Direction = obj.GetVec3("direction")
			};

			pointByObject[obj.Index] = point;
			indexByPoint[point] = result.Points.Count;
			result.Points.Add(point);
			return point;
		}

		var children = source.Children;
		var freeCount = Math.Min(source.GetInt("numWaypoints"), children.Count);
		for (var i = 0; i < freeCount; ++i)
		{
			GetPoint(children[i]).FreePoint = true;
		}

		var edgeCount = source.GetInt("numWays");
		for (var i = 0; i < edgeCount; ++i)
		{
			var firstIndex = freeCount + i * 2;
			var secondIndex = firstIndex + 1;
			if (secondIndex >= children.Count)
			{
				break;
			}

			var first = GetPoint(children[firstIndex]);
			var second = GetPoint(children[secondIndex]);
			result.Edges.Add((indexByPoint[first], indexByPoint[second]));
		}

		return result;
	}
}
