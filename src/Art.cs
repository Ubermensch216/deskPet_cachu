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
    internal enum CharKind { Pikachu, Bulbasaur, Charmander, Squirtle }

    /// <summary>한 포즈의 그림. Right = 오른쪽을 보는 모습, Left = 좌우 반전. Closed* = 눈을 감은 같은 포즈(깜빡임·졸음).</summary>
    internal sealed class Sprite
    {
        public Bitmap Right, Left, ClosedRight, ClosedLeft;
        public CharKind Kind;
        public PointF EyeL, EyeR;          // 오른쪽 보기 기준 눈 위치(스프라이트 픽셀, 왼쪽 위 원점)
        public PointF MouthP;              // 입 위치(오른쪽 보기 기준)
        public PointF GripP;               // 프로펠러 자루를 쥐는 자리(오른쪽 보기 기준)
        public PointF TailP;               // 꼬리 끝(파이리 불꽃이 붙는 자리)
        public PointF BulbP;               // 꽃봉오리 가운데(이상해씨)
        public float EyeRad;               // 눈동자 반지름(스프라이트 픽셀) — 장식 크기 기준
        public bool HasEyes, HasMouth, HasGrip, HasTail, HasBulb, Spiral;
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
        public PointF At(PointF p, bool left) { return left ? new PointF(W - p.X, p.Y) : p; }
        public PointF Mouth(bool left) { return At(MouthP, left); }
    }

    internal sealed class CharInfo
    {
        public string Key, Name, SkillName;
        public CharKind Kind;
        public Color Accent;                  // 말풍선·꽃 장식 등에 어울리는 색
    }

    /// <summary>포즈 한 장의 정보. 좌표는 ref\*.png 원본 그림의 픽셀 좌표(캔버스 기준).</summary>
    internal sealed class PoseDef
    {
        public string Res;
        public bool Flip;                      // 그림이 왼쪽을 보고 있어 오른쪽 보기로 뒤집어야 하면 true
        public float[] Eyes;                   // {왼눈 x, y, 가로반지름, 세로반지름, 오른눈 x, y, 가로반지름, 세로반지름}(눈을 덮을 크기) 없으면 null
        public float EyeRad;                   // 장식(눈물·소용돌이·고글)이 기준으로 삼는 눈동자 반지름
        public float[] Mouth, Grip, Tail, Bulb;// {x, y} 없으면 null
        public Color Skin;                     // 눈을 감을 때 눈 주변을 덮는 얼굴색
        public float Scale = 1f;               // 캐릭터마다 크기 보정
    }

    /// <summary>
    /// ref 폴더의 그림으로 걷기·낙하·벽·천장·울기·낮잠 등 모든 모습을 만든다.
    /// 카츄는 포즈 그림이 여섯 장이고, 이상해씨·파이리·꼬부기는 한 장뿐이라 같은 그림을 변형해서 쓴다.
    /// 장식(프로펠러·눈물·불꽃·번개 등)은 PetRender / PetCharacters 에서 코드로 얹는다.
    /// </summary>
    internal sealed class CharacterArt
    {
        internal static readonly CharInfo[] Infos =
        {
            new CharInfo { Key = "cachu", Name = "카츄", Kind = CharKind.Pikachu, SkillName = "번개 방출", Accent = Color.FromArgb(255, 214, 80) },
            new CharInfo { Key = "bulba", Name = "이상해씨", Kind = CharKind.Bulbasaur, SkillName = "덩굴채찍·잎날가르기", Accent = Color.FromArgb(130, 200, 170) },
            new CharInfo { Key = "charm", Name = "파이리", Kind = CharKind.Charmander, SkillName = "불꽃 뿜기", Accent = Color.FromArgb(255, 140, 60) },
            new CharInfo { Key = "squir", Name = "꼬부기", Kind = CharKind.Squirtle, SkillName = "물대포", Accent = Color.FromArgb(140, 210, 230) },
        };
        internal static int Count { get { return Infos.Length; } }
        internal static float Scale = 1f;

        private static float O(float v) { return v + 60; }   // 단체 그림 좌표 + 캔버스 여백(60)

        // ── 카츄(포즈 6장 중 5장) ──
        private static readonly Color Fur = Color.FromArgb(242, 214, 100);
        private static readonly Color Ink2 = Color.FromArgb(70, 38, 20);
        private static readonly PoseDef PoseLie = new PoseDef { Res = "pose1.png", Skin = Fur, EyeRad = 9f, Eyes = new[] { 185f, 132f, 9.5f, 9.5f, 236f, 128f, 7f, 7f }, Mouth = new[] { 221f, 157f } };
        private static readonly PoseDef PoseSleep = new PoseDef { Res = "pose2.png", Flip = true, Skin = Fur };
        private static readonly PoseDef PoseCry = new PoseDef { Res = "pose3.png", Skin = Fur, EyeRad = 8f, Eyes = new[] { 66f, 53f, 8f, 8f, 113f, 56f, 8.5f, 8.5f }, Mouth = new[] { 88f, 86f } };
        private static readonly PoseDef PoseStand = new PoseDef { Res = "pose4.png", Skin = Fur, EyeRad = 9.5f, Eyes = new[] { 120f, 109f, 9.5f, 9.5f, 168f, 100f, 9.5f, 9.5f }, Mouth = new[] { 148f, 130f }, Grip = new[] { 203f, 163f } };
        private static readonly PoseDef PoseAngry = new PoseDef { Res = "pose6.png", Skin = Fur, EyeRad = 11f, Eyes = new[] { 79f, 82f, 11f, 11f, 130f, 83f, 11f, 11f }, Mouth = new[] { 104f, 100f } };

        // ── 한 장짜리 친구들 (tools\extract-starters.ps1 이 만든 그림) ──
        private static readonly PoseDef DefBulba = new PoseDef
        {
            Res = "bulbasaur.png", Flip = true, Scale = .96f, Skin = Color.FromArgb(128, 200, 168), EyeRad = 13f,
            Eyes = new[] { O(78), O(166), 17f, 26f, O(194), O(206), 28f, 26f },
            Mouth = new[] { O(130), O(240) }, Bulb = new[] { O(282), O(108) }, Grip = new[] { O(282), O(150) }
        };
        private static readonly PoseDef DefCharm = new PoseDef
        {
            Res = "charmander.png", Flip = true, Scale = 1f, Skin = Color.FromArgb(240, 124, 52), EyeRad = 9f,
            Eyes = new[] { O(399), O(127), 10f, 18f, O(489), O(127), 15f, 24f },
            Mouth = new[] { O(452), O(190) }, Tail = new[] { O(633), O(266) }, Grip = new[] { O(578), O(240) }
        };
        private static readonly PoseDef DefSquir = new PoseDef
        {
            Res = "squirtle.png", Scale = .96f, Skin = Color.FromArgb(160, 216, 208), EyeRad = 11f,
            Eyes = new[] { O(604), O(98), 15f, 21f, O(675), O(102), 13f, 23f },
            Mouth = new[] { O(638), O(152) }, Grip = new[] { O(735), O(172) }
        };
        private static readonly PoseDef DefShell = new PoseDef { Res = "squirtle_shell.png", Skin = Color.White };

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
        public CharKind Kind { get { return Info.Kind; } }
        /// <summary>Stand 서기(카츄는 입 다문 차분한 얼굴) · StandOh 놀란 얼굴(카츄 전용, 나머지는 Stand 와 같음) · Sit 앉기·엎드림 · Sleep 잠 · TouchCry 울기 · Angry 화남(카츄 전용)
        /// · Shell 등딱지에 숨은 꼬부기</summary>
        public Sprite Stand, StandOh, Sit, Sleep, TouchCry, IdleCry, Slide, DizzyPose, Ceiling, Angry, Shell;
        public int W, H;                 // 기본 그림 크기(걷기 기준) — 이동·충돌 기준
        public int RotorH;               // 프로펠러가 머리 위로 올라오는 높이
        public int CeilingTopInset;
        public int CeilingContactHeight { get { return Ceiling.H - CeilingTopInset; } }
        /// <summary>프로펠러 자루를 쥔 손의 가로 위치(스프라이트 가운데 기준)와 프로펠러 중심까지의 높이 — 낙하할 때 이 점을 축으로 흔들린다.</summary>
        public float PoleX { get { return StandOh.HasGrip ? StandOh.GripP.X - StandOh.W / 2f : 0; } }
        public float HubHeight { get { return H + RotorH * .72f; } }
        /// <summary>같은 조건에서 캐릭터 모두 비슷한 크기로 보이게 하는 기준 높이(픽셀, 배율 1)</summary>
        private const float StandPx = 62f * SizeBoost;
        /// <summary>캐릭터 그림 크기 배율(말풍선·글자 크기는 그대로). 1.3 = 원래보다 30% 크게.</summary>
        internal const float SizeBoost = 1.3f;

        private CharacterArt(int id)
        {
            Id = id; Info = Infos[id];
            switch (Info.Kind)
            {
                case CharKind.Pikachu: BuildPikachu(); break;
                case CharKind.Bulbasaur: BuildSingle(DefBulba); break;
                case CharKind.Charmander: BuildSingle(DefCharm); break;
                default: BuildSingle(DefSquir); break;
            }
            RotorH = (int)Math.Round(H * .46f);
        }

        private void BuildPikachu()
        {
            float s = Scale;
            using (Bitmap ohImg = LoadRaw(PoseStand.Res))
            using (Bitmap calmImg = CalmFace(ohImg, PoseStand))
            {
                float f = StandPx * s / ohImg.Height;
                StandOh = Make(ohImg, PoseStand, f, 1f, 1f, true);
                Stand = Make(calmImg, PoseStand, f, 1f, 1f, true);
                W = Stand.W; H = Stand.H;
                Ceiling = Derive(Stand, RotateFlipType.Rotate180FlipNone);
                CeilingTopInset = Ceiling.Ink.Top;
                // 포즈마다 머리 크기가 조금 달라 보정 배율을 둔다.
                Sit = Pose(PoseLie, f, .92f, true);
                Sleep = Pose(PoseSleep, f, .98f, false);
                TouchCry = Pose(PoseCry, f, .93f, false);
                Angry = Pose(PoseAngry, f, .98f, false);
            }
            IdleCry = TouchCry; Slide = Sit; DizzyPose = StandOh;
        }

        /// <summary>한 장짜리 친구: 같은 그림을 줄이고 눌러서 앉기, 눈 감은 그림으로 잠, 돌려서 슬라이딩·천장을 만든다.</summary>
        private void BuildSingle(PoseDef def)
        {
            float s = Scale;
            using (Bitmap raw = LoadRaw(def.Res))
            {
                float f = StandPx * s / Bounds(raw, 8).Height * def.Scale;
                Stand = Make(raw, def, f, 1f, 1f, true);
                StandOh = Stand; Angry = Stand;
                W = Stand.W; H = Stand.H;
                Sit = Make(raw, def, f, 1.07f, .88f, true);
                Sleep = Sit; TouchCry = Sit; IdleCry = Sit; DizzyPose = Sit;
                Slide = Derive(Stand, RotateFlipType.Rotate90FlipNone);
                Ceiling = Derive(Stand, RotateFlipType.Rotate180FlipNone);
                CeilingTopInset = Ceiling.Ink.Top;
            }
            if (Info.Kind == CharKind.Squirtle)
            {
                // 등딱지에 쏙 숨는 모습(Withdraw): 낮잠·슬라이딩·콕 눌림에 쓴다.
                using (Bitmap raw = LoadRaw(DefShell.Res))
                {
                    float f = H * .78f / Bounds(raw, 8).Height;
                    Shell = Make(raw, DefShell, f, 1f, 1f, false);
                }
                Sleep = Shell; Slide = Shell;
            }
        }

        private Sprite Pose(PoseDef def, float f, float k, bool closedVariant)
        {
            using (Bitmap img = LoadRaw(def.Res)) return Make(img, def, f * k, 1f, 1f, closedVariant);
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

        /// <summary>서 있는 그림의 벌어진 입을 털색으로 덮고 작은 고양이 입(ω)을 그려 차분한 얼굴로 만든다(카츄).</summary>
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

        /// <summary>눈을 얼굴색으로 덮고 감은 눈(아래로 휜 선)을 그린다.</summary>
        private static Bitmap ClosedEyes(Bitmap src, PoseDef def)
        {
            var b = (Bitmap)src.Clone();
            using (var g = Graphics.FromImage(b))
            using (var skin = new SolidBrush(def.Skin))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 2; i++)
                {
                    float x = def.Eyes[i * 4], y = def.Eyes[i * 4 + 1], rx = def.Eyes[i * 4 + 2], ry = def.Eyes[i * 4 + 3];
                    g.FillEllipse(skin, x - rx * 1.16f, y - ry * 1.2f, rx * 2.32f, ry * 2.4f);
                    float ah = Math.Min(ry, rx * 1.2f);
                    using (var pen = new Pen(Ink2, Math.Max(2.2f, rx * .3f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(pen, x - rx * .95f, y - ah * .7f, rx * 1.9f, ah * 1.1f, 15, 150);
                }
            }
            return b;
        }

        /// <summary>그림을 배율 f(가로 sx·세로 sy 로 눌러서)로 줄여 바닥 중앙 기준 캔버스에 놓는다. 투명 여백은 먼저 잘라낸다.</summary>
        private Sprite Make(Bitmap raw, PoseDef def, float f, float sx, float sy, bool closedVariant)
        {
            Rectangle crop = Bounds(raw, 8);
            Bitmap open = Cropped(raw, crop), closed = null;
            if (closedVariant && def.Eyes != null)
                using (Bitmap c = ClosedEyes(raw, def)) closed = Cropped(c, crop);
            if (def.Flip) { open.RotateFlip(RotateFlipType.RotateNoneFlipX); if (closed != null) closed.RotateFlip(RotateFlipType.RotateNoneFlipX); }

            float pad = (float)Math.Ceiling(3 * Scale);
            int bw = Math.Max(1, (int)Math.Round(crop.Width * f * sx)), bh = Math.Max(1, (int)Math.Round(crop.Height * f * sy));
            int cw = (int)Math.Ceiling(bw + pad * 2), ch = bh + 1, dx = (cw - bw) / 2, dy = ch - 1 - bh;
            Bitmap canvasOpen = Place(open, bw, bh, cw, ch, dx, dy);
            Bitmap canvasClosed = closed == null ? null : Place(closed, bw, bh, cw, ch, dx, dy);
            open.Dispose(); if (closed != null) closed.Dispose();

            Func<float[], PointF> map = a =>
            {
                float u = (a[0] - crop.X) / (float)crop.Width; if (def.Flip) u = 1 - u;
                return new PointF(dx + u * bw, dy + (a[1] - crop.Y) / (float)crop.Height * bh);
            };
            var sp = new Sprite { Right = canvasOpen, Kind = Info.Kind };
            sp.Left = (Bitmap)canvasOpen.Clone(); sp.Left.RotateFlip(RotateFlipType.RotateNoneFlipX);
            sp.Ink = Bounds(canvasOpen);
            if (def.Eyes != null)
            {
                PointF a = map(new[] { def.Eyes[0], def.Eyes[1] }), b = map(new[] { def.Eyes[4], def.Eyes[5] });
                sp.EyeL = a.X <= b.X ? a : b; sp.EyeR = a.X <= b.X ? b : a; sp.HasEyes = true; sp.EyeRad = def.EyeRad * f;
            }
            if (def.Mouth != null) { sp.MouthP = map(def.Mouth); sp.HasMouth = true; }
            if (def.Grip != null) { sp.GripP = map(def.Grip); sp.HasGrip = true; }
            if (def.Tail != null) { sp.TailP = map(def.Tail); sp.HasTail = true; }
            if (def.Bulb != null) { sp.BulbP = map(def.Bulb); sp.HasBulb = true; }
            if (canvasClosed != null) { sp.ClosedRight = canvasClosed; sp.ClosedLeft = (Bitmap)canvasClosed.Clone(); sp.ClosedLeft.RotateFlip(RotateFlipType.RotateNoneFlipX); }
            return sp;
        }

        /// <summary>그림을 90°/180° 돌린 스프라이트(슬라이딩·천장). 눈·입·꼬리 위치도 같이 돌린다.</summary>
        private static Sprite Derive(Sprite src, RotateFlipType t)
        {
            var right = (Bitmap)src.Right.Clone(); right.RotateFlip(t);
            var left = (Bitmap)right.Clone(); left.RotateFlip(RotateFlipType.RotateNoneFlipX);
            var s = new Sprite { Right = right, Left = left, Kind = src.Kind, EyeRad = src.EyeRad, Ink = Bounds(right) };
            int w = src.W, h = src.H;
            Func<PointF, PointF> m = p => t == RotateFlipType.Rotate180FlipNone ? new PointF(w - p.X, h - p.Y) : new PointF(h - p.Y, p.X);
            if (src.HasEyes) { PointF a = m(src.EyeL), b = m(src.EyeR); s.EyeL = a.X <= b.X ? a : b; s.EyeR = a.X <= b.X ? b : a; s.HasEyes = true; }
            if (src.HasMouth) { s.MouthP = m(src.MouthP); s.HasMouth = true; }
            if (src.HasTail) { s.TailP = m(src.TailP); s.HasTail = true; }
            if (src.HasBulb) { s.BulbP = m(src.BulbP); s.HasBulb = true; }
            return s;
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

        /// <summary>트레이·창 아이콘용 작은 그림.</summary>
        internal static Icon MakeIcon(int id)
        {
            try
            {
                CharInfo info = Infos[((id % Count) + Count) % Count];
                string res = info.Kind == CharKind.Pikachu ? PoseStand.Res : info.Kind == CharKind.Bulbasaur ? DefBulba.Res : info.Kind == CharKind.Charmander ? DefCharm.Res : DefSquir.Res;
                using (Bitmap full = LoadRaw(res))
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
