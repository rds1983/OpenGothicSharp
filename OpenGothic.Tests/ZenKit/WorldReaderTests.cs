using OpenGothic.ZenKit;
using System.Linq;
using System.Numerics;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class WorldReaderTests
{
	[Fact]
	public void ReadsAVobTreeAndWayNetFromABinSafeArchive()
	{
		var root = new BinSafeBuilder.Obj { Name = "main", ClassName = "oCWorld:zCWorld", Index = 0 };

		var vob = new BinSafeBuilder.Obj
		{
			Name = "%",
			ClassName = "oCMobInter:oCMOB:zCVob",
			Index = 1
		};
		vob.Fields.Add(BinSafeBuilder.Obj.Int("pack", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.String("presetName", ""));
		vob.Fields.Add(BinSafeBuilder.Obj.String("vobName", "TestBench"));
		vob.Fields.Add(BinSafeBuilder.Obj.String("visual", "BENCH_TEST.ASC"));
		vob.Fields.Add(BinSafeBuilder.Obj.RawFloat("bbox3DWS",
			new[] { 90f, 190f, 290f, 110f, 210f, 310f }));
		vob.Fields.Add(BinSafeBuilder.Obj.RawFloat("trafoOSToWSRot",
			new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f }));
		vob.Fields.Add(BinSafeBuilder.Obj.Vec3("trafoOSToWSPos", new Vector3(100, 200, 300)));
		vob.Fields.Add(BinSafeBuilder.Obj.Bool("showVisual", true));
		vob.Fields.Add(BinSafeBuilder.Obj.Enum("visualCamAlign", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.Enum("visualAniMode", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.Float("visualAniModeStrength", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.Float("vobFarClipZScale", 1));
		vob.Fields.Add(BinSafeBuilder.Obj.Bool("cdStatic", false));
		vob.Fields.Add(BinSafeBuilder.Obj.Bool("cdDyn", true));
		vob.Fields.Add(BinSafeBuilder.Obj.Bool("staticVob", false));
		vob.Fields.Add(BinSafeBuilder.Obj.Enum("dynShadow", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.Int("zbias", 0));
		vob.Fields.Add(BinSafeBuilder.Obj.Bool("isAmbient", false));
		vob.Fields.Add(BinSafeBuilder.Obj.String("focusName", "BENCH"));
		vob.Fields.Add(BinSafeBuilder.Obj.Int("stateNum", 2));
		vob.Fields.Add(BinSafeBuilder.Obj.String("triggerTarget", ""));
		vob.Fields.Add(BinSafeBuilder.Obj.String("useWithItem", "ITKEY"));
		vob.Fields.Add(BinSafeBuilder.Obj.String("conditionFunc", ""));
		vob.Fields.Add(BinSafeBuilder.Obj.String("onStateFunc", "BENCH_ON"));
		vob.Children.Add(new BinSafeBuilder.Obj
		{
			Name = "visual",
			ClassName = "zCProgMeshProto",
			Index = 2
		});

		var vobTree = new BinSafeBuilder.Obj { Name = "VobTree", ClassName = "%", Index = 3 };
		vobTree.Fields.Add(BinSafeBuilder.Obj.Int("childs0", 1));
		vobTree.Children.Add(vob);
		vobTree.Fields.Add(BinSafeBuilder.Obj.Int("childs1", 0));

		var pointA = new BinSafeBuilder.Obj { Name = "A", ClassName = "zCWaypoint", Index = 4 };
		pointA.Fields.Add(BinSafeBuilder.Obj.String("wpName", "A"));
		pointA.Fields.Add(BinSafeBuilder.Obj.Int("waterDepth", 2));
		pointA.Fields.Add(BinSafeBuilder.Obj.Bool("underWater", true));
		pointA.Fields.Add(BinSafeBuilder.Obj.Vec3("position", new Vector3(1, 2, 3)));
		pointA.Fields.Add(BinSafeBuilder.Obj.Vec3("direction", new Vector3(0, 0, 1)));

		var pointB = new BinSafeBuilder.Obj { Name = "B", ClassName = "zCWaypoint", Index = 5 };
		pointB.Fields.Add(BinSafeBuilder.Obj.String("wpName", "B"));
		pointB.Fields.Add(BinSafeBuilder.Obj.Int("waterDepth", 0));
		pointB.Fields.Add(BinSafeBuilder.Obj.Bool("underWater", false));
		pointB.Fields.Add(BinSafeBuilder.Obj.Vec3("position", new Vector3(4, 5, 6)));
		pointB.Fields.Add(BinSafeBuilder.Obj.Vec3("direction", new Vector3(0, 1, 0)));

		var pointARef = new BinSafeBuilder.Obj
		{
			Name = "A",
			IsReference = true,
			ReferenceIndex = pointA.Index
		};

		var zWayNet = new BinSafeBuilder.Obj { Name = "zCWayNet", ClassName = "zCWayNet", Index = 6 };
		zWayNet.Fields.Add(BinSafeBuilder.Obj.Int("waynetVersion", 1));
		zWayNet.Fields.Add(BinSafeBuilder.Obj.Int("numWaypoints", 1));
		zWayNet.Fields.Add(BinSafeBuilder.Obj.Int("numWays", 1));
		zWayNet.Children.Add(pointA);
		zWayNet.Children.Add(pointB);
		zWayNet.Children.Add(pointARef);

		var wayNet = new BinSafeBuilder.Obj { Name = "WayNet", ClassName = "%", Index = 7 };
		wayNet.Children.Add(zWayNet);

		root.Children.Add(vobTree);
		root.Children.Add(wayNet);
		root.Children.Add(new BinSafeBuilder.Obj { Name = "EndMarker", ClassName = "%", Index = 8 });

		var world = WorldReader.Load(new MemoryRead(BinSafeBuilder.Build(root)));

		var one = Assert.Single(world.RootObjects);
		Assert.Equal(VirtualObjectType.oCMobInter, one.Type);
		Assert.Equal("TestBench", one.Name);
		Assert.Equal(new Vector3(100, 200, 300), one.Position);
		Assert.Equal(new Vector3(90, 190, 290), one.BoundingBox.Min);
		Assert.Equal(new Vector3(110, 210, 310), one.BoundingBox.Max);
		Assert.True(one.ShowVisual);
		Assert.True(one.CdDynamic);
		Assert.Equal(VisualType.MultiResolutionMesh, one.Visual.Type);
		Assert.Equal("BENCH_TEST.ASC", one.Visual.Name);

		var interactive = Assert.IsType<InteractiveObject>(one);
		Assert.Equal(2, interactive.State);
		Assert.Equal("BENCH_ON", interactive.OnStateChangeFunction);
		Assert.Equal("ITKEY", interactive.Item);
		Assert.Equal("BENCH", interactive.FocusName);

		Assert.NotNull(world.WayNet);
		var way = world.WayNet;
		Assert.Equal(new[] { "A", "B" }, way.Points.Select(p => p.Name));
		Assert.True(way.Points[0].FreePoint);
		Assert.False(way.Points[1].FreePoint);
		Assert.Equal(new Vector3(1, 2, 3), way.Points[0].Position);
		Assert.Equal(new Vector3(4, 5, 6), way.Points[1].Position);
		var edge = Assert.Single(way.Edges);
		Assert.Equal((1, 0), edge);
	}
}
