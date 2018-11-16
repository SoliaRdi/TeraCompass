using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Capture.GUI;
using Capture.Interface;
using Capture.TeraModule.Processing;
using Capture.TeraModule.Settings;
using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D9;
using TeraCompass.Processing;
using Color = System.Drawing.Color;
using Font = SharpDX.Direct3D9.Font;
using Rectangle = SharpDX.Rectangle;
using Vector2 = System.Numerics.Vector2;

namespace Capture.Hook
{
    internal class DXHookD3D9 : BaseDXHook
    {
        private const int D3D9_DEVICE_METHOD_COUNT = 119;
        private const int D3D9Ex_DEVICE_METHOD_COUNT = 15;

        private bool _isUsingPresent;
        private readonly object _lockRenderTarget = new object();

        private bool _supportsDirect3D9Ex;

        private Hook<Direct3D9Device_EndSceneDelegate> Direct3DDevice_EndSceneHook;
        private Hook<Direct3D9Device_PresentDelegate> Direct3DDevice_PresentHook;
        private Hook<Direct3D9Device_ResetDelegate> Direct3DDevice_ResetHook;
        private Hook<Direct3D9DeviceEx_PresentExDelegate> Direct3DDeviceEx_PresentExHook;
        private TimeSpan Elapsed = TimeSpan.Zero;

        private List<IntPtr> id3dDeviceFunctionAddresses = new List<IntPtr>();

        public ImGuiRender imGuiRender;
        private readonly Stopwatch PerfomanseTester = new Stopwatch();

        Sprite _sprite;
        public static Dictionary<string, Texture> _imageCache = new Dictionary<string, Texture>();
        public DXHookD3D9(CaptureInterface ssInterface)
            : base(ssInterface)
        {
        }

        protected override string HookName => "DXHookD3D9";
        private Process CurrentProcess { get; set; }

        public override void Hook()
        {
            CurrentProcess = Process.GetProcessById(ProcessId);
            TraceListener listen = new DebugListener(Interface);
            Trace.Listeners.Add(listen);
            Services.Tracker.Configure(Services.CompassSettings).Apply();
            DebugMessage("Settings loaded");

           
            DebugMessage("Hook: Begin");

            // First we need to determine the function address for IDirect3DDevice9
            id3dDeviceFunctionAddresses = new List<IntPtr>();
            using (var d3d = new Direct3D())
            {
                using (var renderForm = new Form())
                {
                    Device device;
                    using (device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing,
                        new PresentParameters {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle}))
                    {
                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
                    }
                }
            }

