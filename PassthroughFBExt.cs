using StereoKit;
using StereoKit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Android.App.Assist.AssistStructure;

namespace PicoSKDemo;

class PassthroughFBExt : IStepper
{
    bool extAvailable;
    bool enabled;
    bool enabledPassthrough;
    bool enableOnInitialize;
    bool passthroughRunning;
    XrPassthroughFB activePassthrough = new XrPassthroughFB();
    XrPassthroughLayerFB activeLayer = new XrPassthroughLayerFB();

    Color oldColor;
    bool oldSky;

    Tex passthroughLayerTexture = null;
    Pose windowPose = new Pose(-.4f, 0, 0, Quat.LookDir(1, 0, 1));

    public bool Available => extAvailable;
    public bool Enabled { get => extAvailable && enabled; set => enabled = value; }
    public bool EnabledPassthrough
    {
        get => enabledPassthrough; set
        {
            if (Available && enabledPassthrough != value)
            {
                enabledPassthrough = value;
                if (enabledPassthrough) StartPassthrough();
                if (!enabledPassthrough) EndPassthrough();
            }
        }
    }

    public PassthroughFBExt() : this(true) { }
    public PassthroughFBExt(bool enabled = true)
    {
        if (SK.IsInitialized)
            Log.Err("PassthroughFBExt must be constructed before StereoKit is initialized!");
        Backend.OpenXR.RequestExt("XR_FB_passthrough");
        enableOnInitialize = enabled;
    }

    public bool Initialize()
    {
        extAvailable =
            Backend.XRType == BackendXRType.OpenXR &&
            Backend.OpenXR.ExtEnabled("XR_FB_passthrough") &&
            LoadBindings();

        if (enableOnInitialize)
            EnabledPassthrough = true;
        return true;
    }
    long stepCount = 0;
    public void Step()
    {
        stepCount++;
        if (!EnabledPassthrough) return;

        XrCompositionLayerPassthroughFB layer = new XrCompositionLayerPassthroughFB(
            XrCompositionLayerFlags.BLEND_TEXTURE_SOURCE_ALPHA_BIT, activeLayer);
        Backend.OpenXR.AddCompositionLayer(layer, -1);

        if (passthroughLayerTexture != null)
        {
            UI.WindowBegin("Window", ref windowPose, new Vec2(20, 0) * U.cm, UIWin.Normal);
            var sprite = Sprite.FromTex(passthroughLayerTexture);
            UI.Image(sprite, new Vec2(18, 0) * U.cm);
            UI.WindowEnd();
        }

        var height = (float)SK.System.displayHeight;
        var width = (float)SK.System.displayWidth * 2;
        var projection = Matrix.Perspective(90, width / height, 0.02f, 50f);

        if (stepCount % 60 == 0) { 
            Renderer.Screenshot((dataPtr, outWidth, outHeight) =>
            {
                unsafe { 
                    var colors = new ReadOnlySpan<Color32>(dataPtr.ToPointer(), outWidth * outHeight);

                    // below doesn't need unsafe, but is slower
                    //var colors = new Color32[outWidth * outHeight];
                    //var colorBytes = new byte[colors.Length * 4];
                    //Marshal.Copy(dataPtr, colorBytes, 0, colorBytes.Length);
                    //for (int i = 0; i < colors.Length; i++)
                    //{
                    //    var offset = i * 4;
                    //    colors[i] = new Color32(
                    //        colorBytes[offset + 0],
                    //        colorBytes[offset + 1],
                    //        colorBytes[offset + 2],
                    //        colorBytes[offset + 3]);
                    //}

                    passthroughLayerTexture = Tex.FromColors(colors.ToArray(), outWidth, outHeight, true);
                }
            }, camera: Renderer.CameraRoot, projection: projection, width: (int)width / 8, height: (int)height / 8, layerFilter: RenderLayer.Layer1);
        }
    }

    public void Shutdown()
    {
        EnabledPassthrough = false;
    }

    void StartPassthrough()
    {
        if (!extAvailable) return;
        if (passthroughRunning) return;
        passthroughRunning = true;

        oldColor = Renderer.ClearColor;
        oldSky = Renderer.EnableSky;

        XrResult result = xrCreatePassthroughFB(
            Backend.OpenXR.Session,
            new XrPassthroughCreateInfoFB(XrPassthroughFlagsFB.IS_RUNNING_AT_CREATION_BIT_FB),
            out activePassthrough);

        result = xrCreatePassthroughLayerFB(
            Backend.OpenXR.Session,
            new XrPassthroughLayerCreateInfoFB(activePassthrough, XrPassthroughFlagsFB.IS_RUNNING_AT_CREATION_BIT_FB, XrPassthroughLayerPurposeFB.RECONSTRUCTION_FB),
            out activeLayer);

        Renderer.ClearColor = Color.BlackTransparent;
        Renderer.EnableSky = false;
        result = xrPassthroughStartFB(activePassthrough);
        result = xrPassthroughLayerResumeFB(activeLayer);

    }

