using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.App;
using Java.Lang;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PicoSKDemo.Platforms.Android.Services;
using PicoSKDemo.Services.Abstractions;
using StereoKit;
using System;
using System.Threading.Tasks;

namespace PicoSKDemo;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { "org.khronos.openxr.intent.category.IMMERSIVE_HMD", "com.oculus.intent.category.VR", Intent.CategoryLauncher })]
public class MainActivity : AppCompatActivity, ISurfaceHolderCallback2
{
    public static readonly int REQUEST_CAMERA_PERMISSION = 1;
    App                app;
	Android.Views.View surface;
	IServiceProvider _serviceProvider;


	protected override void OnCreate(Bundle savedInstanceState)
	{
		JavaSystem.LoadLibrary("openxr_loader");
		JavaSystem.LoadLibrary("StereoKitC");

		// Set up a surface for StereoKit to draw on
		Window.TakeSurface(this);
		Window.SetFormat(Format.Unknown);
		surface = new(this);
		SetContentView(surface);
		surface.RequestFocus();

		base.OnCreate(savedInstanceState);
		Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState);
        ServiceCollection services = new ServiceCollection();
		AddServices(services);

        _serviceProvider = services.BuildServiceProvider();

        RequestPermissions(new string[] { Android.Manifest.Permission.Camera }, REQUEST_CAMERA_PERMISSION);

        var cameraManager = (CameraManager)this.GetSystemService(Context.CameraService);

        var cameraList = cameraManager.GetCameraIdList();
        //var concurrentCameraIds = cameraManager.ConcurrentCameraIds;
        Run(Handle);
    }

	public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
	{
		Microsoft.Maui.ApplicationModel.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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

		Task.Run(() => {
			// If the app has a constructor that takes a string array, then
			// we'll use that, and pass the command line arguments into it on
			// creation
			var app = ActivatorUtilities.CreateInstance<App>(_serviceProvider);
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
	public void SurfaceChanged     (ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height) => SK.SetWindow(holder.Surface.Handle);
	public void SurfaceCreated     (ISurfaceHolder holder) => SK.SetWindow(holder.Surface.Handle);
	public void SurfaceDestroyed   (ISurfaceHolder holder) => SK.SetWindow(IntPtr.Zero);
	public void SurfaceRedrawNeeded(ISurfaceHolder holder) { }
}