            try
            {
                using (var d3dEx = new Direct3DEx())
                {
                    using (var renderForm = new Form())
                    {
                        using (var deviceEx = new DeviceEx(d3dEx, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing,
                            new PresentParameters {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle},
                            new DisplayModeEx {Width = 800, Height = 600}))
                        {
                            id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(deviceEx.NativePointer, D3D9_DEVICE_METHOD_COUNT, D3D9Ex_DEVICE_METHOD_COUNT));
                            _supportsDirect3D9Ex = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                _supportsDirect3D9Ex = false;
            }

            // We want to hook each method of the IDirect3DDevice9 interface that we are interested in

            // 42 - EndScene (we will retrieve the back buffer here)
            Direct3DDevice_EndSceneHook = new Hook<Direct3D9Device_EndSceneDelegate>(
                id3dDeviceFunctionAddresses[(int) Direct3DDevice9FunctionOrdinals.EndScene],
                new Direct3D9Device_EndSceneDelegate(EndSceneHook),
                this);

            unsafe
            {
                // If Direct3D9Ex is available - hook the PresentEx
                if (_supportsDirect3D9Ex)
                    Direct3DDeviceEx_PresentExHook = new Hook<Direct3D9DeviceEx_PresentExDelegate>(
                        id3dDeviceFunctionAddresses[(int) Direct3DDevice9ExFunctionOrdinals.PresentEx],
                        new Direct3D9DeviceEx_PresentExDelegate(PresentExHook),
                        this);

                // Always hook Present also (device will only call Present or PresentEx not both)
                Direct3DDevice_PresentHook = new Hook<Direct3D9Device_PresentDelegate>(
                    id3dDeviceFunctionAddresses[(int) Direct3DDevice9FunctionOrdinals.Present],
                    new Direct3D9Device_PresentDelegate(PresentHook),
                    this);
            }

            // 16 - Reset (called on resolution change or windowed/fullscreen change - we will reset some things as well)
            Direct3DDevice_ResetHook = new Hook<Direct3D9Device_ResetDelegate>(
                id3dDeviceFunctionAddresses[(int) Direct3DDevice9FunctionOrdinals.Reset],
                new Direct3D9Device_ResetDelegate(ResetHook),
                this);

            /*
             * Don't forget that all hooks will start deactivated...
             * The following ensures that all threads are intercepted:
             * Note: you must do this for each hook.
             */

            Direct3DDevice_EndSceneHook.Activate();
            Hooks.Add(Direct3DDevice_EndSceneHook);

            Direct3DDevice_PresentHook.Activate();
            Hooks.Add(Direct3DDevice_PresentHook);

            if (_supportsDirect3D9Ex)
            {
                Direct3DDeviceEx_PresentExHook.Activate();
                Hooks.Add(Direct3DDeviceEx_PresentExHook);
            }

            Direct3DDevice_ResetHook.Activate();
            Hooks.Add(Direct3DDevice_ResetHook);

            DebugMessage("Hook: End");

            

        }

        /// <summary>
        ///     Just ensures that the surface we created is cleaned up.
        /// </summary>
        public override void Cleanup()
        {
            lock (_lockRenderTarget)
            {
                try { 
                    _sprite.End();
                }
                catch (Exception)
                {
                    // ignored
                }

                if (imGuiRender != null)
                    RemoveAndDispose(ref imGuiRender);
            }
        }


        /// <summary>
        ///     Reset the _renderTarget so that we are sure it will have the correct presentation parameters (required to support
        ///     working across changes to windowed/fullscreen or resolution changes)
        /// </summary>
        /// <param name="devicePtr"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        private int ResetHook(IntPtr devicePtr, ref PresentParameters presentParameters)
        {
            Cleanup();
            _sprite?.OnLostDevice();
            var hresult = Direct3DDevice_ResetHook.Original(devicePtr, ref presentParameters);
            var win32error = Result.GetResultFromWin32Error(hresult);
            if (win32error.Failure)
                Trace.Write($"{win32error} hresult={hresult}");

            return hresult;
        }

        // Used in the overlay
        private unsafe int PresentExHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags)
        {
            _isUsingPresent = true;
            var device = (DeviceEx) devicePtr;

            DoCaptureRenderTarget(device, "PresentEx");

            return Direct3DDeviceEx_PresentExHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion, dwFlags);
        }

        private unsafe int PresentHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            _isUsingPresent = true;

            var device = (Device) devicePtr;

            DoCaptureRenderTarget(device, "PresentHook");

