using Microsoft.Xna.Framework;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace OpenGothic.Scene;

/// <summary>
/// A node in a placed scene. <see cref="Transform"/> is local to <see cref="Parent"/>;
/// <see cref="WorldTransform"/> composes the chain, so moving a parent moves its descendants.
/// A node without a parent uses <see cref="Transform"/> directly, which is how world geometry
/// and command-line spawns are placed.
/// </summary>
/// <remarks>
/// <see cref="WorldTransform"/> is computed on demand and cached. Changing the local transform
/// invalidates the node and, through <see cref="Children"/>, every descendant, so a static
/// node is composed once and a move costs only the subtree that actually changed.
/// </remarks>
public class SceneNode
{
	private Matrix _transform = Matrix.Identity;
	private Matrix _world = Matrix.Identity;
	private bool _worldValid;
	private SceneNode _parent;

	public SceneNode()
	{
		Children.CollectionChanged += OnChildrenChanged;
	}

	public Matrix Transform
	{
		get => _transform;
		set
		{
			_transform = value;
			Invalidate();
		}
	}

	/// <summary>
	/// Child nodes. Adding one makes this its <see cref="Parent"/>; removing it detaches it.
	/// Remove children individually rather than calling <c>Clear</c>, which reports a reset
	/// without naming the items it dropped. The caller must not form a cycle.
	/// </summary>
	public ObservableCollection<SceneNode> Children { get; } = new ObservableCollection<SceneNode>();

	/// <summary>Node this one is placed relative to, or null for a world-space node.</summary>
	public SceneNode Parent => _parent;

	/// <summary>Transform from local space to world space, composing the parent chain.</summary>
	public Matrix WorldTransform
	{
		get
		{
			if (!_worldValid)
			{
				_world = _parent == null ? _transform : _transform * _parent.WorldTransform;
				_worldValid = true;
			}

			return _world;
		}
	}

	/// <summary>Drops the cached world transform for this node and its descendants.</summary>
	private void Invalidate()
	{
		_worldValid = false;

		foreach (var child in Children)
		{
			child.Invalidate();
		}
	}

	private void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
		{
			foreach (SceneNode child in e.OldItems)
			{
				if (ReferenceEquals(child._parent, this))
				{
					child._parent = null;
					child.Invalidate();
				}
			}
		}

		if (e.NewItems != null)
		{
			foreach (SceneNode child in e.NewItems)
			{
				var previous = child._parent;
				if (previous != null && !ReferenceEquals(previous, this))
				{
					previous.Children.Remove(child);
				}

				child._parent = this;
				child.Invalidate();
			}
		}
	}
}
