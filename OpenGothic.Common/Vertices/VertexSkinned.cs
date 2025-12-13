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

		public Vector3 Position0;
		public Vector3 Position1;
		public Vector3 Position2;
		public Vector3 Position3;
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
						VertexElementUsage.Position,
						1
					),
					new VertexElement(
						24,
						VertexElementFormat.Vector3,
						VertexElementUsage.Position,
						2
					),
					new VertexElement(
						36,
						VertexElementFormat.Vector3,
						VertexElementUsage.Position,
						3
					),
					new VertexElement(
						48,
						VertexElementFormat.Vector3,
						VertexElementUsage.Normal,
						0
					),
					new VertexElement(
						60,
						VertexElementFormat.Vector2,
						VertexElementUsage.TextureCoordinate,
						0
					),
					new VertexElement(
						68,
						VertexElementFormat.Byte4,
						VertexElementUsage.BlendIndices,
						0
					),
					new VertexElement(
						72,
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
			Vector3 position0,
			Vector3 position1,
			Vector3 position2,
			Vector3 position3,
			Vector3 normal,
			Vector2 textureCoordinate,
			Byte4 blendIndices,
			Vector4 blendWeights
		)
		{
			Position0 = position0;
			Position1 = position1;
			Position2 = position2;
			Position3 = position3;
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
				"{{Position0:" + Position0.ToString() +
				" Position1:" + Position1.ToString() +
				" Position2:" + Position2.ToString() +
				" Position3:" + Position3.ToString() +
				" Normal:" + Normal.ToString() +
				" TextureCoordinate:" + TextureCoordinate.ToString() +
				" BlendIndices:" + BlendIndices.ToString() +
				" BlendWeights:" + BlendWeights.ToString() +
				"}}"
			);
		}

		public static bool operator ==(VertexSkinned left, VertexSkinned right)
		{
			return ((left.Position0 == right.Position0) &&
					(left.Position1 == right.Position1) &&
					(left.Position2 == right.Position2) &&
					(left.Position3 == right.Position3) &&
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
