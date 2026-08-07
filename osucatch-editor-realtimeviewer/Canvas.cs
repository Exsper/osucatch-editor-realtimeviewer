using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.Runtime.InteropServices;
using Color = OpenTK.Graphics.Color4;

namespace osucatch_editor_realtimeviewer
{

    public class Canvas : OpenTK.GLControl
    {
        /// <summary>
        /// How many screens add up to the height of canvas.
        /// </summary>
        public static int screensContain = 4;

        /// <summary>
        /// Scale font size to keep the ratio between the size of hitobject and label.
        /// <para />= 1 when window zoom ratio is 100%.
        /// </summary>
        public static float fontScale = 1;

        private static Texture2D? hitCircleTexture;
        private static Texture2D? DropTexture;
        private static Texture2D? BananaTexture;
        private static readonly Dictionary<string, Texture2D> textTextureCache = new();
        private static float textTextureCacheFontScale = -1;
        /// <summary>
        /// 文本纹理缓存上限：标签文本种类很多时（如每物件距离标签），
        /// 无限缓存会导致 GPU 纹理与渲染批次无限增长，内存/GC/每帧遍历开销暴涨。
        /// </summary>
        private const int MaxTextTextureCache = 1024;

        // ---- 批量渲染缓冲（每帧复用，帧末按纹理/线组一次性 GL.DrawArrays）----
        private sealed class QuadBatch
        {
            // 惰性分配：初始只够 1 个四边形，按需倍增，避免每个纹理一个 32KB 大 batch
            public float[] Positions = new float[8];
            public float[] TexCoords = new float[8];
            public float[] Colors = new float[16];
            public int VertexCount;
        }

        private sealed class LineBatch
        {
            public float[] Positions = new float[8];
            public float[] Colors = new float[16];
            public int VertexCount;
            public float Width;
            public bool StippleEnabled;
            public ushort StipplePattern;
        }

        private static readonly Dictionary<Texture2D, QuadBatch> textureBatches = new();
        private static readonly List<LineBatch> backgroundLineBatches = new();
        private static readonly List<LineBatch> foregroundLineBatches = new();

        private readonly float Border_Height = 32;
        private readonly float Border_Width = 32;

        public Canvas()
            : base()
        {
            this.MakeCurrent();
            this.Paint += Canvas_Paint;
            this.Resize += Canvas_Resize;
        }
        public void Canvas_Paint(object? sender, PaintEventArgs? e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            BeginFrame();
            DrawJudgementLine();
            Form1.drawingHelper.Draw();
            FlushFrame();

            this.SwapBuffers();
        }

        private void Canvas_Resize(object? sender, EventArgs? e)
        {
            int w = this.Size.Width;
            int h = this.Size.Height;
            int x = 0;
            int y = 0;
            double width_height = (640.0 + 2 * Border_Width) / (480.0 * screensContain + 2 * Border_Height);
            if (w / width_height > h)
            {
                w = (int)(h * width_height);
                x = (this.Size.Width - w) / 2;
            }
            else if (h * width_height > w)
            {
                h = (int)(w / width_height);
                y = (this.Size.Height - h) / 2;
            }
            GL.Viewport(x, y, w, h);
        }

        public void Init()
        {
            hitCircleTexture = TextureFromFile(Form1.Path_Img_Hitcircle);
            DropTexture = TextureFromFile(Form1.Path_Img_Drop);
            BananaTexture = TextureFromFile(Form1.Path_Img_Banana);

            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.AlphaTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
            this.Canvas_Resize(this, null);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Vector2 border = new Vector2(Border_Width, Border_Height) * ((screensContain > 1) ? 1 : 0);
            GL.Ortho(-border.X, 640.0 + border.X, 480 * screensContain + border.Y, -border.Y, 0.0, 1.0);
            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }

        public void ScreensContainChanged()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Vector2 border = new Vector2(Border_Width, Border_Height) * ((screensContain > 1) ? 1 : 0);
            GL.Ortho(-border.X, 640.0 + border.X, 480 * screensContain + border.Y, -border.Y, 0.0, 1.0);
            this.Canvas_Resize(this, null);
        }

