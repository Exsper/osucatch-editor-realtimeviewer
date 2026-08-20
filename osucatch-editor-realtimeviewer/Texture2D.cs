using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing.Imaging;

namespace osucatch_editor_realtimeviewer
{
    public class Texture2D : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TextureId => textureId;
        private int textureId = 0;
        public Texture2D(Stream stream) : this(stream, true)
        {
        }

        /// <summary>
        /// 加载图像贴图。
        /// </summary>
        /// <param name="stream">图像流。</param>
        /// <param name="generateMipmaps">
        /// 是否生成 mipmap。果子/水滴/香蕉贴图会被缩小绘制（多屏模式下尤其明显），
        /// 没有 mipmap 时 GL_LINEAR 缩小采样会产生边缘锯齿/闪烁；
        /// 文字贴图始终 1:1 绘制，不需要 mipmap（反而会变糊），应传 false。
        /// </param>
        public Texture2D(Stream stream, bool generateMipmaps)
        {
            using (var bitmap = new Bitmap(stream))
            {
                this.Width = bitmap.Width;
                this.Height = bitmap.Height;
                this.textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, this.textureId);
                // 缩小过滤用 mipmap（最近层级 + 双线性，避免三线性带来的过度模糊），放大保持双线性
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    generateMipmaps ? (int)TextureMinFilter.LinearMipmapNearest : (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                // 贴图是满幅圆环，贴到纹理边缘；必须钳制而不是默认的 REPEAT，
                // 否则缩小采样到边缘时会把最右/最下一列混成对侧透明像素，导致边缘缺失
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                UploadLevel(bitmap, 0);
                if (generateMipmaps) GenerateMipmaps(bitmap);
            }
            stream.Dispose();
        }

