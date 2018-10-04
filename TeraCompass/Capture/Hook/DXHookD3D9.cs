using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Capture.GUI;
using Capture.Interface;
using SharpDX.Direct3D9;
using ImGuiNET;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capture.TeraModule;
using Capture.TeraModule.CameraFinder;
using Capture.TeraModule.GameModels;
using Capture.TeraModule.Processing;
using Capture.TeraModule.Settings;
using Capture.TeraModule.ViewModels;
using SharpDX;
using TeraCompass.Processing;
using Vector2 = System.Numerics.Vector2;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;
using Color = System.Drawing.Color;

namespace Capture.Hook
{
    internal class DXHookD3D9 : BaseDXHook
    {
        public DXHookD3D9(CaptureInterface ssInterface)
            : base(ssInterface)
        {
        }

        Hook<Direct3D9Device_EndSceneDelegate> Direct3DDevice_EndSceneHook = null;
        Hook<Direct3D9Device_ResetDelegate> Direct3DDevice_ResetHook = null;
        Hook<Direct3D9Device_PresentDelegate> Direct3DDevice_PresentHook = null;
        Hook<Direct3D9DeviceEx_PresentExDelegate> Direct3DDeviceEx_PresentExHook = null;
        object _lockRenderTarget = new object();
        Stopwatch PerfomanseTester = new Stopwatch();
        private TimeSpan Elapsed = TimeSpan.Zero;

        Query _query;
        SharpDX.Direct3D9.Font _font;

        Surface _renderTargetCopy;
        Surface _resolvedTarget;

        public ImGuiRender imGuiRender;
        
        protected override string HookName => "DXHookD3D9";

        List<IntPtr> id3dDeviceFunctionAddresses = new List<IntPtr>();
        //List<IntPtr> id3dDeviceExFunctionAddresses = new List<IntPtr>();
        const int D3D9_DEVICE_METHOD_COUNT = 119;
        const int D3D9Ex_DEVICE_METHOD_COUNT = 15;
        bool _supportsDirect3D9Ex = false;
        private Process CurrentProcess { get; set; }

        public override void Hook()
        {
            this.DebugMessage("Hook: Begin");
            // First we need to determine the function address for IDirect3DDevice9
            id3dDeviceFunctionAddresses = new List<IntPtr>();
            using (Direct3D d3d = new Direct3D())
            {
                using (var renderForm = new System.Windows.Forms.Form())
                {
                    Device device;
                    using (device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle}))
                    {
                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
                    }
                }
            }

            try
            {
                using (Direct3DEx d3dEx = new Direct3DEx())
                {
                    using (var renderForm = new System.Windows.Forms.Form())
                    {
                        using (var deviceEx = new DeviceEx(d3dEx, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle}, new DisplayModeEx() {Width = 800, Height = 600}))
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
                {
                    Direct3DDeviceEx_PresentExHook = new Hook<Direct3D9DeviceEx_PresentExDelegate>(
                        id3dDeviceFunctionAddresses[(int) Direct3DDevice9ExFunctionOrdinals.PresentEx],
                        new Direct3D9DeviceEx_PresentExDelegate(PresentExHook),
                        this);
                }

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
            CurrentProcess = Process.GetProcessById(ProcessId);
            DebugListener listen = new DebugListener(Interface);
            Debug.Listeners.Add(listen);
            Services.Tracker.Configure(Services.CompassSettings).Apply();
            DebugMessage("Settings loaded");
            TeraSniffer.Instance.Enabled = true;
            TeraSniffer.Instance.Warning += DebugMessage;
            PacketProcessor.Instance.Connected += s => { Debug.Write("Connected"); };
        }

        /// <summary>
        /// Just ensures that the surface we created is cleaned up.
        /// </summary>
        public override void Cleanup()
        {
            lock (_lockRenderTarget)
            {

                
                RemoveAndDispose(ref _renderTargetCopy);

                if(imGuiRender!=null)
                    RemoveAndDispose(ref imGuiRender);
                RemoveAndDispose(ref _resolvedTarget);
                RemoveAndDispose(ref _query);


                RemoveAndDispose(ref _font);
            }
        }

        /// <summary>
        /// The IDirect3DDevice9.EndScene function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9Device_EndSceneDelegate(IntPtr device);

        /// <summary>
        /// The IDirect3DDevice9.Reset function definition
        /// </summary>
        /// <param name="device"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9Device_ResetDelegate(IntPtr device, ref PresentParameters presentParameters);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        unsafe delegate int Direct3D9Device_PresentDelegate(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        unsafe delegate int Direct3D9DeviceEx_PresentExDelegate(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags);

        
        /// <summary>
        /// Reset the _renderTarget so that we are sure it will have the correct presentation parameters (required to support working across changes to windowed/fullscreen or resolution changes)
        /// </summary>
        /// <param name="devicePtr"></param>
        /// <param name="presentParameters"></param>
        /// <returns></returns>
        int ResetHook(IntPtr devicePtr, ref PresentParameters presentParameters)
        {
            Cleanup();
            var hresult = Direct3DDevice_ResetHook.Original(devicePtr, ref presentParameters);
            var win32error= Result.GetResultFromWin32Error(hresult);
            if(win32error.Failure)
                Debug.Write($"{win32error} hresult={hresult}");
            
            return hresult;
        }

        bool _isUsingPresent = false;

        // Used in the overlay
        unsafe int PresentExHook(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags)
        {
            _isUsingPresent = true;
            DeviceEx device = (DeviceEx) devicePtr;

            DoCaptureRenderTarget(device, "PresentEx");

            return Direct3DDeviceEx_PresentExHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion, dwFlags);
        }

