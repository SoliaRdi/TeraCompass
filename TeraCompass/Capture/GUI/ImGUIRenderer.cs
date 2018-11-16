using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Capture.GUI.Vertex;
using Capture.Hook;
using Capture.Interface;
using Capture.TeraModule.Hook;
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

        private readonly MouseState mouseState = new MouseState();
        //private static readonly List<ushort> keyInput = new List<ushort>();
        private Texture texNative;
        //private Dictionary<char, bool> KeyDown = new Dictionary<char, bool>();
        //public static bool CaptureKeyboard
        //{
        //    get => _captureKeyboard;
        //    set
        //    {
        //        keyInput.Clear();
        //        _captureKeyboard = value;
        //    }
        //}

        int hHook;
        HookProc hp;
        private static bool _captureKeyboard;

        [DllImport("user32", SetLastError = true)]
        static extern int SetWindowsHookEx(HookType iHook, HookProc proc, IntPtr hMod, int threadId);
        [DllImport("user32")]
        static extern int CallNextHookEx(int hHook, int idHook, IntPtr wParam, IntPtr lParam);
        [DllImport("user32")]
        static extern int UnhookWindowsHookEx(int iHook);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        delegate int HookProc(int idHook, IntPtr wParam, IntPtr lParam);
        public ImGuiRender(Device dev, NativeMethods.Rect windowRect, CaptureInterface _interface, Process currentProcess)
        {

            device = dev;
            _windowHandle = currentProcess.MainWindowHandle;
            hp = hookProc;
            var cp = Process.GetCurrentProcess();
            var mName = Path.GetFileNameWithoutExtension(cp.MainModule.ModuleName);
            hHook = SetWindowsHookEx(HookType.WH_GETMESSAGE, hp, GetModuleHandle(mName), cp.Threads[0].Id);
            IntPtr context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize= new Vector2(windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
            io.WantCaptureKeyboard = true;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.GetStyle().WindowBorderSize=0;
            PrepareTextureImGui();
            SetupKeyMapping(io);
        }
        int hookProc(int hookId, IntPtr wParam, IntPtr lParam)
        {
            if (hookId >= 0)
            {
                
                var inputInfo = Marshal.PtrToStructure<NativeMessage>(lParam);
                switch (inputInfo.msg)
                {
                    case WindowsMessages.LBUTTONDOWN:
                        mouseState.LMB = true;
                        break;
                    case WindowsMessages.LBUTTONUP:
                        mouseState.LMB = false;
                        break;
                    case WindowsMessages.MBUTTONDOWN:
                        mouseState.MMB = true;
                        break;
                    case WindowsMessages.MBUTTONUP:
                        mouseState.MMB = false;
                        break;
                    case WindowsMessages.RBUTTONDOWN:
                        mouseState.RMB = true;
                        break;
                    case WindowsMessages.RBUTTONUP:
                        mouseState.RMB = false;
                        break;
                    case WindowsMessages.MOUSEWHEEL:
                        mouseState.Wheel = 120;
                        break;
                    case WindowsMessages.MOUSEMOVE:
                        mouseState.X = inputInfo.p.X;
                        mouseState.Y = inputInfo.p.Y;
                        break;
                    //case WindowsMessages.KEYDOWN:
                    //case WindowsMessages.SYSKEYDOWN:
                    //    if ((char)inputInfo.wParam < 512)
                    //        KeyDown[(char)wParam] = true;
                    //    break;
                    //case WindowsMessages.KEYUP:
                    //case WindowsMessages.SYSKEYUP:
                    //    if ((char)inputInfo.wParam < 512)
                    //        KeyDown[(char)wParam] = false;
                    //    break;
                    //case WindowsMessages.CHAR:
                    //    if (true)
                    //    {
                    //        keyInput.Add((char)inputInfo.wParam);
                    //        Trace.Write((char)inputInfo.wParam);
                    //    }
                            
                    //    //Trace.Write($"hHook {hHook} {hookId} wParam {wParam} lParam {lParam} {inputInfo}");
                    //    break;
                }
                //Trace.Write($"hHook {hHook} {hookId} wParam {wParam} lParam {lParam} {inputInfo}");
            }
            return CallNextHookEx(hHook, hookId, wParam, lParam);
        }

        public static unsafe void memcpy(void* dst, void* src, int count)
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
                io.Fonts.AddFontFromFileTTF("C:\\Windows\\Fonts\\tahoma.ttf", 12f, null,(IntPtr)pChars);
            }
            
            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height,out int bytes_per_pixel);
            var t = new Texture(device, width, height, 1, Usage.Dynamic,
                Format.A8R8G8B8, Pool.Default);
            var rect = t.LockRectangle(0, LockFlags.None);
            
            for (var y = 0; y < height; y++)
                memcpy((byte*) (rect.DataPointer + rect.Pitch * y),
                    pixels + width * bytes_per_pixel * y,
                    width * bytes_per_pixel);
            t.UnlockRectangle(0);
            io.Fonts.SetTexID(t.NativePointer);
            texNative = t;
            io.Fonts.ClearTexData();
        }

        private void SetupKeyMapping(ImGuiIOPtr io)
        {
            #region No Resharper
            io.KeyMap[(int)ImGuiKey.Tab] = (int) Keys.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int) Keys.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int) Keys.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int) Keys.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int) Keys.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int) Keys.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int) Keys.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int) Keys.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int) Keys.End;
            io.KeyMap[(int)ImGuiKey.Delete] = (int) Keys.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int) Keys.Back;
            io.KeyMap[(int)ImGuiKey.Enter] = (int) Keys.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int) Keys.Escape;
            io.KeyMap[(int)ImGuiKey.A] = (int) Keys.A;
            io.KeyMap[(int)ImGuiKey.C] = (int) Keys.C;
            io.KeyMap[(int)ImGuiKey.V] = (int) Keys.V;
            io.KeyMap[(int)ImGuiKey.X] = (int) Keys.X;
            io.KeyMap[(int)ImGuiKey.Y] = (int) Keys.Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int) Keys.Z;
            #endregion
        }

        public void GetNewFrame()
        {
            ImGui.NewFrame();
        }

        public unsafe void Draw()
        {
            var io = ImGui.GetIO();
            UpdateImGuiInput(io);
            ImGui.Render();
            var data = ImGui.GetDrawData();
            ImGuiRenderDraw(data.NativePtr);
        }

        private void UpdateImGuiInput(ImGuiIOPtr iso)
        {
            var io = ImGui.GetIO();
            if (NativeMethods.IsWindowInForeground(_windowHandle))
            {
                io.MousePos = new Vector2(mouseState.X / io.DisplayFramebufferScale.X,
                    mouseState.Y / io.DisplayFramebufferScale.Y);
            }
            else
            {
                io.MousePos = new Vector2(-1f, -1f);
            }
            io.MouseDown[0] = mouseState.LMB;
            io.MouseDown[1] = mouseState.RMB;
            io.MouseDown[2] = mouseState.MMB;
            io.MouseWheel = mouseState.Wheel;
            mouseState.Wheel = 0;
            
            
            //if (true)
            //{
            //    foreach (var i in KeyDown)
            //    {
            //        io.KeysDown[i.Key] = i.Value;
            //    }
            //    foreach (var i in keyInput)
            //        io.AddInputCharacter(i);
            //    keyInput.Clear();
            //    KeyDown.Clear();
            //}
        }
        private unsafe void ImGuiRenderDraw(ImDrawData* drawData)
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
                    var vtx_buffer = (ImDrawVert*) cmdList->VtxBuffer.Data;
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
                        var pcmd = &((ImDrawCmd*) cmdList->CmdBuffer.Data)[i];
                        
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
        public void Dispose()
        {
            try
            {
                var io = ImGui.GetIO();
                io.Fonts.Clear();
                UnhookWindowsHookEx(hHook);
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
    }
}