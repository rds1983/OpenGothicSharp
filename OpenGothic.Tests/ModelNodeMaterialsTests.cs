using DigitalRiseModel;
using OpenGothic.Graphics;
using Xunit;

namespace OpenGothic.Tests;

public class ModelNodeMaterialsTests
{
	[Fact]
	public void GothicModel_SettingTheModelBuildsMaterialRows()
	{
		var model = new GothicModel();
		Assert.Null(model.Materials);

		model.Model = new DrModel(new DrModelBone("root"));

		Assert.NotNull(model.Materials);
		Assert.Empty(model.Materials);
	}

	[Fact]
	public void GothicModel_ReplacingTheInstanceRebuildsMaterials()
	{
		var model = new GothicModel(new DrModel(new DrModelBone("root")));
		var first = model.Materials;

		model.Instance = new DrModelInstance(new DrModel(new DrModelBone("root")));

		Assert.NotNull(model.Materials);
		Assert.NotSame(first, model.Materials);
	}

	[Fact]
	public void ModelNode_InitializesMaterialsFromTheModel()
	{
		var model = new DrModel(new DrModelBone("root"));
		var node = new ModelNode
		{
			Instance = new DrModelInstance(model)
		};

		Assert.NotNull(node.Materials);
		Assert.Empty(node.Materials);
	}

	[Fact]
	public void ModelNode_ChangingTheInstanceRebuildsMaterials()
	{
		var node = new ModelNode();
		Assert.Null(node.Materials);

		node.Instance = new DrModelInstance(new DrModel(new DrModelBone("root")));

		Assert.NotNull(node.Materials);
		Assert.Empty(node.Materials);
	}

	[Fact]
	public void BoneAttachment_ExposesOneRowForTheWrappedMesh()
	{
		var attachment = new ModelBoneAttachment(ModelBoneAttachment.WrapMesh(new DrMesh()));

		Assert.NotNull(attachment.Materials);
		Assert.Single(attachment.Materials);
		Assert.Empty(attachment.Materials[0]);
	}
}
