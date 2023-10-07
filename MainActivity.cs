using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Renderscripts;
using Android.Runtime;
using Android.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PicoSKDemo.Services;
using PicoSKDemo.Services.Abstractions;
using StereoKit;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace PicoSKDemo;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { "org.khronos.openxr.intent.category.IMMERSIVE_HMD", "com.oculus.intent.category.VR", Intent.CategoryLauncher })]
//public class MainActivity : Activity
//{
//    protected override void OnCreate(Bundle savedInstanceState)
//    {
//        base.OnCreate(savedInstanceState);
//        Run();
//        SetContentView(PicoSKDemo.Resource.Layout.activity_main);
//    }

//    protected override void OnDestroy()
//    {
//        SK.Quit();
//        base.OnDestroy();
//    }

//    static bool running = false;
//    void Run()
//    {
//        if (running) return;
//        running = true;

//        // Before anything else, give StereoKit the Activity. This should
//        // be set before any other SK calls, otherwise native library
//        // loading may fail.
//        SK.AndroidActivity = this;

//        Task.Run(() => {
//            var entryClass = typeof(Program);
//            MethodInfo entryPoint = entryClass?.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

//            // There are a number of potential method signatures for Main, so
//            // we need to check each one, and give it the correct values.
//            ParameterInfo[] entryParams = entryPoint?.GetParameters();
//            if (entryParams == null || entryParams.Length == 0)
//                entryPoint.Invoke(null, null);
//            else if (entryParams?.Length == 1 && entryParams[0].ParameterType == typeof(string[]))
//                entryPoint.Invoke(null, new object[] { new string[0] });
//            else
//                throw new Exception("Couldn't invoke Program.Main!");

//            Process.KillProcess(Process.MyPid());
//        });
//    }
//}

public class MainActivity : Activity, ISurfaceHolderCallback2
{
	public static readonly int REQUEST_CAMERA_PERMISSION = 1;
	SKApp app;
	Android.Views.View surface;
	IServiceProvider _serviceProvider;


	protected override void OnCreate(Bundle savedInstanceState)
	{
        base.OnCreate(savedInstanceState);

        SK.AndroidActivity = this;

        ServiceCollection services = new ServiceCollection();
		AddServices(services);

		_serviceProvider = services.BuildServiceProvider();

		Run(Handle);
	}

    protected override void OnDestroy()
    {
        SK.Quit();
        base.OnDestroy();
    }

    void AddServices(IServiceCollection services)
	{
		services.AddSingleton<Context>(this.ApplicationContext);
		services.AddSingleton<ICameraService, CameraService>();
		services.AddLogging((builder) =>
		{
			builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);

			builder.AddConsole();
		});
	}

	static bool running = false;
	void Run(IntPtr activityHandle)
	{
		if (running)
			return;
		running = true;

		Task.Run(() =>
		{
			// If the app has a constructor that takes a string array, then
			// we'll use that, and pass the command line arguments into it on
			// creation
			var app = ActivatorUtilities.CreateInstance<SKApp>(_serviceProvider);
			if (app == null)
				throw new System.Exception("StereoKit loader couldn't construct an instance of the App!");

			// Initialize StereoKit, and the app
			SKSettings settings = app.Settings;
			settings.androidActivity = activityHandle;
			if (!SK.Initialize(settings))
				return;
			app.Init();

			// Now loop until finished, and then shut down
			SK.Run(app.Step);

			Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
		});
	}

	// Events related to surface state changes
	public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height) => SK.SetWindow(holder.Surface.Handle);
	public void SurfaceCreated(ISurfaceHolder holder) => SK.SetWindow(holder.Surface.Handle);
	public void SurfaceDestroyed(ISurfaceHolder holder) => SK.SetWindow(IntPtr.Zero);
	public void SurfaceRedrawNeeded(ISurfaceHolder holder) { }
}