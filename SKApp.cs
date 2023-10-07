using Android.Icu.Number;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PicoSKDemo.Scenes;
using PicoSKDemo.Services.Abstractions;
using StereoKit;
using System;
using System.Threading.Tasks;

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
    Solid floorSolid;
    PassthroughFBExt passthrough;
	Model trainModel;// = Model.FromFile("Assets/train.glb");

    private StaticScene staticScene;
    private BakedScene bakedScene;
    private float bakedSamples = 16;
    private Pose bakeSettingsPose = new Pose(0, 0, -0.5f, Quat.LookDir(0, 0, 1));

    private Mesh floorMesh;
    private Material floorMaterial;
    private Mesh cubeMesh;
    private Material cubeMaterial;



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
        //cube = Model.FromMesh(
        //	Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.02f),
        //	Default.MaterialUI);
        //      trainModel = Model.FromFile("pallet_conveyor_model_3.glb");
        //      floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
        //      floorMaterial.Transparency = Transparency.Blend;
        //      floorSolid = new Solid(World.HasBounds ? World.BoundsPose.position : new Vec3(0, -1.5f, 0), Quat.Identity, SolidType.Immovable);

        floorMesh = Mesh.GeneratePlane(V.XY(10, 10), 20);
        cubeMesh = Mesh.GenerateCube(Vec3.One, 8);
        floorMaterial = Material.Default.Copy();
        floorMaterial[MatParamName.DiffuseTex] = Tex.FromFile("test.png");
        cubeMaterial = Material.Default.Copy();
        cubeMaterial[MatParamName.DiffuseTex] = Tex.FromFile("floor.png");

        staticScene = GenerateScene();

        bakedScene = new BakedScene();
        bakedScene.AddDirectionalLight(V.XYZ(-1, -1, -1), Color.White, 1);
        bakedScene.SetSky(Renderer.SkyLight);
        bakedScene.Bake(staticScene, 0);
        Task.Run(() => bakedScene.Bake(staticScene, (int)bakedSamples));


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

    private StaticScene GenerateScene()
    {
        float height = -1.6f;
        if (World.HasBounds)
            height = World.BoundsPose.position.y;

        Random r = new Random();
        StaticScene result = new StaticScene();
        result.AddMesh(floorMesh, floorMaterial, Matrix.T(0, height, 0));
        for (int i = 0; i < 20; i++)
        {
            Vec3 at = new Vec3(r.NextSingle() * 10 - 5, r.NextSingle() * 4 + height, r.NextSingle() * 10 - 5);
            Vec3 scale = new Vec3(r.NextSingle() * 3 + 0.5f, r.NextSingle() * 3 + 0.5f, r.NextSingle() * 3 + 0.5f);
            result.AddMesh(cubeMesh, cubeMaterial, Matrix.TS(
                at + V.XYZ(0, scale.y / 2.0f, 0), scale));
        }

        return result;
    }

    public void Step()
	{
        bakedScene.Draw();
        DrawWindowBakeSettings();

        //if (SK.System.displayType == Display.Opaque)
        //	Default.MeshCube.Draw(floorMaterial, floorTransform);

        //UI.Handle("Cube", ref cubePose, cube.Bounds);
        //      cube.Draw(cubePose.ToMatrix());

        //      //trainModel.Draw(Matrix.TRS(new Vec3(-37.0f, -2.0f, 6.0f), Quat.FromAngles(0, 0, 0), 1.0f));

        //      if (World.HasBounds)
        //      {
        //          Vec2 s = World.BoundsSize / 2;
        //          Matrix pose = World.BoundsPose.ToMatrix();
        //          Vec3 tl = pose.Transform(new Vec3(s.x, 0, s.y));
        //          Vec3 br = pose.Transform(new Vec3(-s.x, 0, -s.y));
        //          Vec3 tr = pose.Transform(new Vec3(-s.x, 0, s.y));
        //          Vec3 bl = pose.Transform(new Vec3(s.x, 0, -s.y));

        //          Lines.Add(tl, tr, Color.White, 1.5f * U.cm);
        //          Lines.Add(bl, br, Color.White, 1.5f * U.cm);
        //          Lines.Add(tl, bl, Color.White, 1.5f * U.cm);
        //          Lines.Add(tr, br, Color.White, 1.5f * U.cm);
        //      }
    }

    
    private void DrawWindowBakeSettings()
    {
        UI.WindowBegin("Bake Settings", ref bakeSettingsPose);
        UI.Label("Samples"); UI.SameLine(); UI.HSlider("Samples", ref bakedSamples, 0, 512, 1);

        UI.PushEnabled(!bakedScene.Baking);
        if (UI.Button("Regenerate"))
        {
            staticScene = GenerateScene();
            bakedScene.Bake(staticScene, 0);
        }

        UI.SameLine();
        if (UI.Button("Bake"))
            Task.Run(() => bakedScene.Bake(staticScene, (int)bakedSamples));

        UI.PopEnabled();
        if (bakedScene.Baking)
        {
            UI.SameLine();
            UI.Label("Baking...");
            UI.ProgressBar(bakedScene.BakingProgress);
        }
        UI.WindowEnd();
    }


}