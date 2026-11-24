using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace OpenGothic.Vobs;

/// <summary>
/// Flat index over a ZEN vob tree. The tree is built once while the world loads; queries are
/// linear over the ~10k vobs, which is cheap enough for focus, triggers and diagnostics, and
/// avoids a second spatial structure until one is needed.
/// </summary>
public sealed class VobTree
{
	private readonly List<VobNode> _all = new();
	private readonly List<VobNode> _roots = new();

	public IReadOnlyList<VobNode> All => _all;

	public IReadOnlyList<VobNode> Roots => _roots;

	public int Count => _all.Count;

	public void Add(VobNode node)
	{
		if (node == null)
		{
			throw new ArgumentNullException(nameof(node));
		}

		_all.Add(node);
		if (node.Parent == null)
		{
			_roots.Add(node);
		}
	}

	/// <summary>
	/// Fills <paramref name="results"/> with nodes whose bounds are within <paramref name="radius"/>
	/// of <paramref name="position"/>, optionally restricted to one kind and/or tag.
	/// </summary>
	public void FindNear(Vector3 position, float radius, List<VobNode> results,
		VobKind? kind = null, string tag = null)
	{
		results.Clear();
		var radiusSquared = radius * radius;

		foreach (var vob in _all)
		{
			if (kind.HasValue && vob.Kind != kind.Value)
			{
				continue;
			}

			if (!string.IsNullOrEmpty(tag) &&
			    !string.Equals(vob.Tag, tag, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (DistanceSquared(position, vob.Bounds) <= radiusSquared)
			{
				results.Add(vob);
			}
		}
	}

	/// <summary>Fills <paramref name="results"/> with nodes whose world bounds contain a point.</summary>
	public void FindAt(Vector3 point, List<VobNode> results, VobKind? kind = null)
	{
		results.Clear();
		foreach (var vob in _all)
		{
			if (kind.HasValue && vob.Kind != kind.Value)
			{
				continue;
			}

			if (vob.Bounds.Contains(point) == ContainmentType.Contains)
			{
				results.Add(vob);
			}
		}
	}

	public void FindByKind(VobKind kind, List<VobNode> results)
	{
		results.Clear();
		foreach (var vob in _all)
		{
			if (vob.Kind == kind)
			{
				results.Add(vob);
			}
		}
	}

	public VobNode FindByName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		foreach (var vob in _all)
		{
			if (string.Equals(vob.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return vob;
			}
		}

		return null;
	}

	public VobNode FindByTag(string tag)
	{
		if (string.IsNullOrEmpty(tag))
		{
			return null;
		}

		foreach (var vob in _all)
		{
			if (string.Equals(vob.Tag, tag, StringComparison.OrdinalIgnoreCase))
			{
				return vob;
			}
		}

		return null;
	}

	public int CountOf(VobKind kind)
	{
		var count = 0;
		foreach (var vob in _all)
		{
			if (vob.Kind == kind)
			{
				++count;
			}
		}

		return count;
	}

	/// <summary>Squared distance from a point to a box; zero when the point is inside.</summary>
	public static float DistanceSquared(Vector3 point, BoundingBox box)
	{
		var dx = MathF.Max(MathF.Max(box.Min.X - point.X, point.X - box.Max.X), 0f);
		var dy = MathF.Max(MathF.Max(box.Min.Y - point.Y, point.Y - box.Max.Y), 0f);
		var dz = MathF.Max(MathF.Max(box.Min.Z - point.Z, point.Z - box.Max.Z), 0f);
		return dx * dx + dy * dy + dz * dz;
	}
}