            return Direct3DDevice_PresentHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
        }

        /// <summary>
        ///     Hook for IDirect3DDevice9.EndScene
        /// </summary>
        /// <param name="devicePtr">
        ///     Pointer to the IDirect3DDevice9 instance. Note: object member functions always pass "this" as
        ///     the first parameter.
        /// </param>
        /// <returns>The HRESULT of the original EndScene</returns>
        /// <remarks>
        ///     Remember that this is called many times a second by the Direct3D application - be mindful of memory and
        ///     performance!
        /// </remarks>
        private int EndSceneHook(IntPtr devicePtr)
        {
            var device = (Device) devicePtr;

            if (!_isUsingPresent)
                DoCaptureRenderTarget(device, "EndSceneHook");

            return Direct3DDevice_EndSceneHook.Original(devicePtr);
        }

        /// <summary>
        ///     Implementation of capturing from the render target of the Direct3D9 Device (or DeviceEx)
        /// </summary>
        /// <param name="device"></param>
        private void DoCaptureRenderTarget(Device device, string hook)
        {
            FPS.Frame();
            if (CaptureThisFrame)

                #region CompasRenderLoop

                try
                {
                    if (imGuiRender == null && device != null && device.NativePointer != IntPtr.Zero)
                    {
                        Trace.Write("Creating ImGui");
                        var handle = CurrentProcess.MainWindowHandle;
                        var rect = new NativeMethods.Rect();
                        NativeMethods.GetWindowRect(handle, ref rect);
                        _sprite = ToDispose(new Sprite(device));
                        IntialiseElementResources(device);
                        imGuiRender = ToDispose(new ImGuiRender(device, rect, Interface, CurrentProcess));

                    }
                    else if (imGuiRender != null)
                    {
                        if (Services.CompassSettings.ShowRenderTime)
                        {
                            PerfomanseTester.Reset();
                            PerfomanseTester.Start();
                        }else if(PerfomanseTester.IsRunning)
                            PerfomanseTester.Stop();

                        _sprite.Begin(SpriteFlags.AlphaBlend);
                        
                        imGuiRender.GetNewFrame();

                        var CompassViewModel = PacketProcessor.Instance?.CompassViewModel;
                        CompassViewModel?.Render(_sprite);
                        ImGui.ShowDemoWindow();
                        if (Services.CompassSettings.ShowRenderTime)
                        {
                            var draw_list = ImGui.GetOverlayDrawList();
                            draw_list.AddText(new Vector2(10, 100), Color.Red.ToDx9ARGB(), $"RenderingTime(ms) = {Elapsed.Milliseconds}");
                        }
                        if (Services.CompassSettings.ShowFPS)
                        {
                            ImGui.SetNextWindowBgAlpha(0);
                            
                            if (ImGui.Begin("FPS counter",ref Services.CompassSettings._showFps, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
                            {
                                ImGui.SetWindowFontScale(1.3F);
                                ImGui.Text($"{FPS.GetFPS():n0} fps");
                            }
                            ImGui.End();
                        }
                        _sprite.End();
                        imGuiRender.Draw();
                        
                        if (Services.CompassSettings.ShowRenderTime && PerfomanseTester.IsRunning)
                        {
                            PerfomanseTester.Stop();
                            Elapsed = PerfomanseTester.Elapsed;
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugMessage(e.ToString());
                    _sprite.End();
                }

            #endregion
        }
        private void IntialiseElementResources(Device device)
        {
            foreach (var image in BasicTeraData.Instance.Icons)
            {
               GetImageForImageElement(image,device);
            }
        }
      unsafe void GetImageForImageElement(ImageElement element, Device device)
        {
            if (!string.IsNullOrEmpty(element.Filename))
            {
                var path = Path.GetFileName(element.Filename);
                if (!_imageCache.TryGetValue(path, out var tex))
                {
                    tex = ToDispose(Texture.FromFile(device, element.Filename));
                   
                    _imageCache[path] = tex;
                }
            }
        }
        /// <summary>
        ///     The IDirect3DDevice9.EndScene function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int Direct3D9Device_EndSceneDelegate(IntPtr device);

        /// <summary>
        ///     The IDirect3DDevice9.Reset function definition
        /// </summary>
        /// <param name="device"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int Direct3D9Device_ResetDelegate(IntPtr device, ref PresentParameters presentParameters);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private unsafe delegate int Direct3D9Device_PresentDelegate(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride,
            IntPtr pDirtyRegion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private unsafe delegate int Direct3D9DeviceEx_PresentExDelegate(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect, IntPtr hDestWindowOverride,
            IntPtr pDirtyRegion, Present dwFlags);
    }
}