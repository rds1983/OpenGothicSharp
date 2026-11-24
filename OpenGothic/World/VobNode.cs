using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using System;
using System.Collections.Generic;
using ZenKit.Vobs;

namespace OpenGothic.Vobs;

/// <summary>
/// One ZEN virtual object in the world tree. The node keeps both the raw ZenKit object (for
/// type-specific data added by later milestones) and the resolved world/local transforms. The
/// rendered <see cref="SceneNode"/> is optional: spawn points, triggers, sounds, decals and
/// effects exist in the tree even when nothing is drawn for them.
/// </summary>
public class VobNode
{
	private readonly List<VobNode> _children = new();

	protected VobNode(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds)
	{
		Source = source;
		Parent = parent;
		World = world;
		Local = local;
		Kind = kind;
		Bounds = bounds;
		Type = source.Type;
		Id = source.Id;
		var visual = source.Visual;
		VisualType = visual?.Type;

		// Many vobs have an empty instance/preset name; the visual name is the useful tag
		// for querying and diagnostics (e.g. BOOK_NW_CITY_CUPBOARD_01.ASC).
		var visualName = visual?.Name;
		Name = !string.IsNullOrEmpty(source.Name) ? source.Name : visualName ?? string.Empty;
		Tag = !string.IsNullOrEmpty(source.PresetName) ? source.PresetName :
			!string.IsNullOrEmpty(source.Name) ? source.Name : visualName ?? string.Empty;

		parent?._children.Add(this);
	}

	/// <summary>Creates a synthetic node; used by tests and by callers without a ZenKit object.</summary>
	public VobNode(VobKind kind, string name, string tag, Vector3 position, BoundingBox bounds,
		VobNode parent = null)
	{
		Kind = kind;
		Name = name ?? string.Empty;
		Tag = string.IsNullOrEmpty(tag) ? Name : tag;
		Parent = parent;
		World = Matrix.CreateTranslation(position);
		Local = World;
		Bounds = bounds;
		Type = VirtualObjectType.Unknown;
		Id = -1;
		VisualType = null;

		parent?._children.Add(this);
	}

	public VobKind Kind { get; }

	public VirtualObjectType Type { get; }

	public VisualType? VisualType { get; }

	public int Id { get; }

	public string Name { get; }

	/// <summary>Prototype/preset name when the file has one, otherwise <see cref="Name"/>.</summary>
	public string Tag { get; }

	public VobNode Parent { get; }

	public IReadOnlyList<VobNode> Children => _children;

	public Matrix World { get; internal set; }

	public Matrix Local { get; internal set; }

	public Vector3 Position => World.Translation;

	public BoundingBox Bounds { get; internal set; }

	public IVirtualObject Source { get; }

	/// <summary>Rendered node for this vob, or null for a marker/effect without geometry.</summary>
	public SceneNode SceneNode { get; internal set; }

	public bool HasVisual => SceneNode != null;

	public T As<T>() where T : class, IVirtualObject => Source as T;
}
