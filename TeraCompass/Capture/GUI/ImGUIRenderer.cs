using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Capture.GUI.Vertex;
using Capture.Hook.Input;
using Capture.Interface;
using EasyHook;
using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics.Interop;
using SharpDX.Windows;

namespace Capture.GUI
{
    public class MouseState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool RMB { get; set; }
        public bool LMB { get; set; }
        public bool MMB { get; set; }
        public int Wheel { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMessage
    {
        public IntPtr handle;
        public uint msg;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public System.Drawing.Point p;
    }

    public class DeviceInfo
    {
        public string deviceName;
        public string deviceType;
        public IntPtr deviceHandle;
        public string Name;
        public string source;
        public ushort key;
        public string vKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        [MarshalAs(UnmanagedType.U4)] public int dwType;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RAWINPUT
    {
        [FieldOffset(0)] public RAWINPUTHEADER header;
        [FieldOffset(16)] public RAWMOUSE mouse;
        [FieldOffset(16)] public RAWKEYBOARD keyboard;
        [FieldOffset(16)] public RAWHID hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTHEADER
    {
        [MarshalAs(UnmanagedType.U4)] public int dwType;
        [MarshalAs(UnmanagedType.U4)] public int dwSize;
        public IntPtr hDevice;
        [MarshalAs(UnmanagedType.U4)] public int wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWHID
    {
        [MarshalAs(UnmanagedType.U4)] public int dwSizHid;
        [MarshalAs(UnmanagedType.U4)] public int dwCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BUTTONSSTR
    {
        [MarshalAs(UnmanagedType.U2)] public ushort usButtonFlags;
        [MarshalAs(UnmanagedType.U2)] public ushort usButtonData;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RAWMOUSE
    {
        [MarshalAs(UnmanagedType.U2)] [FieldOffset(0)] public ushort usFlags;
        [MarshalAs(UnmanagedType.U4)] [FieldOffset(4)] public uint ulButtons;
        [FieldOffset(4)] public BUTTONSSTR buttonsStr;
        [MarshalAs(UnmanagedType.U4)] [FieldOffset(8)] public uint ulRawButtons;
        [FieldOffset(12)] public int lLastX;
        [FieldOffset(16)] public int lLastY;
        [MarshalAs(UnmanagedType.U4)] [FieldOffset(20)] public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWKEYBOARD
    {
        [MarshalAs(UnmanagedType.U2)] public ushort MakeCode;
        [MarshalAs(UnmanagedType.U2)] public ushort Flags;
        [MarshalAs(UnmanagedType.U2)] public ushort Reserved;
        [MarshalAs(UnmanagedType.U2)] public ushort VKey;
        [MarshalAs(UnmanagedType.U4)] public uint Message;
        [MarshalAs(UnmanagedType.U4)] public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTDEVICE
    {
        [MarshalAs(UnmanagedType.U2)] public ushort usUsagePage;
        [MarshalAs(UnmanagedType.U2)] public ushort usUsage;
        [MarshalAs(UnmanagedType.U4)] public int dwFlags;
        public IntPtr hwndTarget;
    };

    public static class RawMouseButtons
    {
        public const uint
            RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001,
            RI_MOUSE_LEFT_BUTTON_UP = 0x0002,
            RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010,
            RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020,
            RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004,
            RI_MOUSE_RIGHT_BUTTON_UP = 0x0008,
            RI_MOUSE_WHEEL = 0x0400;
    }

    public static class RawDeviceType
    {
        public const int
            RIM_TYPEHID = 2,
            RIM_TYPEKEYBOARD = 1,
            RIM_TYPEMOUSE = 0;
    }

    internal class ImGuiRender:IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate uint inputHookDelegate(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e);

        private Texture texNative;
        public readonly Device device;
        private MouseState mouseState = new MouseState();
        private int _wheelPosition = 0;
        public CaptureInterface Interface;
        IntPtr faddr;
        LocalHook inputHook = null;
        private IntPtr _windowHandle;
        NativeMethods.Rect windowSize;
        
        public unsafe ImGuiRender(Device dev, NativeMethods.Rect windowRect, CaptureInterface _interface, IntPtr windowHandle)
        {
            Interface = _interface;
            device = dev;
            _windowHandle = windowHandle;
            Debug.Write("Hook created");
            faddr = LocalHook.GetProcAddress("user32.dll", "GetRawInputData");
            inputHook = LocalHook.Create(faddr, new inputHookDelegate(inputHookSub), this);
            inputHook.ThreadACL.SetExclusiveACL(new Int32[0]);
            windowSize = windowRect;
            //foreach (RAWINPUTDEVICELIST d in GetAllRawDevices())
            //{
            //    IPCDebugMessage("found handle: " + d.hDevice + " type:" + d.dwType);
            //    if (d.dwType == RawDeviceType.RIM_TYPEKEYBOARD)
            //    {
            //        IPCDebugMessage("got keyboard: " + d.hDevice);
            //    }
            //    if (d.dwType == RawDeviceType.RIM_TYPEMOUSE)
            //    {
            //        IPCDebugMessage("got mouse: " + d.hDevice);
            //    }
            //}
            
            IO io = ImGui.GetIO();

            UpdateCanvasSize(windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
            PrepareTextureImGui();
            SetupKeyMapping(io);
        }

        uint inputHookSub(IntPtr hRawInput, IntPtr uiCommand, IntPtr pData, IntPtr pcbSize, IntPtr cbSizeHeader)
        {
            uint res = 0;
            inputHookDelegate del = (inputHookDelegate) Marshal.GetDelegateForFunctionPointer(faddr, typeof(inputHookDelegate));

            try
            {
                res = del(hRawInput, uiCommand, pData, pcbSize, cbSizeHeader);
                if (res != 0)
                {
                    RAWINPUT inp = (RAWINPUT) Marshal.PtrToStructure(pData, typeof(RAWINPUT));
                    Marshal.Release(pData);
                    if (inp.header.dwType == RawDeviceType.RIM_TYPEMOUSE)
                    {
                        if (inp.mouse.lLastX != 0 || inp.mouse.lLastY != 0 || inp.mouse.ulButtons != 0)
                        {
                            if (inp.mouse.ulButtons != 0)
                            {
                                switch (inp.mouse.ulButtons)
                                {
                                    case RawMouseButtons.RI_MOUSE_LEFT_BUTTON_DOWN:
                                        mouseState.LMB = true;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_LEFT_BUTTON_UP:
                                        mouseState.LMB = false;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_RIGHT_BUTTON_DOWN:
                                        mouseState.RMB = true;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_RIGHT_BUTTON_UP:
                                        mouseState.RMB = false;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_MIDDLE_BUTTON_DOWN:
                                        mouseState.MMB = true;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_MIDDLE_BUTTON_UP:
                                        mouseState.MMB = false;
                                        break;
                                    case RawMouseButtons.RI_MOUSE_WHEEL:
                                        mouseState.Wheel += inp.mouse.buttonsStr.usButtonData;
                                        break;
                                }
                            }
                            if (inp.mouse.lLastX != 0 || inp.mouse.lLastY != 0)
                            {
                                //mouseState.X += inp.mouse.lLastX;
                                //mouseState.Y += inp.mouse.lLastY;
                                //if (mouseState.X < windowSize.Left) mouseState.X = windowSize.Left;
                                //if (mouseState.X > windowSize.Right) mouseState.X = windowSize.Right;
                                //if (mouseState.Y < windowSize.Top) mouseState.Y = windowSize.Top;
                                //if (mouseState.Y > windowSize.Bottom) mouseState.Y = windowSize.Bottom;
                            }
                        }
                    }
                    if (inp.header.dwType == RawDeviceType.RIM_TYPEKEYBOARD)
                    {
                        IPCDebugMessage($"key: {(Keys) inp.keyboard.VKey}");
                    }
                    if (inp.header.dwType == RawDeviceType.RIM_TYPEHID)
                    {
                        IPCDebugMessage($"uknown device");
                    }
                }
            }
            catch (Exception ex)
            {
                IPCDebugMessage("ERROR input: " + ex.InnerException);
            }
            return res;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetRawInputDeviceList
            ([In, Out] IntPtr RawInputDeviceList, ref uint NumDevices, uint Size);




        protected void IPCDebugMessage(string message)
        {
            try
            {
                Interface.Message(MessageType.Debug, message);
            }
            catch (RemotingException)
            {
            }
            catch (Exception)
            {
            }
        }

        private static unsafe void memcpy(void* dst, void* src, int count)
        {
            const int blockSize = 4096;
            byte[] block = new byte[blockSize];
            byte* d = (byte*) dst, s = (byte*) src;
            for (int i = 0, step; i < count; i += step, d += step, s += step)
            {
                step = count - i;
                if (step > blockSize)
                {
                    step = blockSize;
                }
                Marshal.Copy(new IntPtr(s), block, 0, step);
                Marshal.Copy(block, 0, new IntPtr(d), step);
            }
        }

        private unsafe void PrepareTextureImGui()
        {
            var io = ImGui.GetIO();
            //char[] ranges =
            //{
            //    (char)0x0020, (char)0x00FF, // Basic Latin + Latin Supplement
            //    (char)0x0400, (char)0x052F, // Cyrillic + Cyrillic Supplement
            //    (char)0,
            //};
            char[] ranges =
            {
                (char)0x0020,  (char)0x052F, // Basic Latin + Latin Supplement
            };
            fixed (char* pChars = ranges)
            {
                io.FontAtlas.AddFontFromFileTTF("C:\\Windows\\Fonts\\tahoma.ttf", 12f, pChars);
            }
           var texDataAsRgba32 = io.FontAtlas.GetTexDataAsRGBA32();
            var t = new Texture(device, texDataAsRgba32.Width, texDataAsRgba32.Height, 1, Usage.Dynamic,
                Format.A8R8G8B8, Pool.Default);
            var rect = t.LockRectangle(0, LockFlags.None);
            
            for (int y = 0; y < texDataAsRgba32.Height; y++)
            {
                memcpy((byte*) (rect.DataPointer + rect.Pitch * y), texDataAsRgba32.Pixels + (texDataAsRgba32.Width * texDataAsRgba32.BytesPerPixel) * y, (texDataAsRgba32.Width * texDataAsRgba32.BytesPerPixel));
            }
            t.UnlockRectangle(0);
            
            io.FontAtlas.SetTexID(t.NativePointer);
            texNative = t;
            io.FontAtlas.ClearTexData();
        }

        private void SetupKeyMapping(IO io)
        {
            
            io.KeyMap[GuiKey.Tab] = (int) Keys.Tab;
            io.KeyMap[GuiKey.LeftArrow] = (int) Keys.Left;
            io.KeyMap[GuiKey.RightArrow] = (int) Keys.Right;
            io.KeyMap[GuiKey.UpArrow] = (int) Keys.Up;
            io.KeyMap[GuiKey.DownArrow] = (int) Keys.Down;
            io.KeyMap[GuiKey.PageUp] = (int) Keys.PageUp;
            io.KeyMap[GuiKey.PageDown] = (int) Keys.PageDown;
            io.KeyMap[GuiKey.Home] = (int) Keys.Home;
            io.KeyMap[GuiKey.End] = (int) Keys.End;
            io.KeyMap[GuiKey.Delete] = (int) Keys.Delete;
            io.KeyMap[GuiKey.Backspace] = (int) Keys.Back;
            io.KeyMap[GuiKey.Enter] = (int) Keys.Enter;
            io.KeyMap[GuiKey.Escape] = (int) Keys.Escape;
            io.KeyMap[GuiKey.A] = (int) Keys.A;
            io.KeyMap[GuiKey.C] = (int) Keys.C;
            io.KeyMap[GuiKey.V] = (int) Keys.V;
            io.KeyMap[GuiKey.X] = (int) Keys.X;
            io.KeyMap[GuiKey.Y] = (int) Keys.Y;
            io.KeyMap[GuiKey.Z] = (int) Keys.Z;
        }

        public void UpdateCanvasSize(float width, float height)
        {
            IO io = ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(width, height);
            //io.DisplayFramebufferScale = new System.Numerics.Vector2(width / height);
        }

        public void GetNewFrame()
        {
            ImGui.NewFrame();
        }

        public unsafe void Draw()
        {
            IO io = ImGui.GetIO();
            io.DeltaTime = (1f / 20f);
            UpdateImGuiInput(io);
            
            ImGui.Render();
            DrawData* data = ImGui.GetDrawData();
            ImGuiRenderDraw(data);
        }

        private unsafe void UpdateImGuiInput(IO io)
        {
            //
            if (NativeMethods.IsWindowInForeground(_windowHandle))
            {
                NativeMethods.Point p;
                NativeMethods.GetCursorPos(out p);
                //IPCDebugMessage($"px:{p.X} py:{p.Y}");
                //NativeMethods.ScreenToClient(_windowHandle, ref p);
                mouseState.X = p.X;
                mouseState.Y = p.Y;

                io.MousePosition = new System.Numerics.Vector2(mouseState.X / io.DisplayFramebufferScale.X, mouseState.Y / io.DisplayFramebufferScale.Y);
            }
            else
            {
                //NativeMethods.Point p;
                //NativeMethods.GetCursorPos(out p);
                //mouseState.X = p.X;
                //mouseState.Y = p.Y;
                io.MousePosition = new System.Numerics.Vector2(-1f, -1f);
            }

            io.MouseDown[0] = mouseState.LMB;
            io.MouseDown[1] = mouseState.RMB;
            io.MouseDown[2] = mouseState.MMB;
            //io.WantCaptureKeyboard = true;
            float newWheelPos = mouseState.Wheel;
            float delta = newWheelPos - _wheelPosition;
            _wheelPosition = (int) newWheelPos;
            io.MouseWheel = delta;
            mouseState.Wheel = 0;
        }

        private unsafe void ImGuiRenderDraw(DrawData* drawData)
        {
            
            if (drawData == null)
                return;
            var io = ImGui.GetIO();
            if (io.DisplaySize.X <= 0.0f || io.DisplaySize.Y <= 0.0f)
                return;
            var st = new StateBlock(device, StateBlockType.All);
            var vp = new Viewport();
            vp.X = vp.Y = 0;
            vp.Width = (int) io.DisplaySize.X;
            vp.Height = (int) io.DisplaySize.Y;
            vp.MinDepth = 0.0f;
            vp.MaxDepth = 1.0f;
            device.Viewport = vp;
            device.PixelShader = null;
            device.VertexShader = null;
            device.SetRenderState(RenderState.CullMode, Cull.None);
            device.SetRenderState(RenderState.Lighting, false);
            device.SetRenderState(RenderState.ZEnable, false);
            device.SetRenderState(RenderState.AlphaBlendEnable, true);
            device.SetRenderState(RenderState.AlphaTestEnable, false);
            device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
            device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
            device.SetRenderState(RenderState.DestinationBlend, Blend.BothInverseSourceAlpha);
            device.SetRenderState(RenderState.ScissorTestEnable, true);
            device.SetTextureStageState(0, TextureStage.ColorOperation, TextureOperation.Modulate);
            device.SetTextureStageState(0, TextureStage.ColorArg1, TextureArgument.Texture);
            device.SetTextureStageState(0, TextureStage.ColorArg2, TextureArgument.Diffuse);
            device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.Modulate);
            device.SetTextureStageState(0, TextureStage.AlphaArg1, TextureArgument.Texture);
            device.SetTextureStageState(0, TextureStage.AlphaArg2, TextureArgument.Diffuse);
            device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Linear);
            // Setup orthographic projection matrix
            {
                const float L = 0.5f;
                float R = io.DisplaySize.X + 0.5f;
                const float T = 0.5f;
                float B = io.DisplaySize.Y + 0.5f;
                RawMatrix mat_identity = new Matrix(1.0f, 0.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 0.0f,
                    0.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 0.0f, 1.0f);
                RawMatrix mat_projection = new Matrix(
                    2.0f / (R - L), 0.0f, 0.0f, 0.0f,
                    0.0f, 2.0f / (T - B), 0.0f, 0.0f,
                    0.0f, 0.0f, 0.5f, 0.0f,
                    (L + R) / (L - R), (T + B) / (B - T), 0.5f, 1.0f);
                device.SetTransform(TransformState.World, ref mat_identity);
                device.SetTransform(TransformState.View, ref mat_identity);
                device.SetTransform(TransformState.Projection, ref mat_projection);
            }
            using (device.VertexDeclaration = new VertexDeclaration(device, GuiVertex.VertexElements))
            {
                for (var n = 0; n < drawData->CmdListsCount; n++)
                {
                    NativeDrawList* cmdList = drawData->CmdLists[n];
                    DrawVert* vtx_buffer = (DrawVert*) cmdList->VtxBuffer.Data;
                    ushort* idx_buffer = (ushort*) cmdList->IdxBuffer.Data;

                    var myCustomVertices = new GuiVertex[cmdList->VtxBuffer.Size];
                    
                    for (var i = 0; i < myCustomVertices.Length; i++)
                    {
                        //var cl = (vtx_buffer[i].col & 0xFF00FF00) | ((vtx_buffer[i].col & 0xFF0000) >> 16) | ((vtx_buffer[i].col & 0xFF) << 16);
                        var cl = (vtx_buffer[i].col & 0xFF00FF00) | ((vtx_buffer[i].col & 0xFF0000) >> 16) | ((vtx_buffer[i].col & 0xFF) << 16);
                        myCustomVertices[i] =
                            new GuiVertex(vtx_buffer[i].pos.X, vtx_buffer[i].pos.Y, vtx_buffer[i].uv.X, vtx_buffer[i].uv.Y, cl);
                    }
                    
                    for (var i = 0; i < cmdList->CmdBuffer.Size; i++)
                    {
                        DrawCmd* pcmd = &((DrawCmd*) cmdList->CmdBuffer.Data)[i];
                        
                        if (pcmd->UserCallback != IntPtr.Zero)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            device.SetTexture(0, new Texture(pcmd->TextureId));
                            device.ScissorRect = new RectangleF((int) pcmd->ClipRect.X,
                                (int) pcmd->ClipRect.Y,
                                (int) (pcmd->ClipRect.Z - pcmd->ClipRect.X),
                                (int) (pcmd->ClipRect.W - pcmd->ClipRect.Y));
                            ushort[] indices = new ushort[pcmd->ElemCount];
                            for (int j = 0; j < indices.Length; j++)
                            {
                                indices[j] = idx_buffer[j];
                            }

                            device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, myCustomVertices.Length, (int) (pcmd->ElemCount / 3), indices, Format.Index16, myCustomVertices);
                        }
                        idx_buffer += pcmd->ElemCount;
                    }
                }
            }
            st.Apply();
            st.Dispose();
        }

        public void Dispose()
        {
            try
            {
                Debug.Write("Удаление хука");
                inputHook?.Dispose();
                var io = ImGui.GetIO();
                io.FontAtlas.Clear();
                ImGui.Shutdown();
                //texNative.UnlockRectangle(0);
                if (texNative.NativePointer != IntPtr.Zero)
                    Marshal.Release(texNative.NativePointer);
                texNative.Dispose();
                GC.SuppressFinalize(this);
            }
            catch (Exception e)

            {
                Debug.Write(e.Message);
            }

        // inputHook?.ThreadACL.SetExclusiveACL(new Int32[0]);

        }
        #region api 

        public static RAWINPUTDEVICELIST[] GetAllRawDevices()
        {
            uint deviceCount = 0;
            uint dwSize = (uint) Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

            uint retValue = GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, dwSize);
            if (0 != retValue)
                return null;

            RAWINPUTDEVICELIST[] deviceList = new RAWINPUTDEVICELIST[deviceCount];
            IntPtr pRawInputDeviceList = Marshal.AllocHGlobal((int) (dwSize * deviceCount));

            GetRawInputDeviceList(pRawInputDeviceList, ref deviceCount, dwSize);

            for (int i = 0; i < deviceCount; i++)
            {
                deviceList[i] = (RAWINPUTDEVICELIST) Marshal.PtrToStructure(
                    new IntPtr((pRawInputDeviceList.ToInt32() + (dwSize * i))),
                    typeof(RAWINPUTDEVICELIST));
            }

            Marshal.FreeHGlobal(pRawInputDeviceList);
            
            return deviceList;
        }

        #endregion
    }
}
