using Microsoft.Xna.Framework;
using OpenGothic.Vobs;
using System.Collections.Generic;
using Xunit;

namespace OpenGothic.Tests;

public class VobTreeTests
{
	private static BoundingBox Box(Vector3 position) =>
		new BoundingBox(position - new Vector3(10f), position + new Vector3(10f));

	private static VobNode Node(VobKind kind, string name, string tag, Vector3 position, VobNode parent = null) =>
		new VobNode(kind, name, tag, position, Box(position), parent);

	[Fact]
	public void FindNear_ReturnsNodesWithinRadius()
	{
		var tree = new VobTree();
		var near = Node(VobKind.Npc, "near", "PC_HERO", new Vector3(0f, 0f, 0f));
		var far = Node(VobKind.Npc, "far", "PC_HERO", new Vector3(500f, 0f, 0f));
		tree.Add(near);
		tree.Add(far);

		var results = new List<VobNode>();
		tree.FindNear(Vector3.Zero, 100f, results);

		Assert.Single(results);
		Assert.Same(near, results[0]);
	}

	[Fact]
	public void FindNear_FiltersByKindAndTag()
	{
		var tree = new VobTree();
		var npc = Node(VobKind.Npc, "npc", "PC_HERO", new Vector3(10f, 0f, 0f));
		var item = Node(VobKind.Item, "item", "ITMW_1H_SWORD", new Vector3(20f, 0f, 0f));
		tree.Add(npc);
		tree.Add(item);

		var results = new List<VobNode>();
		tree.FindNear(Vector3.Zero, 100f, results, VobKind.Item);
		Assert.Single(results);
		Assert.Same(item, results[0]);

		tree.FindNear(Vector3.Zero, 100f, results, tag: "PC_HERO");
		Assert.Single(results);
		Assert.Same(npc, results[0]);
	}

	[Fact]
	public void FindAt_ReturnsContainingBounds()
	{
		var tree = new VobTree();
		var vob = Node(VobKind.Interactive, "chest", "CHESTBIG", new Vector3(100f, 0f, 0f));
		tree.Add(vob);

		var results = new List<VobNode>();
		tree.FindAt(new Vector3(105f, 0f, 0f), results);
		Assert.Single(results);
		Assert.Same(vob, results[0]);

		tree.FindAt(new Vector3(500f, 0f, 0f), results);
		Assert.Empty(results);
	}

	[Fact]
	public void FindByNameAndTag_AreCaseInsensitive()
	{
		var tree = new VobTree();
		var vob = Node(VobKind.StartPoint, "START_HARBOUR", "NW_CITY_START", Vector3.Zero);
		tree.Add(vob);

		Assert.Same(vob, tree.FindByName("start_harbour"));
		Assert.Same(vob, tree.FindByTag("nw_city_start"));
		Assert.Null(tree.FindByName("missing"));
	}

	[Fact]
	public void ChildrenAndRoots_KeepTheHierarchy()
	{
		var tree = new VobTree();
		var parent = Node(VobKind.Static, "parent", "PARENT", Vector3.Zero);
		var child = Node(VobKind.Item, "child", "CHILD", new Vector3(10f, 0f, 0f), parent);
		tree.Add(parent);
		tree.Add(child);

		Assert.Single(parent.Children);
		Assert.Same(child, parent.Children[0]);
		Assert.Same(parent, child.Parent);
		Assert.Single(tree.Roots);
		Assert.Equal(2, tree.Count);
	}

	[Fact]
	public void CountOf_CountsByKind()
	{
		var tree = new VobTree();
		tree.Add(Node(VobKind.Npc, "a", "A", Vector3.Zero));
		tree.Add(Node(VobKind.Npc, "b", "B", Vector3.Zero));
		tree.Add(Node(VobKind.Item, "c", "C", Vector3.Zero));

		Assert.Equal(2, tree.CountOf(VobKind.Npc));
		Assert.Equal(1, tree.CountOf(VobKind.Item));
		Assert.Equal(0, tree.CountOf(VobKind.Trigger));
	}

	[Fact]
	public void DistanceSquared_IsZeroInsideBox()
	{
		var box = Box(new Vector3(100f, 0f, 0f));

		Assert.Equal(0f, VobTree.DistanceSquared(new Vector3(100f, 0f, 0f), box));
		Assert.Equal(90f * 90f, VobTree.DistanceSquared(new Vector3(0f, 0f, 0f), box));
	}
}
