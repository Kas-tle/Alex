using Alex.ResourcePackLib.Json.Models.Entities;
using Microsoft.Xna.Framework;

namespace Alex.Graphics.Models.Entity.BlockEntities
{
	public class SkullBlockEntityModel : EntityModel
	{
		public SkullBlockEntityModel()
		{
			Description = new ModelDescription()
			{
				Identifier = "geometry.alex.skull", TextureHeight = 32, TextureWidth = 64
			};

			Bones = new[]
			{
				new EntityModelBone()
				{
					Name = "head",
					Pivot = Vector3.Zero,
					Cubes = new[]
					{
						new EntityModelCube()
						{
							Origin = new Vector3(-4f, 0f, -4f),
							Size = new Vector3(8f, 8f, 8f),
							Uv = Vector2.Zero
						},
					}
				}
			};
		}
	}
}