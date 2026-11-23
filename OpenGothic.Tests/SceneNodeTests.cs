using Microsoft.Xna.Framework;
using OpenGothic.Scene;
using Xunit;

namespace OpenGothic.Tests;

public sealed class SceneNodeTests
{
	[Fact]
	public void WorldTransform_WithoutParent_IsTheLocalTransform()
	{
		var node = new SceneNode
		{
			Transform = Matrix.CreateTranslation(1, 2, 3)
		};

		Assert.Equal(node.Transform, node.WorldTransform);
	}

	[Fact]
	public void WorldTransform_ComposesTheParentChain()
	{
		// Parent: rotate 90 degrees about Y, then move 10 along X.
		var parent = new SceneNode
		{
			Transform = Matrix.CreateRotationY(MathHelper.PiOver2) * Matrix.CreateTranslation(10, 0, 0)
		};

		var child = new SceneNode
		{
			Transform = Matrix.CreateTranslation(0, 5, 0)
		};

		parent.Children.Add(child);

		Assert.Equal(new Vector3(10, 5, 0), Vector3.Transform(Vector3.Zero, child.WorldTransform));
	}

	[Fact]
	public void AddingAChild_SetsItsParent_AndRemovingClearsIt()
	{
		var parent = new SceneNode();
		var child = new SceneNode();

		parent.Children.Add(child);
		Assert.Same(parent, child.Parent);

		parent.Children.Remove(child);
		Assert.Null(child.Parent);
	}

	[Fact]
	public void AddingToASecondParent_DetachesFromTheFirst()
	{
		var first = new SceneNode();
		var second = new SceneNode();
		var child = new SceneNode();

		first.Children.Add(child);
		second.Children.Add(child);

		Assert.Empty(first.Children);
		Assert.Same(second, child.Parent);
	}

	[Fact]
	public void MovingAParent_MovesItsChild()
	{
		var parent = new SceneNode();
		var child = new SceneNode
		{
			Transform = Matrix.CreateTranslation(2, 0, 0)
		};

		parent.Children.Add(child);

		// Populate the cached world transform first: the move has to invalidate it.
		Assert.Equal(new Vector3(2, 0, 0), Vector3.Transform(Vector3.Zero, child.WorldTransform));

		parent.Transform = Matrix.CreateTranslation(5, 0, 0);

		Assert.Equal(new Vector3(7, 0, 0), Vector3.Transform(Vector3.Zero, child.WorldTransform));
	}

	[Fact]
	public void MovingAGrandparent_MovesAGrandchild()
	{
		var root = new SceneNode();
		var middle = new SceneNode { Transform = Matrix.CreateTranslation(3, 0, 0) };
		var leaf = new SceneNode { Transform = Matrix.CreateTranslation(1, 0, 0) };

		root.Children.Add(middle);
		middle.Children.Add(leaf);

		Assert.Equal(new Vector3(4, 0, 0), Vector3.Transform(Vector3.Zero, leaf.WorldTransform));

		root.Transform = Matrix.CreateTranslation(10, 0, 0);

		Assert.Equal(new Vector3(14, 0, 0), Vector3.Transform(Vector3.Zero, leaf.WorldTransform));
	}

	[Fact]
	public void ChangingTheLocalTransform_UpdatesTheWorldTransform()
	{
		var node = new SceneNode
		{
			Transform = Matrix.CreateTranslation(1, 0, 0)
		};

		Assert.Equal(new Vector3(1, 0, 0), Vector3.Transform(Vector3.Zero, node.WorldTransform));

		node.Transform = Matrix.CreateTranslation(2, 0, 0);

		Assert.Equal(new Vector3(2, 0, 0), Vector3.Transform(Vector3.Zero, node.WorldTransform));
	}

	[Fact]
	public void Transform_FromGlobal_RecomposesToTheSamePlace()
	{
		// How a vob child is stored: Gothic keeps the world transform, so the loader derives a
		// local one against the parent. Composing must return the original world transform.
		var parentWorld = Matrix.CreateRotationY(0.7f) * Matrix.CreateTranslation(100, 20, -30);
		var childWorld = Matrix.CreateRotationY(-0.3f) * Matrix.CreateTranslation(120, 25, -10);

		var parent = new SceneNode { Transform = parentWorld };
		var child = new SceneNode { Transform = childWorld * Matrix.Invert(parentWorld) };
		parent.Children.Add(child);

		var composed = child.WorldTransform;
		var expected = Vector3.Transform(new Vector3(1, 2, 3), childWorld);
		var actual = Vector3.Transform(new Vector3(1, 2, 3), composed);

		Assert.True((expected - actual).Length() < 0.01f, $"expected {expected}, got {actual}");
	}
}
