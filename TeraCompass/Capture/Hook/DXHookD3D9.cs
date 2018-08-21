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


        Query _query;
        SharpDX.Direct3D9.Font _font;

        Surface _renderTargetCopy;
        Surface _resolvedTarget;

        public ImGuiRender imGuiRender;
        Stopwatch PerfomanseTester = new Stopwatch();
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
            //id3dDeviceExFunctionAddresses = new List<IntPtr>();
            this.DebugMessage("Hook: Before device creation");
            using (Direct3D d3d = new Direct3D())
            {
                using (var renderForm = new System.Windows.Forms.Form())
                {
                    Device device;
                    using (device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle}))
                    {
                        this.DebugMessage("Hook: Device created");
                        id3dDeviceFunctionAddresses.AddRange(GetVTblAddresses(device.NativePointer, D3D9_DEVICE_METHOD_COUNT));
                    }
                }
            }

            try
            {
                using (Direct3DEx d3dEx = new Direct3DEx())
                {
                    this.DebugMessage("Hook: Direct3DEx...");
                    using (var renderForm = new System.Windows.Forms.Form())
                    {
                        using (var deviceEx = new DeviceEx(d3dEx, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() {BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle}, new DisplayModeEx() {Width = 800, Height = 600}))
                        {
                            this.DebugMessage("Hook: DeviceEx created - PresentEx supported");
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

            TeraSniffer.Instance.Enabled = true;
            TeraSniffer.Instance.Warning += DebugMessage;
            PacketProcessor.Instance.Connected += s => { Debug.Write("The connection is established"); };
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


        private TimeSpan Elapsed = TimeSpan.Zero;
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
                        if (UIState.ShowFPS)
                        {
                            PerfomanseTester.Reset();
                            PerfomanseTester.Start();
                        }

                        imGuiRender.GetNewFrame();
                        Vector2 window_pos = new Vector2((UIState.OverlayCorner == 1) ? ImGui.GetIO().DisplaySize.X - UIState.DISTANCE : UIState.DISTANCE, (UIState.OverlayCorner == 2) ? ImGui.GetIO().DisplaySize.Y - UIState.DISTANCE * 4 : UIState.DISTANCE * 4);
                        Vector2 window_pos_pivot = new Vector2((UIState.OverlayCorner == 1) ? 1.0f : 0.0f, (UIState.OverlayCorner == 2) ? 1.0f : 0.0f);
                        Vector2 window_size = new Vector2(300, 300);
                        var draw_list = ImGui.GetOverlayDrawList();
                        if (UIState.OverlayCorner != -1)
                            ImGui.SetNextWindowPos(window_pos, Condition.Always, window_pos_pivot);
                        
                        if (ImGui.BeginWindow("Overlay", ref UIState.OverlayOpened, window_size, 0.3f, (UIState.OverlayCorner != -1 ? WindowFlags.NoMove : 0) | WindowFlags.NoTitleBar | WindowFlags.NoResize | WindowFlags.NoFocusOnAppearing))
                        {
                            window_pos = ImGui.GetWindowPosition();
                            if (ImGuiNative.igBeginPopupContextWindow("Options", 1, true))
                            {
                                if (ImGui.MenuItem("Custom position", null, UIState.OverlayCorner == -1, true)) UIState.OverlayCorner = -1;
                                if (ImGui.MenuItem("Top right", null, UIState.OverlayCorner == 0, true)) UIState.OverlayCorner = 0;
                                if (ImGui.MenuItem("Settings", null, UIState.SettingsOpened, true)) UIState.SettingsOpened = !UIState.SettingsOpened;
                                
                                ImGuiNative.igEndPopup();
                            }
                            draw_list.AddLine(new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y), new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y + window_size.Y),Color.FromArgb(90, 70, 70, 255).ToDx9ARGB(), 1f);
                            draw_list.AddLine(new Vector2(window_pos.X, window_pos.Y + window_size.Y * 0.5f), new Vector2(window_pos.X + window_size.X, window_pos.Y + window_size.Y * 0.5f),Color.FromArgb(90, 70, 70, 255).ToDx9ARGB(), 1f);
                            
                            if (PacketProcessor.Instance?.CompassViewModel != null)
                            {
                                Vector2 dot1 = new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y + window_size.Y * 0.5f);
                                Vector2 dot2 = new Vector2(window_pos.X + window_size.X - 30, (window_pos.Y + window_size.Y * 0.5f));
                                var final = PacketProcessor.Instance.CompassViewModel.CameraScanner.CameraAddress != 0 ? CompassViewModel.RotatePoint(dot2, dot1, new Angle(PacketProcessor.Instance.CompassViewModel.CameraScanner.Angle()).Gradus - 90) : CompassViewModel.RotatePoint(dot2, dot1, PacketProcessor.Instance.EntityTracker.CompassUser.Heading.Gradus - 90);
                                draw_list.AddLine(dot1, final, (uint) Color.FromArgb(120, 255, 255, 255).ToDx9ARGB(), 1f);
                                
                                PlayerModel[] values = PacketProcessor.Instance.CompassViewModel.PlayerModels.Values.ToArray();
                                for (var i=0;i<values.Length;i++)
                                {
                                    if (UIState.CaptureOnlyEnemy && UIState.FriendlyTypes.Contains(values[i].Relation)) continue;
                                    if(UIState.FilterByClassess&& UIState.FilteredClasses.Contains(values[i].PlayerClass)) continue;
                                    if (!UIState.RelationColors.TryGetValue(values[i].Relation, out var color))
                                        UIState.RelationColors.TryGetValue(RelationType.Unknown, out color);
                                    var ScreenPosition = PacketProcessor.Instance.CompassViewModel.GetScreenPos(values[i]);
                                    draw_list.AddCircleFilled(new Vector2(window_pos.X + ScreenPosition.X, window_pos.Y + ScreenPosition.Y), UIState.PlayerSize, color.ToDx9ARGB(), UIState.PlayerSize*2);
                                    if (UIState.ShowNicknames)
                                        draw_list.AddText(new Vector2(window_pos.X + ScreenPosition.X - (values[i].Name.Length*4/2), window_pos.Y + ScreenPosition.Y+ UIState.PlayerSize), values[i].Name, color.ToDx9ARGB());
                                }
                            }
                        }
                        ImGui.EndWindow();
                        if (PacketProcessor.Instance?.CompassViewModel != null && PacketProcessor.Instance.CompassViewModel.PlayerModels.Count > 0)
                        {
                            if (UIState.StatisticsOpened)
                            {
                                var GuldList = PacketProcessor.Instance.CompassViewModel.PlayerModels.Values
                                    .ToArray()
                                    .GroupBy(x => x.GuildName.Length == 0 ? "Without Guild" : x.GuildName, (key, g) => new {GuildName = key, Players = g.ToList()})
                                    .OrderByDescending(x => x.Players.Count).ToHashSet();
                                if (GuldList.Count>0)
                                {
                                    ImGui.SetNextWindowPos(new Vector2(window_pos.X, window_pos.Y + window_size.Y), Condition.Always, window_pos_pivot);
                                    if (ImGui.BeginWindow("Guilds", ref UIState.OverlayOpened, new Vector2(350, 200), 0.3f, WindowFlags.NoTitleBar | WindowFlags.NoFocusOnAppearing))
                                    {
                                        ImGui.BeginChild("left pane", new Vector2(150, 0), true);

                                        foreach (var i in GuldList)
                                        {
                                            if (ImGui.Selectable($"{i.GuildName} ({i.Players.Count})", UIState.SelectedGuildName == i.GuildName))
                                            {
                                                UIState.SelectedGuildName = i.GuildName;
                                            }
                                        }

                                        ImGui.EndChild();
                                        ImGui.SameLine();
                                        ImGuiNative.igBeginGroup();
                                        ImGui.BeginChild("item view", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true); // Leave room for 1 line below us

                                        ImGui.TextUnformatted(($"Guild name {UIState.SelectedGuildName}\n"));
                                        ImGui.Columns(3, null, true);

                                        var players = GuldList.SingleOrDefault(x => x.GuildName == UIState.SelectedGuildName)?.Players?.GroupBy(x => x.PlayerClass, (key, g) => new {Class = key, Players = g.ToList()});
                                        if (players != null)
                                            foreach (var details in players)
                                            {
                                                if (ImGui.GetColumnIndex() == 0)
                                                    ImGui.Separator();
                                                ImGui.TextUnformatted($"{details.Class.ToString()} ({details.Players.Count})\n");

                                                if (details.Players?.Count > 0)
                                                {
                                                    foreach (var name in details.Players)
                                                    {
                                                        ImGui.TextUnformatted($"{name.Name}\n");
                                                    }
                                                }
                                                ImGui.NextColumn();
                                            }
                                        ImGui.Columns(1, null, true);

                                        ImGui.Separator();
                                        ImGui.EndChild();
                                        ImGuiNative.igEndGroup();
                                    }
                                    ImGui.EndWindow();
                                }
                            }
                        }
                        if (UIState.SettingsOpened)
                        {
                            if (ImGui.BeginWindow("Settings", ref UIState.SettingsOpened, new Vector2(350, 400), 0.3f, WindowFlags.NoFocusOnAppearing|WindowFlags.AlwaysAutoResize))
                            {
                                ImGui.Checkbox("Guild statistic", ref UIState.StatisticsOpened);
                                ImGui.Checkbox("Show only enemy players", ref UIState.CaptureOnlyEnemy);
                                ImGui.Checkbox("Filter by classes", ref UIState.FilterByClassess);
                                ImGui.Checkbox("Show nicknames", ref UIState.ShowNicknames);
                                ImGui.Checkbox("Perfomance test", ref UIState.ShowFPS);
                                ImGui.SliderFloat("Zoom", ref UIState.Zoom, 1, 20,$"Zoom={UIState.Zoom}",2f);
                                if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                                    ImGui.SetTooltip($"{UIState.Zoom:F2}");
                                ImGui.SliderInt("PlayerSize", ref UIState.PlayerSize, 1, 10, $"PlayerSize = {UIState.PlayerSize}");
                                if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                                    ImGui.SetTooltip($"{UIState.PlayerSize}");
                                if (ImGui.CollapsingHeader("Settings for filter by class            ", TreeNodeFlags.CollapsingHeader|TreeNodeFlags.AllowItemOverlap))
                                {
                                    ImGui.TextUnformatted("Common ignored");
                                    ImGui.Columns(3, null, false);
                                    foreach (PlayerClass i in Enum.GetValues(typeof(PlayerClass)))
                                    {
                                        bool flag = UIState.FilteredClasses.Contains(i);
                                        ImGui.Checkbox(i.ToString(), ref flag);
                                        if (flag)
                                            UIState.FilteredClasses.Add(i);
                                        else
                                            if (UIState.FilteredClasses.Contains(i))
                                            UIState.FilteredClasses.Remove(i);
                                        ImGui.NextColumn();
                                    }
                                    ImGui.Columns(1,null,false);
                                }
                                
                                if (ImGui.CollapsingHeader("Colors for player relation", TreeNodeFlags.CollapsingHeader))
                                {
                                    var keys = UIState.RelationColors.Keys.ToArray();
                                    for (int i = 0; i < keys.Length; i++)
                                    {
                                        UIState.RelationColors.TryGetValue(keys[i], out var color);
                                        UIState.R[i] = ((color >> 16) & 255) / 255f;
                                        UIState.G[i] = ((color >> 8) & 255) / 255f;
                                        UIState.B[i] = ((color >> 0) & 255) / 255f;
                                        UIState.A[i] = ((color >> 24) & 255) / 255f;
                                        ImGui.TextUnformatted(keys[i].ToString());
                                        ImGui.SameLine();
                                        ImGui.ColorEdit4(keys[i].ToString(), ref UIState.R[i], ref UIState.G[i], ref UIState.B[i], ref UIState.A[i], (ColorEditFlags.NoInputs | ColorEditFlags.RGB | ColorEditFlags.NoLabel));
                                        uint mr = UIState.R[i] >= 1.0 ? 255 : (UIState.R[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.R[i] * 255f)),
                                            mg = UIState.G[i] >= 1.0 ? 255 : (UIState.G[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.G[i] * 255f)),
                                            mb = UIState.B[i] >= 1.0 ? 255 : (UIState.B[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.B[i] * 255f)),
                                            ma = UIState.A[i] >= 1.0 ? 255 : (UIState.A[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.A[i] * 255f));
                                        UIState.RelationColors[keys[i]] = ((ma << 24) | (mr << 16) | (mg << 8) | (mb << 0));
                                    }
                                }
                            }
                            ImGui.EndWindow();
                        }
                        //if (UIState.SettingsOpened)
                        //    ImGuiNative.igShowDemoWindow(ref UIState.OverlayOpened);
                        if(UIState.ShowFPS)
                            draw_list.AddText(window_pos, $"PerfomanseTester = {Elapsed}", Color.Red.ToDx9ARGB());
                        imGuiRender.Draw();
                        if (UIState.ShowFPS && PerfomanseTester.IsRunning)
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
