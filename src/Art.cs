using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cachu
{
    /// <summary>한 포즈의 그림. Right = 오른쪽을 보는 모습, Left = 좌우 반전. Closed* = 눈을 감은 같은 포즈(깜빡임·졸음).</summary>
    internal sealed class Sprite
    {
        public Bitmap Right, Left, ClosedRight, ClosedLeft;
        public PointF EyeL, EyeR;          // 오른쪽 보기 기준 눈 위치(스프라이트 픽셀, 왼쪽 위 원점)
        public PointF MouthP;              // 입 위치(오른쪽 보기 기준)
        public PointF GripP; public bool HasGrip;   // 프로펠러 자루를 쥐는 손 위치(오른쪽 보기 기준)
        public float EyeRad;               // 눈동자 반지름(스프라이트 픽셀) — 장식 크기 기준
        public bool HasEyes, HasMouth, Spiral;
        public int W { get { return Right.Width; } }
        public int H { get { return Right.Height; } }
        internal Rectangle Ink;

        public Bitmap Face(bool left) { return left ? Left : Right; }
        public Bitmap FaceClosed(bool left) { Bitmap c = left ? ClosedLeft : ClosedRight; return c ?? Face(left); }
        public PointF[] Eyes(bool left)
        {
            if (!left) return new[] { EyeL, EyeR };
            return new[] { new PointF(W - EyeR.X, EyeR.Y), new PointF(W - EyeL.X, EyeL.Y) };
        }
        public PointF Mouth(bool left) { return left ? new PointF(W - MouthP.X, MouthP.Y) : MouthP; }
    }

    internal sealed class CharInfo
    {
        public string Key, Name;
        public Color Accent;                  // 말풍선·꽃 장식 등에 어울리는 색
    }

    /// <summary>포즈 한 장의 정보: 눈·입 위치는 ref\poseN.png 원본 픽셀 좌표.</summary>
    internal sealed class PoseDef
    {
        public string Res;
        public bool Flip;                      // 그림이 왼쪽을 보고 있어 오른쪽 보기로 뒤집어야 하면 true
        public float[] Eyes;                   // {왼눈x, y, 반지름, 오른눈x, y, 반지름} 없으면 null
        public float[] Mouth;                  // {x, y} 없으면 null
        public float[] Grip;                   // 손으로 물건을 쥘 자리 {x, y} 없으면 null
    }

    /// <summary>
    /// ref 폴더의 6포즈 그림(엎드림·잠·앉아 울기·서서 놀람·화남)으로 걷기·낙하·벽·천장·울기·낮잠 등 모든 모습을 만든다.
    /// 장식(프로펠러·눈물·소용돌이 등)은 PetRender 에서 코드로 얹는다.
    /// </summary>
    internal sealed class CharacterArt
    {
        internal static readonly CharInfo[] Infos =
        {
            new CharInfo { Key = "cachu", Name = "카츄", Accent = Color.FromArgb(255, 214, 80) },
        };
        internal static int Count { get { return Infos.Length; } }
        internal static float Scale = 1f;

        // 포즈 그림 (픽셀 좌표는 ref\pose*.png 기준)
        private static readonly PoseDef PoseLie = new PoseDef { Res = "pose1.png", Eyes = new[] { 185f, 132f, 9.5f, 236f, 128f, 7f }, Mouth = new[] { 221f, 157f } };
        private static readonly PoseDef PoseSleep = new PoseDef { Res = "pose2.png", Flip = true };
        private static readonly PoseDef PoseCry = new PoseDef { Res = "pose3.png", Eyes = new[] { 66f, 53f, 8f, 113f, 56f, 8.5f }, Mouth = new[] { 88f, 86f } };
        private static readonly PoseDef PoseStand = new PoseDef { Res = "pose4.png", Eyes = new[] { 120f, 109f, 9.5f, 168f, 100f, 9.5f }, Mouth = new[] { 148f, 130f }, Grip = new[] { 203f, 163f } };
        private static readonly PoseDef PoseAngry = new PoseDef { Res = "pose6.png", Eyes = new[] { 79f, 82f, 11f, 130f, 83f, 11f }, Mouth = new[] { 104f, 100f } };

        private static readonly Color Fur = Color.FromArgb(242, 214, 100);
        private static readonly Color Ink2 = Color.FromArgb(70, 38, 20);

        private static readonly Dictionary<int, CharacterArt> cache = new Dictionary<int, CharacterArt>();
        internal static CharacterArt Get(int id)
        {
            id = ((id % Count) + Count) % Count;
            CharacterArt a;
            if (!cache.TryGetValue(id, out a)) cache[id] = a = new CharacterArt(id);
            return a;
        }
        internal static void Reset(float scale) { Scale = scale; cache.Clear(); }

        public readonly int Id;
        public readonly CharInfo Info;
        /// <summary>Stand 차분한 서기(입 다문 얼굴) · StandOh 놀란 얼굴(낙하·점프·기지개) · Sit 엎드려 쉬기 · Sleep 잠 · TouchCry 앉아 울기 · Angry 화남(눌림·잡힘)</summary>
        public Sprite Stand, StandOh, Sit, Sleep, TouchCry, IdleCry, Slide, DizzyPose, Ceiling, Angry;
        public int W, H;                 // 기본 그림 크기(걷기 기준) — 이동·충돌 기준
        public int RotorH;               // 프로펠러가 머리 위로 올라오는 높이
        public int CeilingTopInset;
        /// <summary>프로펠러 자루를 쥔 손의 가로 위치(스프라이트 가운데 기준)와 프로펠러 중심까지의 높이 — 낙하할 때 이 점을 축으로 흔들린다.</summary>
        public float PoleX { get { return StandOh.HasGrip ? StandOh.GripP.X - StandOh.W / 2f : 0; } }
        public float HubHeight { get { return H + RotorH * .72f; } }
        public int CeilingContactHeight { get { return Ceiling.H - CeilingTopInset; } }

        /// <summary>서 있는 그림의 높이(픽셀, 배율 1). 다른 포즈도 같은 비율로 줄여 몸 크기가 비슷해 보이게 한다.</summary>
        private const float StandPx = 62f * SizeBoost;
        /// <summary>캐릭터 그림 크기 배율(말풍선·글자 크기는 그대로). 1.3 = 원래보다 30% 크게.</summary>
        internal const float SizeBoost = 1.3f;

        private CharacterArt(int id)
        {
            Id = id; Info = Infos[id];
            float s = Scale;
            using (Bitmap ohImg = LoadRaw(PoseStand.Res))
            using (Bitmap calmImg = CalmFace(ohImg, PoseStand))
            {
                float f = StandPx * s / ohImg.Height;
                StandOh = Make(ohImg, PoseStand, f, 1f, true);
                Stand = Make(calmImg, PoseStand, f, 1f, true);
                W = Stand.W; H = Stand.H;
                Ceiling = FromBitmap(Rotated(Stand.Right, RotateFlipType.Rotate180FlipNone), null, 0, false);
                CeilingTopInset = Ceiling.Ink.Top;
                // 포즈마다 머리 크기가 조금 달라 보정 배율을 둔다.
                Sit = Pose(PoseLie, f, .92f, true);
                Sleep = Pose(PoseSleep, f, .98f, false);
                TouchCry = Pose(PoseCry, f, .93f, false);
                Angry = Pose(PoseAngry, f, .98f, false);
            }
            RotorH = (int)Math.Round(H * .46f);
            IdleCry = TouchCry; Slide = Sit; DizzyPose = StandOh;
        }

        private Sprite Pose(PoseDef def, float f, float k, bool closedVariant)
        {
            using (Bitmap img = LoadRaw(def.Res)) return Make(img, def, f * k, 1f, closedVariant);
        }

        internal static Bitmap LoadRaw(string name)
        {
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (st == null) throw new InvalidOperationException("그림을 찾지 못했어요: " + name);
                using (var b = new Bitmap(st)) return new Bitmap(b);
            }
        }

        internal static Rectangle Bounds(Bitmap b, int threshold = 30)
        {
            int l = b.Width, t = b.Height, r = -1, bot = -1;
            var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * b.Height]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                for (int y = 0; y < b.Height; y++)
                    for (int x = 0; x < b.Width; x++)
                        if (bytes[y * data.Stride + x * 4 + 3] > threshold) { if (x < l) l = x; if (y < t) t = y; if (x > r) r = x; if (y > bot) bot = y; }
            }
            finally { b.UnlockBits(data); }
            return r < l ? new Rectangle(0, 0, b.Width, b.Height) : Rectangle.FromLTRB(l, t, r + 1, bot + 1);
        }

        internal static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>큰 그림을 단계적으로 절반씩 줄여 선이 깨지지 않게 축소한다.</summary>
        internal static Bitmap Downscale(Bitmap src, int w, int h)
        {
            Bitmap cur = (Bitmap)src.Clone();
            while (cur.Width > w * 2 || cur.Height > h * 2)
            {
                int nw = Math.Max(w, cur.Width / 2), nh = Math.Max(h, cur.Height / 2);
                var next = new Bitmap(nw, nh, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(next))
                using (var attr = new ImageAttributes())
                {
                    Quality(g); g.Clear(Color.Transparent); attr.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(cur, new Rectangle(0, 0, nw, nh), 0, 0, cur.Width, cur.Height, GraphicsUnit.Pixel, attr);
                }
                cur.Dispose(); cur = next;
            }
            var final = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(final))
            using (var attr = new ImageAttributes())
            {
                Quality(g); g.Clear(Color.Transparent); attr.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(cur, new Rectangle(0, 0, w, h), 0, 0, cur.Width, cur.Height, GraphicsUnit.Pixel, attr);
            }
            cur.Dispose(); return final;
        }

        // ───────── 고해상도 원본에서 직접 그리는 얼굴 변형 ─────────

        /// <summary>서 있는 그림의 벌어진 입을 털색으로 덮고 작은 고양이 입(ω)을 그려 차분한 얼굴로 만든다.</summary>
        private static Bitmap CalmFace(Bitmap src, PoseDef def)
        {
            var b = (Bitmap)src.Clone();
            float mx = def.Mouth[0], my = def.Mouth[1];
            using (var g = Graphics.FromImage(b))
            using (var fur = new SolidBrush(Fur))
            using (var pen = new Pen(Ink2, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(fur, mx - 13f, my - 12f, 26f, 28f);
                g.DrawArc(pen, mx - 8.5f, my - 4f, 8.5f, 7f, 0, 180);
                g.DrawArc(pen, mx, my - 4f, 8.5f, 7f, 0, 180);
                g.DrawLine(pen, mx, my - 12f, mx, my - 4f + 1f);
            }
            return b;
        }

        /// <summary>눈동자를 털색으로 덮고 감은 눈(아래로 휜 선)을 그린다.</summary>
        private static Bitmap ClosedEyes(Bitmap src, PoseDef def)
        {
            var b = (Bitmap)src.Clone();
            using (var g = Graphics.FromImage(b))
            using (var fur = new SolidBrush(Fur))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 2; i++)
                {
                    float x = def.Eyes[i * 3], y = def.Eyes[i * 3 + 1], r = def.Eyes[i * 3 + 2];
                    g.FillEllipse(fur, x - r * 1.18f, y - r * 1.25f, r * 2.36f, r * 2.5f);
                    using (var pen = new Pen(Ink2, Math.Max(2.2f, r * .32f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(pen, x - r * .95f, y - r * .75f, r * 1.9f, r * 1.2f, 15, 150);
                }
            }
            return b;
        }

        /// <summary>그림을 배율 f 로 줄여 바닥 중앙 기준 캔버스에 놓는다. 투명 여백은 먼저 잘라낸다.</summary>
        private Sprite Make(Bitmap raw, PoseDef def, float f, float unused, bool closedVariant)
        {
            Rectangle crop = Bounds(raw, 8);
            Bitmap open = Cropped(raw, crop), closed = null;
            if (closedVariant && def.Eyes != null)
                using (Bitmap c = ClosedEyes(raw, def)) closed = Cropped(c, crop);
            if (def.Flip) { open.RotateFlip(RotateFlipType.RotateNoneFlipX); }

            float pad = (float)Math.Ceiling(3 * Scale);
            int bw = Math.Max(1, (int)Math.Round(crop.Width * f)), bh = Math.Max(1, (int)Math.Round(crop.Height * f));
            int cw = (int)Math.Ceiling(bw + pad * 2), ch = bh + 1, dx = (cw - bw) / 2, dy = ch - 1 - bh;
            Bitmap canvasOpen = Place(open, bw, bh, cw, ch, dx, dy);
            Bitmap canvasClosed = closed == null ? null : Place(closed, bw, bh, cw, ch, dx, dy);
            open.Dispose(); if (closed != null) closed.Dispose();

            PointF? el = null, er = null; float rad = 0;
            if (def.Eyes != null)
            {
                Func<float, float, PointF> map = (px, py) => new PointF(dx + (px - crop.X) * bw / (float)crop.Width, dy + (py - crop.Y) * bh / (float)crop.Height);
                el = map(def.Eyes[0], def.Eyes[1]); er = map(def.Eyes[3], def.Eyes[4]);
                rad = (def.Eyes[2] + def.Eyes[5]) / 2f * f;
            }
            Sprite sp = FromBitmap(canvasOpen, el, rad, true, er);
            if (def.Mouth != null)
            {
                sp.MouthP = new PointF(dx + (def.Mouth[0] - crop.X) * bw / (float)crop.Width, dy + (def.Mouth[1] - crop.Y) * bh / (float)crop.Height);
                sp.HasMouth = true;
            }
            if (def.Grip != null)
            {
                sp.GripP = new PointF(dx + (def.Grip[0] - crop.X) * bw / (float)crop.Width, dy + (def.Grip[1] - crop.Y) * bh / (float)crop.Height);
                sp.HasGrip = true;
            }
            if (canvasClosed != null) { sp.ClosedRight = canvasClosed; sp.ClosedLeft = (Bitmap)canvasClosed.Clone(); sp.ClosedLeft.RotateFlip(RotateFlipType.RotateNoneFlipX); }
            return sp;
        }

        private static Bitmap Cropped(Bitmap b, Rectangle r)
        {
            var o = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(o)) { g.DrawImage(b, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel); }
            return o;
        }

        private static Bitmap Place(Bitmap img, int bw, int bh, int cw, int ch, int dx, int dy)
        {
            using (Bitmap small = Downscale(img, bw, bh))
            {
                var canvas = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(canvas))
                using (var attr = new ImageAttributes())
                {
                    Quality(g); g.Clear(Color.Transparent); attr.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(small, new Rectangle(dx, dy, bw, bh), 0, 0, small.Width, small.Height, GraphicsUnit.Pixel, attr);
                }
                return canvas;
            }
        }

        private static Bitmap Rotated(Bitmap src, RotateFlipType t) { var b = (Bitmap)src.Clone(); b.RotateFlip(t); return b; }

        private static Sprite FromBitmap(Bitmap right, PointF? eyeL, float eyeRad, bool hasEyes, PointF? eyeR = null)
        {
            var left = (Bitmap)right.Clone(); left.RotateFlip(RotateFlipType.RotateNoneFlipX);
            var s = new Sprite { Right = right, Left = left };
            if (eyeL.HasValue && eyeR.HasValue) { s.EyeL = eyeL.Value; s.EyeR = eyeR.Value; s.HasEyes = true; s.EyeRad = eyeRad; }
            s.Ink = Bounds(right);
            return s;
        }

        /// <summary>트레이·창 아이콘용 작은 그림.</summary>
        internal static Icon MakeIcon(int id)
        {
            try
            {
                using (Bitmap full = LoadRaw(PoseStand.Res))
                using (Bitmap raw = Cropped(full, Bounds(full, 8)))
                {
                    int n = Math.Max(16, (int)Math.Round(16 * Scale));
                    using (var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb))
                    {
                        float f = Math.Min((n - 1f) / raw.Width, (n - 1f) / raw.Height);
                        int w = Math.Max(1, (int)Math.Round(raw.Width * f)), h = Math.Max(1, (int)Math.Round(raw.Height * f));
                        using (Bitmap small = Downscale(raw, w, h))
                        using (var g = Graphics.FromImage(bmp)) { Quality(g); g.Clear(Color.Transparent); g.DrawImage(small, (n - w) / 2f, (n - h) / 2f, w, h); }
                        IntPtr hicon = bmp.GetHicon();
                        Icon icon = (Icon)Icon.FromHandle(hicon).Clone(); Native.DestroyIcon(hicon); return icon;
                    }
                }
            }
            catch { return SystemIcons.Application; }
        }

        /// <summary>장식을 얹을 때 쓰는 보조: 스프라이트 기준 눈 위치(없으면 얼굴 위쪽을 추정).</summary>
        internal static PointF[] EyePoints(Sprite sp, bool left)
        {
            if (sp.HasEyes) return sp.Eyes(left);
            float y = sp.H * .3f;
            return new[] { new PointF(sp.W * .36f, y), new PointF(sp.W * .66f, y) };
        }
    }
}
