using System.Buffers;
using System.Numerics;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImDrawListPtr
    {
        public void AddText(Vector2 pos, string text_begin,uint col)
        {
            int text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
            byte* native_text_begin = stackalloc byte[text_begin_byteCount + 1];
            fixed (char* text_begin_ptr = text_begin)
            {
                int native_text_begin_offset = Encoding.UTF8.GetBytes(text_begin_ptr, text_begin.Length, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            }
            byte* native_text_end = null;
            ImGuiNative.ImDrawList_AddText(NativePtr, pos, col, native_text_begin, native_text_end);
        }
        //public unsafe void AddText(Vector2 position, string text, uint color)
        //{
        //    // Consider using stack allocation if a newer version of Encoding is used (with byte* overloads).
        //    int bytes = Encoding.UTF8.GetByteCount(text);
        //    byte[] tempBytes = ArrayPool<byte>.Shared.Rent(bytes);
        //    Encoding.UTF8.GetBytes(text, 0, text.Length, tempBytes, 0);
        //    fixed (byte* bytePtr = &tempBytes[0])
        //    {
        //        ImGuiNative.ImDrawList_AddText(NativePtr, position, color, bytePtr, bytePtr + bytes);
        //    }
        //    ArrayPool<byte>.Shared.Return(tempBytes);
        //}

        public void AddText(ImFontPtr font, float font_size, Vector2 pos, uint col, string text_begin)
        {
            ImFont* native_font = font.NativePtr;
            int text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
            byte* native_text_begin = stackalloc byte[text_begin_byteCount + 1];
            fixed (char* text_begin_ptr = text_begin)
            {
                int native_text_begin_offset = Encoding.UTF8.GetBytes(text_begin_ptr, text_begin.Length, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            }
            byte* native_text_end = null;
            float wrap_width = 0.0f;
            Vector4* cpu_fine_clip_rect = null;
            ImGuiNative.ImDrawList_AddTextFontPtr(NativePtr, native_font, font_size, pos, col, native_text_begin, native_text_end, wrap_width, cpu_fine_clip_rect);
        }
    }
}
