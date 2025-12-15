using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Materials;
using System.Collections.Generic;
using ZenKit;

namespace OpenGothic
{
	partial class Assets
	{
		private World LoadWorld(GraphicsDevice device, string name)
		{
			var record = GetLastRecord(name);

			var result = new World(record.Node.Buffer);

			// Load materials
			var materials = new Dictionary<int, DefaultMaterial>();
			for(var i = 0; i < result.Mesh.Materials.Count; ++i)
			{
				var zkMaterial = result.Mesh.Materials[i];

				var texture = GetTexture(device, zkMaterial.Texture);

				var material = new DefaultMaterial
				{
					DiffuseTexture = texture
				};

				materials[i] = material;
			}
			
			return result;
		}

		public World GetWorld(GraphicsDevice device, string name) => Get(device, name, LoadWorld);
	}
}
