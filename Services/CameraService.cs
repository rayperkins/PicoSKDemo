using Android.Content;
using Android.Hardware.Camera2;
using PicoSKDemo.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicoSKDemo.Services
{
    public class CameraService : ICameraService
    {
        private readonly Context _androidContext;

        public CameraService(Context androidContext)
        {
            _androidContext = androidContext;
        }

        public void InitCamera()
        {
            
            var cameraManager = (CameraManager)_androidContext.GetSystemService(Context.CameraService);

            try
            {
                var cameraList = cameraManager.GetCameraIdList();
                foreach(var camera in cameraList) 
                { 
                
                }

            }
            catch(Exception ex) 
            { 
            
            }
        }

        public void StartCamera()
        {
            throw new NotImplementedException();
        }
    }
}