        unsafe int PresentHook(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            _isUsingPresent = true;

            Device device = (Device) devicePtr;

            DoCaptureRenderTarget(device, "PresentHook");

            return Direct3DDevice_PresentHook.Original(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
        }

        /// <summary>
        /// Hook for IDirect3DDevice9.EndScene
        /// </summary>
        /// <param name="devicePtr">Pointer to the IDirect3DDevice9 instance. Note: object member functions always pass "this" as the first parameter.</param>
        /// <returns>The HRESULT of the original EndScene</returns>
        /// <remarks>Remember that this is called many times a second by the Direct3D application - be mindful of memory and performance!</remarks>
        int EndSceneHook(IntPtr devicePtr)
        {
            Device device = (Device) devicePtr;

            if (!_isUsingPresent)
                DoCaptureRenderTarget(device, "EndSceneHook");

            return Direct3DDevice_EndSceneHook.Original(devicePtr);
        }


        
        /// <summary>
        /// Implementation of capturing from the render target of the Direct3D9 Device (or DeviceEx)
        /// </summary>
        /// <param name="device"></param>
        unsafe void DoCaptureRenderTarget(Device device, string hook)
        {
           
            if (CaptureThisFrame)
                #region CompasRenderLoop
                try
                {
                    if (imGuiRender == null && (device != null && device.NativePointer != IntPtr.Zero))
                    {
                        Debug.Write("Creating ImGui");
                        IntPtr handle = CurrentProcess.MainWindowHandle;
                        NativeMethods.Rect rect = new NativeMethods.Rect();
                        NativeMethods.GetWindowRect(handle, ref rect);
                        imGuiRender = ToDispose(new ImGuiRender(device, rect, Interface, handle));
                    }
                    else if(imGuiRender!=null)
                    {
                        if (Services.CompassSettings.ShowFPS)
                        {
                            PerfomanseTester.Reset();
                            PerfomanseTester.Start();
                        }
                        imGuiRender.GetNewFrame();

                        var CompassViewModel = PacketProcessor.Instance?.CompassViewModel;
                        CompassViewModel?.Render();

                        //if (UIState.SettingsOpened)
                        //    ImGuiNative.igShowDemoWindow(ref UIState.OverlayOpened);
                        if (Services.CompassSettings.ShowFPS)
                        {
                            var draw_list = ImGui.GetOverlayDrawList();
                            draw_list.AddText(new Vector2(10,10), $"PerfomanseTester = {Elapsed}", Color.Red.ToDx9ARGB());
                        }
                            
                        imGuiRender.Draw();
                        if (Services.CompassSettings.ShowFPS && PerfomanseTester.IsRunning)
                        {
                            PerfomanseTester.Stop();
                            Elapsed = PerfomanseTester.Elapsed;
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugMessage(e.ToString());
                }

            #endregion
        }
    }
}
