using Android.Icu.Number;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PicoSKDemo.Services.Abstractions;
using StereoKit;
using System;

namespace PicoSKDemo;

public class SKApp
{
    ILogger _logger;
    IServiceProvider _serviceProvider;
    ICameraService _cameraService;

    public SKSettings Settings => new SKSettings {
		appName           = "PicoSKDemo",
		assetsFolder      = "assets",
		displayPreference = DisplayMode.MixedReality
	};

	Pose  cubePose = new Pose(0, 0, -0.5f, Quat.Identity);
	Model cube;
	Matrix   floorTransform = Matrix.TS(new Vec3(0, -1.5f, 0), new Vec3(30, 0.1f, 30));
	Material floorMaterial;
    Solid floorSolid;
    PassthroughFBExt passthrough;
	Model trainModel;// = Model.FromFile("Assets/train.glb");



    public SKApp(
		ILogger<SKApp> logger,
		IServiceProvider serviceProvider,
        ICameraService cameraService)
	{
		_logger = logger;
        _serviceProvider = serviceProvider;
		_cameraService = cameraService;
        //passthrough = SK.AddStepper<PassthroughFBExt>();
    }

    public void Init()
	{
		// Create assets used by the app
		cube = Model.FromMesh(
			Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.02f),
			Default.MaterialUI);
        trainModel = Model.FromFile("pallet_conveyor_model_3.glb");
        floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
        floorMaterial.Transparency = Transparency.Blend;
        floorSolid = new Solid(World.HasBounds ? World.BoundsPose.position : new Vec3(0, -1.5f, 0), Quat.Identity, SolidType.Immovable);

        if (!SK.System.worldOcclusionPresent)
		{
			_logger.LogWarning("worldOcclusionPresent not available");
        }

        if (!SK.System.worldRaycastPresent)
        {
            _logger.LogWarning("worldRaycastPresent not available");
        }

        //_cameraService.InitCamera();
    }

	public void Step()
	{
		if (SK.System.displayType == Display.Opaque)
			Default.MeshCube.Draw(floorMaterial, floorTransform);

		UI.Handle("Cube", ref cubePose, cube.Bounds);
        cube.Draw(cubePose.ToMatrix());

        trainModel.Draw(Matrix.TRS(new Vec3(-37.0f, -2.0f, 6.0f), Quat.FromAngles(0, 0, 0), 1.0f));

        if (World.HasBounds)
        {
            Vec2 s = World.BoundsSize / 2;
            Matrix pose = World.BoundsPose.ToMatrix();
            Vec3 tl = pose.Transform(new Vec3(s.x, 0, s.y));
            Vec3 br = pose.Transform(new Vec3(-s.x, 0, -s.y));
            Vec3 tr = pose.Transform(new Vec3(-s.x, 0, s.y));
            Vec3 bl = pose.Transform(new Vec3(s.x, 0, -s.y));

            Lines.Add(tl, tr, Color.White, 1.5f * U.cm);
            Lines.Add(bl, br, Color.White, 1.5f * U.cm);
            Lines.Add(tl, bl, Color.White, 1.5f * U.cm);
            Lines.Add(tr, br, Color.White, 1.5f * U.cm);
        }
    }


}