        public Texture2D(string text, float fontScale)
        {
            Font font = new Font("Arial", (float)(32.0 * fontScale));
            // 创建一个空的Bitmap，大小根据实际需要决定
            Bitmap tempBitmap = new Bitmap(1, 1);
            SizeF textSize = Graphics.FromImage(tempBitmap).MeasureString(text, font);
            int width = (int)textSize.Width;
            int height = (int)textSize.Height;
            tempBitmap.Dispose();

            // 创建实际大小的Bitmap
            Bitmap bitmap = new Bitmap(width, height);

            // 使用指定的背景颜色填充Bitmap
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 设置文字的渲染质量
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                SolidBrush solidBrush = new SolidBrush(Color.White);
                // 使用指定的Font和颜色绘制文本
                g.DrawString(text, font, solidBrush, new PointF(0, 0));
                font.Dispose();
                solidBrush.Dispose();
            }
            this.Width = bitmap.Width;
            this.Height = bitmap.Height;
            this.textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, this.textureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            UploadLevel(bitmap, 0);
        }

        /// <summary>
        /// 将位图的像素上传为指定 mip 级别。
        /// </summary>
        private static void UploadLevel(Bitmap bitmap, int level)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                GL.TexImage2D(TextureTarget.Texture2D, level, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>
        /// 用精确的 2×2 盒式平均逐级生成 mipmap。
        /// 双三次缩放在小层级会把圆环边缘抹软并产生不对称量化（右/下边缘缺失），
        /// 盒式平均是缩小一半时的标准预滤波，圆环在每个层级都保持对称清晰。
        /// 多级纹理（glTexImage2D level）从 GL 1.0 起就是核心功能，
        /// 兼容旧显卡/软件 GL，不依赖 GL 3.0 的 glGenerateMipmap。
        /// </summary>
        private static void GenerateMipmaps(Bitmap baseBitmap)
        {
            Bitmap src = new Bitmap(baseBitmap);
            try
            {
                int level = 1;
                int w = Math.Max(1, baseBitmap.Width / 2);
                int h = Math.Max(1, baseBitmap.Height / 2);
                while (true)
                {
                    Bitmap dst = BoxDownscale(src);
                    UploadLevel(dst, level);
                    src.Dispose();
                    src = dst;

                    if (w == 1 && h == 1) break;
                    level++;
                    w = Math.Max(1, w / 2);
                    h = Math.Max(1, h / 2);
                }
            }
            finally
            {
                src.Dispose();
            }
        }

        /// <summary>
        /// 将 32bpp 位图按 2×2 块取平均缩小一半。
        /// </summary>
        private static Bitmap BoxDownscale(Bitmap src)
        {
            int sw = src.Width, sh = src.Height;
            int dw = sw / 2, dh = sh / 2;
            var bmp = new Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var sd = src.LockBits(new Rectangle(0, 0, sw, sh), ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var dd = bmp.LockBits(new Rectangle(0, 0, dw, dh), ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] s = new byte[sw * sh * 4];
                byte[] d = new byte[dw * dh * 4];
                System.Runtime.InteropServices.Marshal.Copy(sd.Scan0, s, 0, s.Length);
                for (int y = 0; y < dh; y++)
                {
                    for (int x = 0; x < dw; x++)
                    {
                        int b = 0, g = 0, r = 0, a = 0;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            for (int dx = 0; dx < 2; dx++)
                            {
                                int idx = ((y * 2 + dy) * sw + (x * 2 + dx)) * 4;
                                b += s[idx]; g += s[idx + 1]; r += s[idx + 2]; a += s[idx + 3];
                            }
                        }
                        int o = (y * dw + x) * 4;
                        d[o] = (byte)(b / 4); d[o + 1] = (byte)(g / 4); d[o + 2] = (byte)(r / 4); d[o + 3] = (byte)(a / 4);
                    }
                }
                System.Runtime.InteropServices.Marshal.Copy(d, 0, dd.Scan0, d.Length);
            }
            finally
            {
                src.UnlockBits(sd);
                bmp.UnlockBits(dd);
            }
            return bmp;
        }

        public void Draw(Vector2 pos, Vector2 origin, Color4 color)
        {
            pos -= origin;
            Canvas.AddQuad(this, pos.X, pos.Y, Width, Height, color);
        }
        public void Draw(Vector2 pos, float w, float h, Vector2 origin, Color4 color)
        {
            pos -= origin;
            Canvas.AddQuad(this, pos.X, pos.Y, w, h, color);
        }
        public void Draw(Vector2 pos, Vector2 origin, Color4 color, float rotation, float scale)
        {
            pos -= origin;
            GL.Color4(color);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            if (rotation != 0 || scale != 0)
            {
                Vector3 diff = new Vector3(-pos.X - origin.X, -pos.Y - origin.Y, 0.0f);
                GL.Translate(-diff);
                GL.Rotate(MathHelper.RadiansToDegrees(rotation), 0.0f, 0.0f, 1.0f);
                GL.Scale(scale, scale, 1.0f);
                GL.Translate(diff);
            }
            GL.BindTexture(TextureTarget.Texture2D, this.textureId);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0.0f, 0.0f);
            GL.Vertex2(pos.X, pos.Y);
            GL.TexCoord2(1.0f, 0.0f);
            GL.Vertex2(pos.X + this.Width, pos.Y);
            GL.TexCoord2(1.0f, 1.0f);
            GL.Vertex2(pos.X + this.Width, pos.Y + this.Height);
            GL.TexCoord2(0.0f, 1.0f);
            GL.Vertex2(pos.X, pos.Y + this.Height);
            GL.End();
        }
        public void Draw(Vector2 pos, Vector2 origin, Color4 color, Rectangle source, float rotation, float scale)
        {
            pos -= origin;
            Vector2 texCoordMin = new Vector2(source.X / (float)this.Width, source.Y / (float)this.Height);
            Vector2 texCoordMax = new Vector2((source.X + source.Width) / (float)this.Width, (source.Y + source.Height) / (float)this.Height);
            GL.Color4(color);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            if (rotation != 0 || scale != 0)
            {
                Vector3 diff = new Vector3(-pos.X - origin.X, -pos.Y - origin.Y, 0.0f);
                GL.Translate(-diff);
                GL.Rotate(MathHelper.RadiansToDegrees(rotation), 0.0f, 0.0f, 1.0f);
                GL.Scale(scale, scale, 1.0f);
                GL.Translate(diff);
            }
            GL.BindTexture(TextureTarget.Texture2D, this.textureId);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(texCoordMin.X, texCoordMin.Y);
            GL.Vertex2(pos.X, pos.Y);
            GL.TexCoord2(texCoordMax.X, texCoordMin.Y);
            GL.Vertex2(pos.X + source.Width, pos.Y);
            GL.TexCoord2(texCoordMax.X, texCoordMax.Y);
            GL.Vertex2(pos.X + source.Width, pos.Y + source.Height);
            GL.TexCoord2(texCoordMin.X, texCoordMax.Y);
            GL.Vertex2(pos.X, pos.Y + source.Height);
            GL.End();
        }
        public void Dispose()
        {
            GL.DeleteTexture(this.textureId);
        }
    }
}
