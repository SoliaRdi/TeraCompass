using Capture.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

using System.Runtime.InteropServices;

namespace Capture.Hook
{
    [Serializable]
    public sealed class ImageElement : Element
    {
        /// <summary>
        /// The image file bytes
        /// </summary>
        public byte[] Image { get; set; }

        System.Drawing.Bitmap _bitmap = null;
        public Bitmap ToBitmap(byte[] imageBytes)
        {
            // Note: deliberately not disposing of MemoryStream, it doesn't have any unmanaged resources anyway and the GC 
            //       will deal with it. This fixes GitHub issue #19 (https://github.com/spazzarama/Direct3DHook/issues/19).
            MemoryStream ms = new MemoryStream(imageBytes);
            try
            {
                Bitmap image = (Bitmap)System.Drawing.Image.FromStream(ms);
                return image;
            }
            catch
            {
                return null;
            }
        }
        internal System.Drawing.Bitmap Bitmap
        {
            get
            {
                if (_bitmap == null && Image != null)
                {
                    _bitmap = ToBitmap(Image);
                    _ownsBitmap = true;
                }

                return _bitmap;
            }
            set { _bitmap = value; }
        }

        /// <summary>
        /// This value is multiplied with the source color (e.g. White will result in same color as source image)
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="System.Drawing.Color.White"/>.
        /// </remarks>
        public System.Drawing.Color Tint { get; set; } = System.Drawing.Color.White;

        /// <summary>
        /// The location of where to render this image element
        /// </summary>
        public System.Drawing.Point Location { get; set; }

        public float Angle { get; set; }

        public float Scale { get; set; } = 1.0f;

        public string Filename { get; set; }

        bool _ownsBitmap = false;

        public ImageElement()
        {
        }

        public ImageElement(string filename)
        {
            Filename = filename;
            Tint = System.Drawing.Color.White;
            Bitmap = new System.Drawing.Bitmap(filename);
            _ownsBitmap = true;
            Scale = 1.0f;
        }

        private new void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (_ownsBitmap)
                {
                    SafeDispose(this.Bitmap);
                    this.Bitmap = null;
                }
            }
        }
    }

    [Serializable]
    public abstract class Element
    {
        public virtual bool Hidden { get; set; }

        ~Element()
        {
            Dispose(false);
        }

        public virtual void Frame()
        {
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">true if disposing both unmanaged and managed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        protected void SafeDispose(IDisposable disposableObj)
        {
            if (disposableObj != null)
                disposableObj.Dispose();
        }
    }
}