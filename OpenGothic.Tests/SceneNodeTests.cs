using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using Xunit;

namespace OpenGothic.Tests;

/// <summary>
/// A scene node is a visual, not a hierarchy: its transform is world-space and the vob tree
/// owns parenting. These tests keep that contract explicit.
/// </summary>
public sealed class SceneNodeTests
{
	[Fact]
	public void WorldTransform_IsTheVisualTransform()
	{
		var node = new SceneNode
		{
			Transform = Matrix.CreateTranslation(1, 2, 3)
		};

		Assert.Equal(node.Transform, node.WorldTransform);
	}

	[Fact]
	public void ChangingTheTransform_UpdatesTheWorldTransform()
	{
		var node = new SceneNode
		{
			Transform = Matrix.CreateTranslation(1, 0, 0)
		};

		Assert.Equal(new Vector3(1, 0, 0), Vector3.Transform(Vector3.Zero, node.WorldTransform));

		node.Transform = Matrix.CreateTranslation(2, 0, 0);

		Assert.Equal(new Vector3(2, 0, 0), Vector3.Transform(Vector3.Zero, node.WorldTransform));
	}
}
