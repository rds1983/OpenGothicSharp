using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System.Runtime.InteropServices;

namespace OpenGothic.Vertices
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct VertexSkinned : IVertexType
	{
		#region Private Properties

		VertexDeclaration IVertexType.VertexDeclaration
		{
			get
			{
				return VertexDeclaration;
			}
		}

		#endregion

		#region Public Variables

		public Vector3 Position;
		public Vector3 Normal;
		public Vector2 TextureCoordinate;
		public Byte4 BlendIndices;
		public Vector4 BlendWeights;

		#endregion

		#region Public Static Variables

		public static readonly VertexDeclaration VertexDeclaration;

		#endregion

		#region Private Static Constructor

		static VertexSkinned()
		{
			VertexDeclaration = new VertexDeclaration(
				new VertexElement[]
				{
					new VertexElement(
						0,
						VertexElementFormat.Vector3,
						VertexElementUsage.Position,
						0
					),
					new VertexElement(
						12,
						VertexElementFormat.Vector3,
						VertexElementUsage.Normal,
						0
					),
					new VertexElement(
						24,
						VertexElementFormat.Vector2,
						VertexElementUsage.TextureCoordinate,
						0
					),
					new VertexElement(
						32,
						VertexElementFormat.Byte4,
						VertexElementUsage.BlendIndices,
						0
					),
					new VertexElement(
						36,
						VertexElementFormat.Vector4,
						VertexElementUsage.BlendWeight,
						0
					)
				}
			);
		}

		#endregion

		#region Public Constructor

		public VertexSkinned(
			Vector3 position,
			Vector3 normal,
			Vector2 textureCoordinate,
			Byte4 blendIndices,
			Vector4 blendWeights
		)
		{
			Position = position;
			Normal = normal;
			TextureCoordinate = textureCoordinate;
			BlendIndices = blendIndices;
			BlendWeights = blendWeights;
		}

		#endregion

		#region Public Static Operators and Override Methods

		public override int GetHashCode()
		{
			// TODO: Fix GetHashCode
			return 0;
		}

		public override string ToString()
		{
			return (
				"{{Position:" + Position.ToString() +
				" Normal:" + Normal.ToString() +
				" TextureCoordinate:" + TextureCoordinate.ToString() +
				" BlendIndices:" + BlendIndices.ToString() +
				" BlendWeights:" + BlendWeights.ToString() +
				"}}"
			);
		}

		public static bool operator ==(VertexSkinned left, VertexSkinned right)
		{
			return ((left.Position == right.Position) &&
					(left.Normal == right.Normal) &&
					(left.TextureCoordinate == right.TextureCoordinate) &&
					(left.BlendIndices == right.BlendIndices) &&
					(left.BlendWeights == right.BlendWeights));
		}

		public static bool operator !=(VertexSkinned left, VertexSkinned right)
		{
			return !(left == right);
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			if (obj.GetType() != base.GetType())
			{
				return false;
			}
			return (this == ((VertexSkinned)obj));
		}

		#endregion
	}
}
