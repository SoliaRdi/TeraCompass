using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Capture.GUI.Vertex;
using Capture.Hook.Input;
using Capture.Interface;
using EasyHook;
using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics.Interop;
using TeraCompass.Processing;
using Color = System.Drawing.Color;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;

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


    internal class ImGuiRender : IDisposable
    {
        private readonly IntPtr _windowHandle;
        private readonly Device device;
        private readonly LocalHook DefWindowProcWHook, inputHook;
        private readonly MouseState mouseState = new MouseState();
        private int _wheelPosition;
        private Texture texNative;
        private readonly inputHookDelegate OriginalInputHook;
        private readonly TWindowProcW OriginalWndProcHook;

        public ImGuiRender(Device dev, NativeMethods.Rect windowRect, CaptureInterface _interface, IntPtr windowHandle)
        {

            device = dev;
            _windowHandle = windowHandle;
            IntPtr InputHookAddr = LocalHook.GetProcAddress("user32.dll", "GetRawInputData");
            inputHook = LocalHook.Create(InputHookAddr, new inputHookDelegate(inputHookSub), this);
            inputHook.ThreadACL.SetExclusiveACL(new Int32[0]);
            OriginalInputHook =
                (inputHookDelegate) Marshal.GetDelegateForFunctionPointer(InputHookAddr, typeof(inputHookDelegate));
            var defWindowProcWAddr = LocalHook.GetProcAddress("user32.dll", "DefWindowProcW");
            DefWindowProcWHook = LocalHook.Create(defWindowProcWAddr, new TWindowProcW(WindowProcWImplementation), this);
            DefWindowProcWHook.ThreadACL.SetExclusiveACL(new int[0]);
            OriginalWndProcHook = (TWindowProcW)Marshal.GetDelegateForFunctionPointer(defWindowProcWAddr, typeof(TWindowProcW));
            var io = ImGui.GetIO();
            UpdateCanvasSize(windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
            PrepareTextureImGui();
            SetupKeyMapping(io);
        }

        public void Dispose()
        {
            try
            {
                inputHook?.Dispose();
                DefWindowProcWHook?.Dispose();
                var io = ImGui.GetIO();
                io.FontAtlas.Clear();
                ImGui.Shutdown();
                if (texNative.NativePointer != IntPtr.Zero)
                    Marshal.Release(texNative.NativePointer);
                texNative.Dispose();
                GC.SuppressFinalize(this);
            }
            catch (Exception e)

            {
                Trace.Write(e.Message);
            }
        }

        

        private IntPtr WindowProcWImplementation(IntPtr hWnd, WindowsMessages uMsg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (uMsg)
                {
                    case WindowsMessages.MOUSEMOVE:
                    {
                        mouseState.X = (short) lParam.ToInt32();
                        mouseState.Y = lParam.ToInt32() >> 16;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.Write("ERROR wndproc: " + ex.Message);
            }
            

            return OriginalWndProcHook(hWnd, uMsg, wParam, lParam);
        }
        uint inputHookSub(IntPtr hRawInput, IntPtr uiCommand, IntPtr pData, IntPtr pcbSize, IntPtr cbSizeHeader)
        {
            uint res = 0;

            try
            {
                res = OriginalInputHook(hRawInput, uiCommand, pData, pcbSize, cbSizeHeader);
                if (res != 0)
                {
                    RAWINPUT inp = (RAWINPUT) Marshal.PtrToStructure(pData, typeof(RAWINPUT));
                    Marshal.Release(pData);
                    if (inp.header.dwType == RawDeviceType.RIM_TYPEMOUSE&&inp.mouse.ulButtons != 0)
                    {
                        switch (inp.mouse.ulButtons)
                        {
                            case RawMouseButtons.MOUSE_LEFT_BUTTON_DOWN:
                                mouseState.LMB = true;
                                break;
                            case RawMouseButtons.MOUSE_LEFT_BUTTON_UP:
                                mouseState.LMB = false;
                                break;
                            case RawMouseButtons.MOUSE_RIGHT_BUTTON_DOWN:
                                mouseState.RMB = true;
                                break;
                            case RawMouseButtons.MOUSE_RIGHT_BUTTON_UP:
                                mouseState.RMB = false;
                                break;
                            case RawMouseButtons.MOUSE_MIDDLE_BUTTON_DOWN:
                                mouseState.MMB = true;
                                break;
                            case RawMouseButtons.MOUSE_MIDDLE_BUTTON_UP:
                                mouseState.MMB = false;
                                break;
                            case RawMouseButtons.MOUSE_WHEEL:
                                mouseState.Wheel += inp.mouse.buttonsStr.usButtonData;
                                break;
                        }
                    }
                    //uint virtualKey = inp.keyboard.VKey;
                    //uint makeCode = inp.keyboard.MakeCode;
                    //uint flags = inp.keyboard.Flags;
                    //if (virtualKey != 0)
                    //{
                    //    var keyCode = VirtualKeyCorrection(inp, virtualKey, (flags & VIRTUALKEY.RI_KEY_E0) != 0, makeCode);
                    //    Trace.Write(keyCode);
                    //}

                }
            }
            catch (Exception ex)
            {
                Trace.Write("ERROR input: " + ex.InnerException);
            }

            return res;
        }
        private static uint VirtualKeyCorrection(RAWINPUT input, uint virtualKey, bool isE0BitSet, uint makeCode)
        {
            var correctedVKey = virtualKey;

            if (input.header.hDevice == IntPtr.Zero)
            {
                // When hDevice is 0 and the vkey is VK_CONTROL indicates the ZOOM key
                if (input.keyboard.VKey == VIRTUALKEY.VK_CONTROL)
                {
                    correctedVKey = VIRTUALKEY.VK_ZOOM;
                }
            }
            else
            {
                switch (virtualKey)
                {
                    // Right-hand CTRL and ALT have their e0 bit set 
                    case VIRTUALKEY.VK_CONTROL:
                        correctedVKey = isE0BitSet ? VIRTUALKEY.VK_RCONTROL : VIRTUALKEY.VK_LCONTROL;
                        break;
                    case VIRTUALKEY.VK_MENU:
                        correctedVKey = isE0BitSet ? VIRTUALKEY.VK_RMENU : VIRTUALKEY.VK_LMENU;
                        break;
                    case VIRTUALKEY.VK_SHIFT:
                        correctedVKey = makeCode == VIRTUALKEY.SC_SHIFT_R ? VIRTUALKEY.VK_RSHIFT : VIRTUALKEY.VK_LSHIFT;
                        break;
                    default:
                        correctedVKey = virtualKey;
                        break;
                }
            }

            return (correctedVKey);
        }

        private static unsafe void memcpy(void* dst, void* src, int count)
        {
            const int blockSize = 4096;
            var block = new byte[blockSize];
            byte* d = (byte*) dst, s = (byte*) src;
            for (int i = 0, step; i < count; i += step, d += step, s += step)
            {
                step = count - i;
                if (step > blockSize) step = blockSize;
                Marshal.Copy(new IntPtr(s), block, 0, step);
                Marshal.Copy(block, 0, new IntPtr(d), step);
            }
        }

        private unsafe void PrepareTextureImGui()
        {
            var io = ImGui.GetIO();
            char[] ranges =
            {
                (char) 0x0020, (char) 0x052F // Basic Latin + Latin Supplement + Cyrillic + Cyrillic Supplement
            };
            fixed (char* pChars = ranges)
            {
                io.FontAtlas.AddFontFromFileTTF("C:\\Windows\\Fonts\\tahoma.ttf", 12f, pChars);
                
            }

            var texDataAsRgba32 = io.FontAtlas.GetTexDataAsRGBA32();
            var t = new Texture(device, texDataAsRgba32.Width, texDataAsRgba32.Height, 1, Usage.Dynamic,
                Format.A8R8G8B8, Pool.Default);
            var rect = t.LockRectangle(0, LockFlags.None);

            for (var y = 0; y < texDataAsRgba32.Height; y++)
                memcpy((byte*) (rect.DataPointer + rect.Pitch * y),
                    texDataAsRgba32.Pixels + texDataAsRgba32.Width * texDataAsRgba32.BytesPerPixel * y,
                    texDataAsRgba32.Width * texDataAsRgba32.BytesPerPixel);
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
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(width, height);
            //io.DisplayFramebufferScale = new System.Numerics.Vector2(width / height);
        }

        public void GetNewFrame()
        {
            ImGui.NewFrame();
        }

        public unsafe void Draw()
        {
            var io = ImGui.GetIO();
            io.DeltaTime = 1f / 20f;
            UpdateImGuiInput(io);
            
            ImGui.Render();
            var data = ImGui.GetDrawData();
            ImGuiRenderDraw(data);
        }

        private void UpdateImGuiInput(IO io)
        {
            if (NativeMethods.IsWindowInForeground(_windowHandle))
            {
                io.MousePosition = new Vector2(mouseState.X / io.DisplayFramebufferScale.X,
                    mouseState.Y / io.DisplayFramebufferScale.Y);
            }
            else
            {
                io.MousePosition = new Vector2(-1f, -1f);
            }

            io.MouseDown[0] = mouseState.LMB;
            io.MouseDown[1] = mouseState.RMB;
            io.MouseDown[2] = mouseState.MMB;
            float newWheelPos = mouseState.Wheel;
            var delta = newWheelPos - _wheelPosition;
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
                var R = io.DisplaySize.X + 0.5f;
                const float T = 0.5f;
                var B = io.DisplaySize.Y + 0.5f;
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
                    var cmdList = drawData->CmdLists[n];
                    var vtx_buffer = (DrawVert*) cmdList->VtxBuffer.Data;
                    var idx_buffer = (ushort*) cmdList->IdxBuffer.Data;

                    var myCustomVertices = new GuiVertex[cmdList->VtxBuffer.Size];

                    for (var i = 0; i < myCustomVertices.Length; i++)
                    {
                        var cl = (vtx_buffer[i].col & 0xFF00FF00) | ((vtx_buffer[i].col & 0xFF0000) >> 16) |
                                 ((vtx_buffer[i].col & 0xFF) << 16);
                        myCustomVertices[i] =
                            new GuiVertex(vtx_buffer[i].pos.X, vtx_buffer[i].pos.Y, vtx_buffer[i].uv.X,
                                vtx_buffer[i].uv.Y, cl);
                    }

                    for (var i = 0; i < cmdList->CmdBuffer.Size; i++)
                    {
                        var pcmd = &((DrawCmd*) cmdList->CmdBuffer.Data)[i];
                        
                        if (pcmd->UserCallback != IntPtr.Zero) throw new NotImplementedException();
                        //Trace.WriteLine(pcmd->TextureId.ToString());
                        device.SetTexture(0, new Texture(pcmd->TextureId));
                        device.ScissorRect = new RectangleF((int) pcmd->ClipRect.X,
                            (int) pcmd->ClipRect.Y,
                            (int) (pcmd->ClipRect.Z - pcmd->ClipRect.X),
                            (int) (pcmd->ClipRect.W - pcmd->ClipRect.Y));
                        var indices = new ushort[pcmd->ElemCount];
                        for (var j = 0; j < indices.Length; j++) indices[j] = idx_buffer[j];

                        device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, myCustomVertices.Length,
                            (int) (pcmd->ElemCount / 3), indices, Format.Index16, myCustomVertices);
                        idx_buffer += pcmd->ElemCount;
                    }
                }
            }

            st.Apply();
            st.Dispose();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate uint inputHookDelegate(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate IntPtr TWindowProcW([In] IntPtr hWnd, WindowsMessages uMsg, IntPtr wParam, IntPtr lParam);
    }
}