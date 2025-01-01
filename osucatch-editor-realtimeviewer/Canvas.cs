﻿using OpenTK;
using OpenTK.Graphics.OpenGL;
using osu.Game.Rulesets.Catch.Objects;
using Color = OpenTK.Graphics.Color4;

namespace osucatch_editor_realtimeviewer
{

    public class Canvas : OpenTK.GLControl
    {
        public ViewerManager viewerManager;

        public float CatcherAreaHeight { get; set; }

        private Texture2D? hitCircleTexture;
        private Texture2D? DropTexture;
        private Texture2D? BananaTexture;

        public static Color[] Default_Colors = new Color[8] {
            // Rainbow! >_<
            new Color(255, 191, 191, 255),
            new Color(255, 210, 128, 255),
            new Color(255, 255, 128, 255),
            new Color(128, 255, 128, 255),
            new Color(128, 255, 255, 255),
            new Color(128, 191, 255, 255),
            new Color(191, 128, 255, 255),
            new Color(255, 128, 255, 255),
        };

        public static Color[] Combo_Colors = new Color[8] { Default_Colors[1], Default_Colors[3], Default_Colors[5], Default_Colors[7], Default_Colors[0], Default_Colors[2], Default_Colors[4], Default_Colors[6] };

        public Canvas()
            : base()
        {
            this.MakeCurrent();
            this.Paint += Canvas_Paint;
            this.Resize += Canvas_Resize;
        }
        public void Canvas_Paint(object sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            if (viewerManager != null)
            {
                viewerManager.BuildNearby();

                this.DrawJudgementLine();
                this.Draw();

            }
            this.SwapBuffers();
        }

        private void Canvas_Resize(object? sender, EventArgs? e)
        {
            int w = this.Size.Width;
            int h = this.Size.Height;
            int x = 0;
            int y = 0;
            if (w * 4 > h)
            {
                w = h / 4;
                x = (this.Size.Width - w) / 2;
            }
            if (h / 4 > w)
            {
                h = w * 4;
                y = (this.Size.Height - h) / 2;
            }
            GL.Viewport(x, y, w, h);
        }

        public void Init()
        {
            // 盘子区间
            this.CatcherAreaHeight = 384f - 350f;

            this.hitCircleTexture = this.TextureFromFile(Form1.Path_Img_Hitcircle);
            this.DropTexture = this.TextureFromFile(Form1.Path_Img_Drop);
            this.BananaTexture = this.TextureFromFile(Form1.Path_Img_Banana);

            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.AlphaTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
            this.Canvas_Resize(this, null);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Vector2 border = new Vector2(1.0f, 4.0f) * 32.0f;
            GL.Ortho(-border.X, 512.0 + border.X, 2048 + border.Y, -border.Y, 0.0, 1.0);
            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }

        private Texture2D? TextureFromFile(string path)
        {
            try
            {
                return new Texture2D(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch
            {
                return null;
            }
        }

        private Texture2D? TextureFromString(string s)
        {
            try
            {
                return new Texture2D(s);
            }
            catch
            {
                return null;
            }
        }


        public void Draw()
        {
            int circleDiameter = (int)(108.848 - viewerManager.Beatmap.Difficulty.CircleSize * 8.9646);
            float fruitSpeed = 384 / viewerManager.ApproachTime;
            for (int b = viewerManager.NearbyHitObjects.Count - 1; b >= 0; b--)
            {
                WithDistancePalpableCatchHitObject hitObject = viewerManager.NearbyHitObjects[b];
                // the song time relative to the hitobject start time
                float diff = (float)(hitObject.currentObject.StartTime - viewerManager.currentTime);
                // 0=在顶端 1=在判定线上 >1=超过判定线
                float alpha = 1.0f;
                if (diff < viewerManager.ApproachTime * viewerManager.State_ARMul && diff > -(viewerManager.ApproachTime * (viewerManager.State_ARMul + 1)))
                {
                    alpha = 1 - (diff / (float)viewerManager.ApproachTime);
                    this.DrawHitcircle(hitObject, alpha, circleDiameter, viewerManager.DistanceType);
                }
            }
        }



        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            GL.Disable(EnableCap.Texture2D);
            GL.Color4(color);
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(start.X, start.Y);
            GL.Vertex2(end.X, end.Y);
            GL.End();
            GL.Enable(EnableCap.Texture2D);
        }

        private void DrawHitcircle(WithDistancePalpableCatchHitObject wdpch, float alpha, int circleDiameter, DistanceType distanceType)
        {
            PalpableCatchHitObject hitObject = wdpch.currentObject;
            Vector2 pos = new Vector2(hitObject.EffectiveX, 384 * alpha - this.CatcherAreaHeight + 640);
            if (Form1.Combo_Colour)
            {
                int comboColorIndex = (hitObject.ComboIndex - 1) % 8;
                Color color = Combo_Colors[comboColorIndex];
                if (hitObject is TinyDroplet)
                {
                    if (hitObject.HyperDash) this.DrawHyperDashCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale / 2));
                    this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale / 2), color);
                }
                else if (hitObject is Droplet)
                {
                    if (hitObject.HyperDash) this.DrawHyperDashCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale));
                    this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale), color);
                }
                else if (hitObject is Fruit)
                {
                    if (hitObject.HyperDash) this.DrawHyperDashCircle(hitCircleTexture, pos, circleDiameter);
                    this.DrawCircle(hitCircleTexture, pos, circleDiameter, color);
                }
                else if (hitObject is Banana)
                {
                    this.DrawCircle(BananaTexture, pos, circleDiameter, Color.Yellow);
                }
            }
            else
            {
                if (hitObject is TinyDroplet)
                {
                    if (hitObject.HyperDash)
                        this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale / 2), new Color(1.0f, 0f, 0f, 1.0f));
                    else
                        this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale / 2), new Color(1.0f, 1.0f, 1.0f, 1.0f));
                }
                else if (hitObject is Droplet)
                {
                    if (hitObject.HyperDash)
                        this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale), new Color(1.0f, 0f, 0f, 1.0f));
                    else
                        this.DrawCircle(DropTexture, pos, (int)(circleDiameter * hitObject.Scale), new Color(1.0f, 1.0f, 1.0f, 1.0f));
                }
                else if (hitObject is Fruit)
                {
                    if (hitObject.HyperDash)
                        this.DrawCircle(hitCircleTexture, pos, circleDiameter, new Color(1.0f, 0f, 0f, 1.0f));
                    else
                        this.DrawCircle(hitCircleTexture, pos, circleDiameter, new Color(1.0f, 1.0f, 1.0f, 1.0f));
                }
                else if (hitObject is Banana)
                {
                    this.DrawCircle(BananaTexture, pos, circleDiameter, Color.Yellow);
                }
            }
            if (distanceType != DistanceType.None && (hitObject is Fruit || (hitObject is Droplet && hitObject is not TinyDroplet)))
            {
                string distanceString = wdpch.GetDistanceString(distanceType);
                Texture2D distanceTexture = new Texture2D(distanceString);
                if (distanceString.Length > 0) this.DrawDistance(distanceTexture, pos, circleDiameter, Color.LightBlue);
                distanceTexture.Dispose();
            }
        }

        private void DrawDistance(Texture2D texture, Vector2 pos, int diameter, Color color)
        {
            // 没有经过计算，纯测出来的
            if (pos.X > 340) pos.X -= (float)(diameter * 1.5);
            else pos.X += diameter;
            pos.Y += (float)(diameter / 6.0);
            texture.Draw(pos, new Vector2(diameter * 0.5f), color);
        }

        private void DrawCircle(Texture2D texture, Vector2 pos, int diameter, Color color)
        {
            texture.Draw(pos, diameter, diameter, new Vector2(diameter * 0.5f), color);
        }

        private void DrawHyperDashCircle(Texture2D texture, Vector2 pos, int diameter)
        {
            texture.Draw(pos, diameter * 1.4f, diameter * 1.4f, new Vector2(diameter * 1.4f * 0.5f), Color.Red);
        }

        private void DrawJudgementLine()
        {
            Vector2 rp0 = new Vector2(0, 384 - CatcherAreaHeight + 640);
            Vector2 rp1 = new Vector2(512, 384 - CatcherAreaHeight + 640);
            DrawLine(rp0, rp1, Color.White);
        }



    }
}