        private static Texture2D? TextureFromFile(string path)
        {
            try
            {
                return new Texture2D(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Read texture file failed: " + path + "\r\n" + ex, Log.LogType.Drawing, Log.LogLevel.Error);
                return null;
            }
        }

        private static Texture2D? TextureFromString(string s, float fontscale)
        {
            try
            {
                // 文本纹理缓存：标签文本只在重建/设置变化时才变，
                // 避免每帧重新做 GDI+ 字体渲染并上传纹理
                if (textTextureCacheFontScale != fontscale)
                {
                    foreach (Texture2D texture in textTextureCache.Values) texture.Dispose();
                    textTextureCache.Clear();
                    textureBatches.Clear();
                    textTextureCacheFontScale = fontscale;
                }

                if (textTextureCache.TryGetValue(s, out Texture2D? cached)) return cached;

                // 超出上限时整体清空，避免标签种类多时纹理/批次无限增长
                if (textTextureCache.Count >= MaxTextTextureCache)
                {
                    foreach (Texture2D texture in textTextureCache.Values) texture.Dispose();
                    textTextureCache.Clear();
                    textureBatches.Clear();
                }

                Texture2D newTexture = new Texture2D(s, fontscale);
                textTextureCache[s] = newTexture;
                return newTexture;
            }
            catch (Exception ex)
            {
                Log.ConsoleLog("Build text texture failed: " + s + "\r\n" + ex, Log.LogType.Drawing, Log.LogLevel.Error);
                return null;
            }
        }

        public static void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            AddLine(start, end, color, 1f, 0, false, false);
        }

        public static void DrawLine(Vector2 start, Vector2 end, Color color, float width, LineType lineType, bool beforeTextures = false)
        {
            bool stipple = lineType != LineType.Solid;
            ushort pattern = 0;
            if (stipple)
            {
                pattern = lineType switch
                {
                    LineType.Dash => 0x00FF,
                    LineType.Dot => 0xCCCC,
                    LineType.DashDot => 0xFF18,
                    LineType.DashDotDot => 0xFCCC,
                    _ => 0,
                };
            }
            AddLine(start, end, color, width, pattern, stipple, beforeTextures);
        }