    void EndPassthrough()
    {
        if (!passthroughRunning) return;
        passthroughRunning = false;

        xrPassthroughPauseFB(activePassthrough);
        xrDestroyPassthroughLayerFB(activeLayer);
        xrDestroyPassthroughFB(activePassthrough);

        Renderer.ClearColor = oldColor;
        Renderer.EnableSky = oldSky;
    }

    static void WriteBitmap(Stream stream, int width, int height, Color32[] imageData)
    {
        using (BinaryWriter bw = new BinaryWriter(stream))
        {
            // define the bitmap file header
            bw.Write((UInt16)0x4D42);                               // bfType;
            bw.Write((UInt32)(14 + 40 + (width * height * 4)));     // bfSize;
            bw.Write((UInt16)0);                                    // bfReserved1;
            bw.Write((UInt16)0);                                    // bfReserved2;
            bw.Write((UInt32)14 + 40);                              // bfOffBits;

            // define the bitmap information header
            bw.Write((UInt32)40);                                   // biSize;
            bw.Write((Int32)width);                                 // biWidth;
            bw.Write((Int32)height);                                // biHeight;
            bw.Write((UInt16)1);                                    // biPlanes;
            bw.Write((UInt16)32);                                   // biBitCount;
            bw.Write((UInt32)0);                                    // biCompression;
            bw.Write((UInt32)(width * height * 4));                 // biSizeImage;
            bw.Write((Int32)0);                                     // biXPelsPerMeter;
            bw.Write((Int32)0);                                     // biYPelsPerMeter;
            bw.Write((UInt32)0);                                    // biClrUsed;
            bw.Write((UInt32)0);                                    // biClrImportant;

            // switch the image data from RGB to BGR
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = x + ((height - 1) - y) * width;
                    bw.Write(imageData[i].b);
                    bw.Write(imageData[i].g);
                    bw.Write(imageData[i].r);
                    bw.Write(imageData[i].a);
                }
            }
        }
    }

    #region OpenXR native bindings and types
    enum XrStructureType : UInt64
    {
        XR_TYPE_PASSTHROUGH_CREATE_INFO_FB = 1000118001,
        XR_TYPE_PASSTHROUGH_LAYER_CREATE_INFO_FB = 1000118002,
        XR_TYPE_PASSTHROUGH_STYLE_FB = 1000118020,
        XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_FB = 1000118003,
    }
    enum XrPassthroughFlagsFB : UInt64
    {
        None = 0,
        IS_RUNNING_AT_CREATION_BIT_FB = 0x00000001
    }
    enum XrCompositionLayerFlags : UInt64
    {
        None = 0,
        CORRECT_CHROMATIC_ABERRATION_BIT = 0x00000001,
        BLEND_TEXTURE_SOURCE_ALPHA_BIT = 0x00000002,
        UNPREMULTIPLIED_ALPHA_BIT = 0x00000004,
    }
    enum XrPassthroughLayerPurposeFB : UInt32
    {
        RECONSTRUCTION_FB = 0,
        PROJECTED_FB = 1,
        TRACKED_KEYBOARD_HANDS_FB = 1000203001,
        MAX_ENUM_FB = 0x7FFFFFFF,
    }
    enum XrResult : UInt32
    {
        Success = 0,
    }

#pragma warning disable 0169 // handle is not "used", but required for interop
    struct XrPassthroughFB { ulong handle; }
    struct XrPassthroughLayerFB { ulong handle; }
