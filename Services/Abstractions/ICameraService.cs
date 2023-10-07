using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicoSKDemo.Services.Abstractions
{
    public interface ICameraService
    {
        void InitCamera();
        void StartCamera();
    }
}