        /// <summary>
        /// 绘制虚线圆轮廓（用于模板参考物件，半透明由颜色 alpha 控制）。
        /// </summary>
        public static void DrawDashedCircleOutline(Vector2 center, float radius, Color4 color, bool beforeTextures = true)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                double angle0 = 2.0 * Math.PI * i / segments;
                double angle1 = 2.0 * Math.PI * (i + 1) / segments;
                Vector2 p0 = new Vector2(center.X + (float)Math.Cos(angle0) * radius, center.Y + (float)Math.Sin(angle0) * radius);
                Vector2 p1 = new Vector2(center.X + (float)Math.Cos(angle1) * radius, center.Y + (float)Math.Sin(angle1) * radius);
                AddLine(p0, p1, color, 1f, 0x00FF, true, beforeTextures);
            }
        }

        private static void DrawHitObjectLabel(Texture2D? texture, Vector2 notePos, float diameter, Color color)
        {
            if (texture == null) return;
            Vector2 labelPosStart = notePos;
            labelPosStart.X += diameter / 2;
            labelPosStart.Y -= diameter / 2;

            float textureRightX = labelPosStart.X + texture.Width;
            if (textureRightX > 640) labelPosStart.X -= diameter + texture.Width;
            labelPosStart.Y += (diameter - texture.Height) / 2;
            texture.Draw(labelPosStart, new Vector2(0, 0), color);
        }

        private static void DrawLineLabel(Texture2D? texture, Vector2 pos, bool isLeft, Color color)
        {
            if (texture == null) return;
            if (isLeft) texture.Draw(pos, new Vector2(30, 0), color);
            else texture.Draw(pos, new Vector2(texture.Width - 30, 0), color);
        }

        public static void DrawBPMLabel(double bpm, int posY)
        {
            Vector2 rp0 = new Vector2(64, posY);
            Vector2 rp1 = new Vector2(576, posY);
            Canvas.DrawLine(rp0, rp1, Color.Red);
            Texture2D? BPMTexture = TextureFromString(bpm.ToString("F0"), fontScale);
            if (BPMTexture == null) return;
            DrawLineLabel(BPMTexture, rp0, true, Color.Red);
        }

        public static void DrawSVLabel(double sv, int posY)
        {
            Vector2 rp0 = new Vector2(64, posY);
            Vector2 rp1 = new Vector2(576, posY);
            DrawLine(rp0, rp1, Color.LightGreen);
            Texture2D? BPMTexture = TextureFromString(sv.ToString("F2"), fontScale);
            if (BPMTexture == null) return;
            DrawLineLabel(BPMTexture, rp1, false, Color.LightGreen);
        }

        public static void DrawBookmarkLabel(string comment, Color color, int posY)
        {
            Vector2 rp1 = new Vector2(576, posY);
            Texture2D? commentTexture = TextureFromString(comment, fontScale);
            if (commentTexture == null) return;
            DrawLineLabel(commentTexture, rp1, false, color);
        }

        public static void DrawHitObjectLabel(string hitObjectString, Vector2 pos, float circleDiameter, Color color)
        {
            if (hitObjectString == "") return;
            Texture2D? labelTexture = TextureFromString(hitObjectString, fontScale);
            if (labelTexture == null) return;
            if (hitObjectString.Length > 0) DrawHitObjectLabel(labelTexture, pos, circleDiameter, color);
        }

        private static void DrawHyperDashCircle(Texture2D? texture, Vector2 pos, float diameter)
        {
            if (texture == null) return;
            texture.Draw(pos, diameter * 1.3f, diameter * 1.3f, new Vector2(diameter * 1.3f * 0.5f), Color.Red);
        }

        private static void DrawSelectedCircle(Texture2D? texture, Vector2 pos, float diameter)
        {
            if (texture == null) return;
            texture.Draw(pos, diameter * 0.8f, diameter * 0.8f, new Vector2(diameter * 0.8f * 0.5f), Color.Orange);
        }

        private static void DrawCircleWithCircleColor(Texture2D? texture, Vector2 pos, float diameter, Color color, bool isHyperDash, bool isSelected)
        {
            if (texture == null) return;
            if (isHyperDash) DrawHyperDashCircle(texture, pos, diameter);
            texture.Draw(pos, diameter, diameter, new Vector2(diameter * 0.5f), color);
            if (isSelected) DrawSelectedCircle(texture, pos, diameter);
        }

        private static void DrawCircle(Texture2D? texture, Vector2 pos, float diameter, bool isHyperDash, bool isSelected)
        {
            if (texture == null) return;
            Color circleColor = Color.White;
            DrawCircleWithCircleColor(texture, pos, diameter, circleColor, isHyperDash, isSelected);
        }

        public static void DrawFruit(Vector2 pos, float circleDiameter, Color color, bool withCircleColor, bool isHyperDash, bool isSelected)
        {
            if (withCircleColor) DrawCircleWithCircleColor(hitCircleTexture, pos, circleDiameter, color, isHyperDash, isSelected);
            else DrawCircle(hitCircleTexture, pos, circleDiameter, isHyperDash, isSelected);
        }

        public static void DrawDroplet(Vector2 pos, float circleDiameter, float hitObjectScale, Color color, bool withCircleColor, bool isHyperDash, bool isSelected)
        {
            if (withCircleColor) DrawCircleWithCircleColor(DropTexture, pos, circleDiameter * hitObjectScale, color, isHyperDash, isSelected);
            else DrawCircle(DropTexture, pos, circleDiameter * hitObjectScale, isHyperDash, isSelected);
        }

        public static void DrawTinyDroplet(Vector2 pos, float circleDiameter, float hitObjectScale, Color color, bool withCircleColor, bool isHyperDash, bool isSelected)
        {
            if (withCircleColor) DrawCircleWithCircleColor(DropTexture, pos, circleDiameter * hitObjectScale / 2, color, isHyperDash, isSelected);
            else DrawCircle(DropTexture, pos, circleDiameter * hitObjectScale / 2, isHyperDash, isSelected);
        }

        public static void DrawBanana(Vector2 pos, float circleDiameter, bool isSelected)
        {
            if (BananaTexture == null) return;
            BananaTexture.Draw(pos, circleDiameter, circleDiameter, new Vector2(circleDiameter * 0.5f), Color.Yellow);
            if (isSelected) DrawSelectedCircle(BananaTexture, pos, circleDiameter);
        }

        private static void BeginFrame()
        {
            // 每帧重置批处理缓冲（保留已分配容量）
            foreach (QuadBatch batch in textureBatches.Values) batch.VertexCount = 0;
            foreach (LineBatch batch in backgroundLineBatches) batch.VertexCount = 0;
            foreach (LineBatch batch in foregroundLineBatches) batch.VertexCount = 0;

            // 所有四边形都是轴对齐屏幕坐标，modelview 恒为 identity，帧开始设一次即可
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        private static void FlushFrame()
        {
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            // 保持原有层级：背景线 → 纹理（物件/标签）→ 前景线
            GL.Disable(EnableCap.Texture2D);
            FlushLineBatches(backgroundLineBatches);
            GL.Enable(EnableCap.Texture2D);

            FlushTextureBatches();

            GL.Disable(EnableCap.Texture2D);
            FlushLineBatches(foregroundLineBatches);
            GL.Enable(EnableCap.Texture2D);

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.Disable(EnableCap.LineStipple);
        }

        private static void FlushLineBatches(List<LineBatch> batches)
        {
            foreach (LineBatch batch in batches)
            {
                if (batch.VertexCount == 0) continue;

                GL.LineWidth(batch.Width);
                if (batch.StippleEnabled)
                {
                    GL.Enable(EnableCap.LineStipple);
                    GL.LineStipple(2, batch.StipplePattern);
                }
                else
                {
                    GL.Disable(EnableCap.LineStipple);
                }

                DrawLineBatch(batch);
            }
        }

        /// <summary>
        /// 用 GCHandle 固定顶点/颜色数组后一次性绘制。
        /// OpenTK 的泛型数组重载（VertexPointer&lt;T&gt; 等）内部用 C# fixed，
        /// 只在设置指针的瞬间 pin，方法返回即解除；而 glDrawArrays 在之后才读取数组，
        /// 两次调用之间数组可能被 GC 移动，导致读取已失效地址（c0000005，可能读到 0x0）。
        /// </summary>
        private static void DrawLineBatch(LineBatch batch)
        {
            GCHandle hPos = GCHandle.Alloc(batch.Positions, GCHandleType.Pinned);
            GCHandle hCol = GCHandle.Alloc(batch.Colors, GCHandleType.Pinned);
            try
            {
                GL.VertexPointer(2, VertexPointerType.Float, 0, hPos.AddrOfPinnedObject());
                GL.ColorPointer(4, ColorPointerType.Float, 0, hCol.AddrOfPinnedObject());
                GL.DrawArrays(PrimitiveType.Lines, 0, batch.VertexCount);
            }
            finally
            {
                hCol.Free();
                hPos.Free();
            }
        }

        private static void FlushTextureBatches()
        {
            foreach (KeyValuePair<Texture2D, QuadBatch> pair in textureBatches)
            {
                QuadBatch batch = pair.Value;
                if (batch.VertexCount == 0) continue;

                DrawQuadBatch(pair.Key, batch);
            }
        }

        private static void DrawQuadBatch(Texture2D texture, QuadBatch batch)
        {
            GCHandle hPos = GCHandle.Alloc(batch.Positions, GCHandleType.Pinned);
            GCHandle hCol = GCHandle.Alloc(batch.Colors, GCHandleType.Pinned);
            GCHandle hTex = GCHandle.Alloc(batch.TexCoords, GCHandleType.Pinned);
            try
            {
                GL.BindTexture(TextureTarget.Texture2D, texture.TextureId);
                GL.VertexPointer(2, VertexPointerType.Float, 0, hPos.AddrOfPinnedObject());
                GL.ColorPointer(4, ColorPointerType.Float, 0, hCol.AddrOfPinnedObject());
                GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, hTex.AddrOfPinnedObject());
                GL.DrawArrays(PrimitiveType.Quads, 0, batch.VertexCount);
            }
            finally
            {
                hTex.Free();
                hCol.Free();
                hPos.Free();
            }
        }

        internal static void AddQuad(Texture2D texture, float x, float y, float w, float h, Color4 color)
        {
            if (!textureBatches.TryGetValue(texture, out QuadBatch? batch))
            {
                batch = new QuadBatch();
                textureBatches[texture] = batch;
            }

            int v = batch.VertexCount;
            if (v + 4 > batch.Positions.Length / 2) GrowQuadBatch(batch, v + 4);

            int pos = v * 2;
            int col = v * 4;

            // 左下
            batch.Positions[pos] = x; batch.Positions[pos + 1] = y;
            batch.TexCoords[pos] = 0; batch.TexCoords[pos + 1] = 0;
            // 右下
            batch.Positions[pos + 2] = x + w; batch.Positions[pos + 3] = y;
            batch.TexCoords[pos + 2] = 1; batch.TexCoords[pos + 3] = 0;
            // 右上
            batch.Positions[pos + 4] = x + w; batch.Positions[pos + 5] = y + h;
            batch.TexCoords[pos + 4] = 1; batch.TexCoords[pos + 5] = 1;
            // 左上
            batch.Positions[pos + 6] = x; batch.Positions[pos + 7] = y + h;
            batch.TexCoords[pos + 6] = 0; batch.TexCoords[pos + 7] = 1;

            for (int i = 0; i < 4; i++)
            {
                batch.Colors[col + i * 4] = color.R;
                batch.Colors[col + i * 4 + 1] = color.G;
                batch.Colors[col + i * 4 + 2] = color.B;
                batch.Colors[col + i * 4 + 3] = color.A;
            }

            batch.VertexCount = v + 4;
        }

        private static void AddLine(Vector2 start, Vector2 end, Color4 color, float width, ushort stipplePattern, bool stippleEnabled, bool beforeTextures)
        {
            List<LineBatch> batches = beforeTextures ? backgroundLineBatches : foregroundLineBatches;

            LineBatch? batch = null;
            foreach (LineBatch b in batches)
            {
                if (b.Width == width && b.StippleEnabled == stippleEnabled && (!stippleEnabled || b.StipplePattern == stipplePattern))
                {
                    batch = b;
                    break;
                }
            }
            if (batch == null)
            {
                batch = new LineBatch { Width = width, StippleEnabled = stippleEnabled, StipplePattern = stipplePattern };
                batches.Add(batch);
            }

            int v = batch.VertexCount;
            if (v + 2 > batch.Positions.Length / 2) GrowLineBatch(batch, v + 2);

            int pos = v * 2;
            int col = v * 4;
            batch.Positions[pos] = start.X; batch.Positions[pos + 1] = start.Y;
            batch.Positions[pos + 2] = end.X; batch.Positions[pos + 3] = end.Y;

            for (int i = 0; i < 2; i++)
            {
                batch.Colors[col + i * 4] = color.R;
                batch.Colors[col + i * 4 + 1] = color.G;
                batch.Colors[col + i * 4 + 2] = color.B;
                batch.Colors[col + i * 4 + 3] = color.A;
            }

            batch.VertexCount = v + 2;
        }

        private static void GrowQuadBatch(QuadBatch batch, int minVertices)
        {
            int newCapacity = Math.Max(batch.Positions.Length / 2 * 2, minVertices);
            Array.Resize(ref batch.Positions, newCapacity * 2);
            Array.Resize(ref batch.TexCoords, newCapacity * 2);
            Array.Resize(ref batch.Colors, newCapacity * 4);
        }

        private static void GrowLineBatch(LineBatch batch, int minVertices)
        {
            int newCapacity = Math.Max(batch.Positions.Length / 2 * 2, minVertices);
            Array.Resize(ref batch.Positions, newCapacity * 2);
            Array.Resize(ref batch.Colors, newCapacity * 4);
        }

        private static void DrawJudgementLine()
        {
            if (screensContain > 1)
            {
                Vector2 rp0 = new Vector2(64, (float)(240.0 * screensContain));
                Vector2 rp1 = new Vector2(576, (float)(240.0 * screensContain));
                DrawLine(rp0, rp1, Color.White, 1f, LineType.Solid, true);
            }
            else
            {
                Vector2 rp0 = new Vector2(64, 408);
                Vector2 rp1 = new Vector2(576, 408);
                DrawLine(rp0, rp1, Color.White, 1f, LineType.Solid, true);
            }
        }
    }
}