#pragma warning restore 0169

    [StructLayout(LayoutKind.Sequential)]
    struct XrPassthroughCreateInfoFB
    {
        private XrStructureType type;
        public IntPtr next;
        public XrPassthroughFlagsFB flags;

        public XrPassthroughCreateInfoFB(XrPassthroughFlagsFB passthroughFlags)
        {
            type = XrStructureType.XR_TYPE_PASSTHROUGH_CREATE_INFO_FB;
            next = IntPtr.Zero;
            flags = passthroughFlags;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct XrPassthroughLayerCreateInfoFB
    {
        private XrStructureType type;
        public IntPtr next;
        public XrPassthroughFB passthrough;
        public XrPassthroughFlagsFB flags;
        public XrPassthroughLayerPurposeFB purpose;

        public XrPassthroughLayerCreateInfoFB(XrPassthroughFB passthrough, XrPassthroughFlagsFB flags, XrPassthroughLayerPurposeFB purpose)
        {
            type = XrStructureType.XR_TYPE_PASSTHROUGH_LAYER_CREATE_INFO_FB;
            next = IntPtr.Zero;
            this.passthrough = passthrough;
            this.flags = flags;
            this.purpose = purpose;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct XrPassthroughStyleFB
    {
        public XrStructureType type;
        public IntPtr next;
        public float textureOpacityFactor;
        public Color edgeColor;
        public XrPassthroughStyleFB(float textureOpacityFactor, Color edgeColor)
        {
            type = XrStructureType.XR_TYPE_PASSTHROUGH_STYLE_FB;
            next = IntPtr.Zero;
            this.textureOpacityFactor = textureOpacityFactor;
            this.edgeColor = edgeColor;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct XrCompositionLayerPassthroughFB
    {
        public XrStructureType type;
        public IntPtr next;
        public XrCompositionLayerFlags flags;
        public ulong space;
        public XrPassthroughLayerFB layerHandle;
        public XrCompositionLayerPassthroughFB(XrCompositionLayerFlags flags, XrPassthroughLayerFB layerHandle)
        {
            type = XrStructureType.XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_FB;
            next = IntPtr.Zero;
            space = 0;
            this.flags = flags;
            this.layerHandle = layerHandle;
        }
    }

    delegate XrResult del_xrCreatePassthroughFB(ulong session, [In] XrPassthroughCreateInfoFB createInfo, out XrPassthroughFB outPassthrough);
    delegate XrResult del_xrDestroyPassthroughFB(XrPassthroughFB passthrough);
    delegate XrResult del_xrPassthroughStartFB(XrPassthroughFB passthrough);
    delegate XrResult del_xrPassthroughPauseFB(XrPassthroughFB passthrough);
    delegate XrResult del_xrCreatePassthroughLayerFB(ulong session, [In] XrPassthroughLayerCreateInfoFB createInfo, out XrPassthroughLayerFB outLayer);
    delegate XrResult del_xrDestroyPassthroughLayerFB(XrPassthroughLayerFB layer);
    delegate XrResult del_xrPassthroughLayerPauseFB(XrPassthroughLayerFB layer);
    delegate XrResult del_xrPassthroughLayerResumeFB(XrPassthroughLayerFB layer);
    delegate XrResult del_xrPassthroughLayerSetStyleFB(XrPassthroughLayerFB layer, [In] XrPassthroughStyleFB style);

    del_xrCreatePassthroughFB xrCreatePassthroughFB;
    del_xrDestroyPassthroughFB xrDestroyPassthroughFB;
    del_xrPassthroughStartFB xrPassthroughStartFB;
    del_xrPassthroughPauseFB xrPassthroughPauseFB;
    del_xrCreatePassthroughLayerFB xrCreatePassthroughLayerFB;
    del_xrDestroyPassthroughLayerFB xrDestroyPassthroughLayerFB;
    del_xrPassthroughLayerPauseFB xrPassthroughLayerPauseFB;
    del_xrPassthroughLayerResumeFB xrPassthroughLayerResumeFB;
    del_xrPassthroughLayerSetStyleFB xrPassthroughLayerSetStyleFB;

    bool LoadBindings()
    {
        xrCreatePassthroughFB = Backend.OpenXR.GetFunction<del_xrCreatePassthroughFB>("xrCreatePassthroughFB");
        xrDestroyPassthroughFB = Backend.OpenXR.GetFunction<del_xrDestroyPassthroughFB>("xrDestroyPassthroughFB");
        xrPassthroughStartFB = Backend.OpenXR.GetFunction<del_xrPassthroughStartFB>("xrPassthroughStartFB");
        xrPassthroughPauseFB = Backend.OpenXR.GetFunction<del_xrPassthroughPauseFB>("xrPassthroughPauseFB");
        xrCreatePassthroughLayerFB = Backend.OpenXR.GetFunction<del_xrCreatePassthroughLayerFB>("xrCreatePassthroughLayerFB");
        xrDestroyPassthroughLayerFB = Backend.OpenXR.GetFunction<del_xrDestroyPassthroughLayerFB>("xrDestroyPassthroughLayerFB");
        xrPassthroughLayerPauseFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerPauseFB>("xrPassthroughLayerPauseFB");
        xrPassthroughLayerResumeFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerResumeFB>("xrPassthroughLayerResumeFB");
        xrPassthroughLayerSetStyleFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerSetStyleFB>("xrPassthroughLayerSetStyleFB");

        return
            xrCreatePassthroughFB != null &&
            xrDestroyPassthroughFB != null &&
            xrPassthroughStartFB != null &&
            xrPassthroughPauseFB != null &&
            xrCreatePassthroughLayerFB != null &&
            xrDestroyPassthroughLayerFB != null &&
            xrPassthroughLayerPauseFB != null &&
            xrPassthroughLayerResumeFB != null &&
            xrPassthroughLayerSetStyleFB != null;
    }
    #endregion
}
