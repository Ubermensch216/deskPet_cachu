using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;

namespace Cachu
{
    [Flags]
    internal enum Decor
    {
        None = 0, Rotor = 1, Tears = 2, Spiral = 4, Stars = 8, Flower = 16, Goggles = 32, Notes = 64, Cookie = 128, Speed = 256, Sleepy = 512, Blink = 1024, Dots = 2048, Skill = 4096, Flame = 8192, Sun = 16384, Shades = 32768, Jet = 65536
    }

    internal sealed partial class Pet
    {
        /// <summary>한 순간의 모습: 어떤 그림을 어떻게 변형하고 어떤 장식·말풍선을 얹을지.</summary>
        internal struct Look
        {
            public Sprite Sprite; public bool Left;
            public float Sx, Sy, Angle, Px, Py, HopY;
            public string Bubble;
            public int ZPhase; public float ZTime;
            public Decor Decor; public float DecorPhase;
            public string AffinityLabel; public bool AffinityOnLeft;
            public float Flash;   // 번개 순간 몸이 하얗게 번쩍이는 정도 0~1
            public float T, Flame;   // 펫이 살아온 시간(불꽃·햇살의 일렁임) / 파이리 꼬리 불꽃 세기

            public string Key()
            {
                return string.Format("{0}|{1}|{2:0.00}|{3:0.00}|{4:0.0}|{5:0}|{6:0}|{7}|{8}|{9}|{10}|{11}|{12}|{13:0}|{14}|{15:0}",
                    RuntimeHelpers.GetHashCode(Sprite), Left ? 1 : 0, Sx, Sy, Angle, Px, Py, Bubble ?? "", ZPhase, (int)Decor,
                    Decor == Decor.None ? 0 : (int)(DecorPhase * 24), AffinityLabel, AffinityOnLeft, Flash * 12,
                    (Decor & (Decor.Flame | Decor.Sun | Decor.Jet)) != 0 ? (int)(T * 16) : 0, Flame * 10);
            }
        }

        /// <summary>모양(가로세로 비율)은 절대 바꾸지 않는다 — 균일 확대와 발끝을 축으로 한 회전만 쓴다.</summary>
        private static void Uni(ref Look L, double k) { L.Sx *= (float)k; L.Sy *= (float)k; }

        private double ClimbPhase { get { return animT * Math.PI * 2 * 1.9; } }

        /// <summary>걷는 몸짓: 발끝을 축으로 좌우 ±6° 뒤뚱, 발을 바꾸는 사이에만 살짝 뜬다.</summary>
        private void Waddle(ref Look L)
        {
            if (walkAmp <= .01) return;
            double a = walkAmp;
            L.Angle += (float)(6 * a * Math.Sin(walkPhase));
            L.HopY += (float)(-Math.Abs(Math.Cos(walkPhase)) * 2.2 * S * a);
        }

        /// <summary>눈 깜빡임: 펫마다 어긋난 박자로 약 4초에 한 번(가끔 두 번).</summary>
        private bool Blinking(double t)
        {
            double c = (t + blinkOffset) % 4.3;
            return c < .13 || (c > 1.1 && c < 1.22 && ((int)((t + blinkOffset) / 4.3) % 3 == 0));
        }

        /// <summary>잠: 눈을 감고 숨 쉴 때마다 균일하게 부풀며, Z 세 개가 시차를 두고 떠오른다.</summary>
        private void SleepLook(ref Look L, double t)
        {
            L.Sprite = Art.Sleep; Uni(ref L, 1 + .015 * Math.Sin(t * 1.7));
            L.ZPhase = (int)(t * 20) % 100000; L.ZTime = (float)t;
            if (Art.Kind != CharKind.Pikachu) L.Decor |= Decor.Sleepy;   // 카츄 말고는 같은 그림의 눈 감은 모습
        }

        public Look GetLook(double now)
        {
            var L = new Look { Sx = 1, Sy = 1, ZPhase = -1, Left = Dir < 0, Sprite = Art.Stand };
            lookJitterX = 0;
            switch (State)
            {
                case PetState.Walk: case PetState.Gather: Waddle(ref L); break;
                case PetState.Pause: case PetState.WaitHome: Waddle(ref L); Uni(ref L, 1 + Math.Sin(animT * 2.0) * .01); break;
                case PetState.Climb: L.Left = climbDir < 0; Waddle(ref L); break;
                case PetState.Falling: case PetState.PilotRise:
                    // 프로펠러 아래에 매달려 천천히 흔들린다: 회전축은 프로펠러 쪽.
                    L.Sprite = Art.StandOh; L.Left = false; L.Decor |= Decor.Rotor; L.DecorPhase = (float)StateT;
                    // 우산을 쥔 것처럼 자루 꼭대기(프로펠러 중심)를 축으로 대롱대롱 흔들린다.
                    L.Px = Art.PoleX; L.Py = -Art.HubHeight;
                    L.Angle = (float)(2.6 * Math.Sin(StateT * 2.4) + .8 * Math.Sin(StateT * 5.3));
                    break;
                case PetState.Squish:
                {
                    // 눌림도 비율은 그대로: 균일하게 살짝 줄었다 돌아온다.
                    double k = Math.Sin(Math.PI * Math.Min(1, StateT / squishDur));
                    if (squishAmp >= .2)   // 콕 눌렸을 때: 카츄는 발끈, 꼬부기는 등딱지에 쏙, 나머지는 파르르
                        switch (Art.Kind)
                        {
                            case CharKind.Pikachu: L.Sprite = Art.Angry; L.Left = false; L.Angle = (float)(Math.Sin(StateT * 46) * 2.2 * k); break;
                            case CharKind.Squirtle: L.Sprite = Art.Shell; L.Left = false; L.Angle = (float)(Math.Sin(StateT * 34) * 3 * k); break;
                            default: L.Angle = (float)(Math.Sin(StateT * 46) * 2.6 * k); break;
                        }
                    Uni(ref L, 1 - squishAmp * .4 * k);
                    break;
                }
                case PetState.Drag:
                    L.Angle = (float)angle; L.Px = (float)DragPx; L.Py = (float)DragPy; L.Left = false;
                    L.Sprite = Math.Abs(angle) > 22 ? Art.Angry : Art.StandOh;   // 대롱대롱: 놀람, 세게 흔들리면 발끈
                    break;
                case PetState.Thrown:
                    L.Sprite = Art.Angry; L.Left = false; L.Px = 0; L.Py = -Art.H * .5f;
                    L.Angle = (float)(Math.Max(-32, Math.Min(32, throwVx / (900 * S) * 30)) + Math.Sin(StateT * 13) * 6);
                    break;
                case PetState.Sleep: SleepLook(ref L, animT); break;
                case PetState.Waking:
                    // 깨어나며 한 번 크게 기지개(균일 확대 + 발끝 회전) 후 눈을 뜬다.
                    if (StateT < .3) { L.Sprite = Art.Sleep; Uni(ref L, 1 + .03 * Math.Sin(Math.PI * StateT / .3)); if (Art.Kind != CharKind.Pikachu) L.Decor |= Decor.Sleepy; }
                    else if (StateT < .85)
                    {
                        // 일어나 하품하며 기지개(놀란 입 = 하품), 눈은 아직 감김
                        double q = Math.Sin(Math.PI * (StateT - .3) / .55);
                        L.Sprite = Art.StandOh; Uni(ref L, 1 + .055 * q); L.Angle = (float)(-2.4 * q);
                        if (StateT < .65) L.Decor |= Decor.Sleepy;
                    }
                    break;
                case PetState.Cheer:
                    if (now < cheerHopUntil)
                    {
                        L.Sprite = Art.StandOh;
                        double q = StateT % .42 / .42;
                        L.HopY = (float)(-Math.Sin(Math.PI * q) * 11 * S);
                        L.Angle = (float)(Math.Sin(q * Math.PI * 2) * 3);
                    }
                    break;
                case PetState.Resting:
                    RestLook(ref L, now);
                    break;
                case PetState.Eating:
                {
                    // 간식을 오물오물: 씹을 때마다 가볍게 균일하게 들썩.
                    double chew = Math.Sin(animT * 2.4 * Math.PI * 2);
                    L.Sprite = Art.Sit; Uni(ref L, 1 + .014 * chew); L.Angle = (float)(chew * .8);
                    L.Decor |= Decor.Cookie; L.DecorPhase = (float)animT;
                    break;
                }
                case PetState.IdleCry:
                {
                    bool cry = FriendData.Current.Hungry || previewIdleCry;
                    L.Sprite = cry ? Art.IdleCry : Art.Sit; L.Left = false; Uni(ref L, 1 + Math.Sin(StateT * 8) * .01);
                    if (cry) { lookJitterX = Math.Sin(StateT * 21) * .45 * S; L.Decor |= Decor.Tears; L.DecorPhase = (float)StateT; }
                    break;
                }
                case PetState.TouchCry:
                    // 달달 떨며 운다: 처음에 움찔(균일 축소) 후 흐느낌.
                    L.Sprite = Art.TouchCry; L.Left = false; lookJitterX = Math.Sin(StateT * 52) * .6 * S;
                    L.HopY = (float)(Math.Sin(StateT * 40) * .4 * S); L.Decor |= Decor.Tears; L.DecorPhase = (float)StateT;
                    Uni(ref L, 1 + Math.Sin(StateT * 11) * .012 - (StateT < .18 ? .06 * Math.Sin(StateT / .18 * Math.PI) : 0));
                    break;
                case PetState.Skill: SkillLook(ref L); break;
                case PetState.Hop:
                {
                    // 웅크림 → 점프 → 착지 펄스. 늘이거나 누르지 않고 균일 확대와 기울기만.
                    double t = StateT / extraDuration;
                    L.Sprite = Art.StandOh;
                    if (t < .16) Uni(ref L, 1 - .07 * Math.Sin(t / .16 * Math.PI));
                    else if (t < .84)
                    {
                        double q = (t - .16) / .68; L.HopY = (float)(-Math.Sin(q * Math.PI) * 22 * S);
                        L.Angle = (float)(Math.Sin(q * Math.PI * 2) * 5); Uni(ref L, 1 + .03 * Math.Sin(q * Math.PI));
                    }
                    else Uni(ref L, 1 - .07 * Math.Sin((t - .84) / .16 * Math.PI));
                    break;
                }
                case PetState.Sliding:
                    L.DecorPhase = (float)animT;
                    if (Art.Kind == CharKind.Squirtle)
                    {
                        // 등딱지에 숨어 빙글빙글 돌며 미끄러진다. 뒤로 물을 뿜어 나아간다.
                        L.Sprite = Art.Shell; L.Px = 0; L.Py = -Art.Shell.H / 2f; L.Angle = (float)((Dir * animT * 760) % 360);
                        L.HopY = (float)(Math.Sin(animT * 20) * .5 * S); L.Decor |= Decor.Jet;
                        break;
                    }
                    L.Sprite = Art.Slide; L.HopY = (float)(Math.Sin(animT * 20) * .6 * S);
                    L.Angle = (float)(-Dir * Math.Sin(animT * 9) * 2.5);
                    L.Decor |= Decor.Speed;
                    break;
                case PetState.Wall: case PetState.WindowClimb:
                {
                    // 발을 벽에 대고 몸을 옆으로 눕혀 걷는다(머리는 벽 반대쪽). 걷기와 같은 뒤뚱을 발끝 축으로 얹는다.
                    int wallDir = State == PetState.Wall ? wallSide : -approachSide;
                    bool down = State == PetState.Wall && wallDescending;
                    L.Left = wallDir < 0;
                    L.Angle = (float)(-wallDir * (down ? WallDescendDeg : WallAscendDeg) + 6 * Math.Sin(ClimbPhase));
                    L.HopY = 0;
                    L.Px = 0; L.Py = 0;
                    // 벽에서의 '뜸'은 벽 반대쪽으로 살짝 떨어졌다 붙는 것
                    lookJitterX = -wallDir * Math.Abs(Math.Cos(ClimbPhase)) * 2.0 * S;
                    break;
                }
                case PetState.Ceiling:
                {
                    // 천장에 발을 붙이고 거꾸로 걷는다: 뒤뚱의 축은 천장에 닿은 발끝.
                    L.Sprite = Art.Ceiling; L.Left = Dir < 0;
                    L.Px = 0; L.Py = -Art.Ceiling.H;
                    L.Angle = (float)(6 * Math.Sin(ClimbPhase));
                    break;
                }
                case PetState.Dizzy:
                    L.Sprite = Art.DizzyPose; L.Left = false; L.Angle = (float)(Math.Sin(StateT * 7) * 6);
                    L.Decor |= Decor.Stars; if (!L.Sprite.Spiral) L.Decor |= Decor.Spiral; L.DecorPhase = (float)StateT;
                    break;
                case PetState.SecretDance:
                {
                    double phase = now - danceStarted;
                    L.Left = (int)(phase / .6) % 2 == 0;
                    L.HopY = (float)(-Math.Abs(Math.Sin(phase * Math.PI * 2.5)) * 12 * S);
                    L.Angle = (float)(Math.Sin(phase * Math.PI * 2.5) * 9);
                    L.Decor |= Decor.Notes; L.DecorPhase = (float)phase;
                    break;
                }
            }
            L.Bubble = PersistentBubble(hourlyBubble, Bubble);
            CompanionLook(ref L, now);
            CharacterLook(ref L, now);
            bool calm = State == PetState.Walk || State == PetState.Pause || (State == PetState.Resting && restPose != RestPose.Sleep) || State == PetState.WaitHome || State == PetState.Gather || State == PetState.Cheer;
            if (calm && (L.Decor & (Decor.Tears | Decor.Sleepy | Decor.Spiral)) == 0 && Blinking(now)) L.Decor |= Decor.Blink;
            return L;
        }

        /// <summary>앉아 쉬는 네 가지 자세. 새 그림 없이 움직임만으로 구분한다.</summary>
        private void RestLook(ref Look L, double now)
        {
            double t = StateT;
            switch (restPose)
            {
                case RestPose.Sleep: SleepLook(ref L, t); break;
                case RestPose.Daydream:
                    // 멍때리기: 몸이 아주 느리게 흔들리고 생각 점 세 개가 차례로 떠오른다.
                    L.Sprite = Art.Sit; L.Angle = (float)(1.6 * Math.Sin(t * .85));
                    L.Decor |= Decor.Dots; L.DecorPhase = (float)t;
                    break;
                case RestPose.Stretch:
                {
                    // 기지개: 한 번 크게 펴졌다가(균일 확대 + 발끝 회전) 다시 웅크린다.
                    double pulse = Math.Sin((extraDuration <= 0 ? 1 : t / extraDuration) * Math.PI * 2);
                    L.Sprite = pulse > .15 ? Art.StandOh : Art.Sit; Uni(ref L, 1 + .055 * pulse); L.Angle = (float)(-2.4 * pulse);
                    break;
                }
                default:
                    L.Sprite = Art.Sit; Uni(ref L, 1 + Math.Sin(animT * 2.0) * .014);
                    break;
            }
        }

        private void CompanionLook(ref Look look, double now)
        {
            if (PlayOptions.Current.ShowAffinity)
            {
                look.AffinityLabel = FriendData.Current.Affinity.ToString("N0");
                look.AffinityOnLeft = X + Art.W * .5 + (22 + look.AffinityLabel.Length * 8) * S > Area.Right;
            }
            bool hungry = FriendData.Current.Hungry;
            if (hungry && IsCompanionFloorPose)
            {
                look.Sprite = Art.IdleCry; look.Left = false;
                if (State != PetState.Squish) { look.Sx = look.Sy = (float)(1 + Math.Sin(animT * 8) * .01); look.HopY = 0; look.Angle = 0; }
                lookJitterX = Math.Sin(animT * 21) * .45 * S;
                look.Decor |= Decor.Tears; look.DecorPhase = (float)animT;
            }
            if (hungry && (IsCompanionFloorPose || State == PetState.TouchCry) && string.IsNullOrEmpty(look.Bubble) && now >= flowerUntil)
                look.Bubble = "배고파! 간식줘 ㅠㅠ";
            if (now < flowerUntil && IsCompanionFloorPose && !previewIdleCry)
            {
                look.Sprite = Art.Stand; look.Left = false; look.Decor &= ~Decor.Tears; look.Decor |= Decor.Flower; look.DecorPhase = (float)(now - flowerStarted);
                look.Angle = 0; lookJitterX = 0;
            }
            if (IsPilotFlight) { look.Decor |= Decor.Goggles; look.DecorPhase = (float)animT; }
        }

        internal static string PersistentBubble(string hourly, string ordinary)
        {
            if (string.IsNullOrEmpty(hourly)) return ordinary;
            if (string.IsNullOrEmpty(ordinary) || hourly == ordinary || hourly.IndexOf(ordinary, StringComparison.Ordinal) >= 0) return hourly;
            return hourly + "\n" + ordinary;
        }

        // ───────────────────────── 그리기 ─────────────────────────
        private static Font bubbleFont, affinityFont, zFont;
        private static float fontScale;
        private static void EnsureFonts()
        {
            float s = CharacterArt.Scale;
            if (bubbleFont != null && fontScale == s) return;
            fontScale = s;
            try { bubbleFont = new Font("Malgun Gothic", 11f * s, FontStyle.Bold, GraphicsUnit.Pixel); }
            catch { bubbleFont = new Font(FontFamily.GenericSansSerif, 11f * s, FontStyle.Bold, GraphicsUnit.Pixel); }
            try { affinityFont = new Font("Segoe UI", 10f * s, FontStyle.Bold, GraphicsUnit.Pixel); }
            catch { affinityFont = new Font(FontFamily.GenericSansSerif, 10f * s, FontStyle.Bold, GraphicsUnit.Pixel); }
        }
        private static FontFamily zFamily;
        private static FontFamily ZFamily
        {
            get
            {
                if (zFamily == null) { try { zFamily = new FontFamily("Segoe UI"); } catch { zFamily = FontFamily.GenericSansSerif; } }
                return zFamily;
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        /// <summary>장식이 스프라이트 바깥으로 얼마나 나가는지 (스프라이트 좌표, 바닥 가운데 원점).</summary>
        private static RectangleF DecorExtent(Look L, float w, float h, float s, int rotorH)
        {
            float l = 0, t = 0, r = 0, b = 0;
            if ((L.Decor & Decor.Rotor) != 0)
            {
                float px = L.Sprite.HasGrip ? L.Sprite.GripP.X - w / 2 : 0, rad = w * .6f;
                l = Math.Min(l, px - rad); r = Math.Max(r, px + rad); t = Math.Min(t, -h - rotorH - 4 * s);
            }
            if ((L.Decor & Decor.Skill) != 0)
            {
                float ex = L.Sprite.Kind == CharKind.Pikachu ? 1.6f : L.Sprite.Kind == CharKind.Bulbasaur ? 1.8f : 1.95f;
                float ey = L.Sprite.Kind == CharKind.Pikachu ? 2.15f : L.Sprite.Kind == CharKind.Bulbasaur ? 1.95f : 1.3f;
                l = Math.Min(l, -w * ex); r = Math.Max(r, w * ex); t = Math.Min(t, -h * ey);
            }
            if ((L.Decor & Decor.Flame) != 0 && L.Sprite.HasTail)
            {
                PointF a = Anc(L.Sprite, L.Sprite.TailP, L.Left, w, h); float fu = h / 62f;
                l = Math.Min(l, a.X - 16 * fu); r = Math.Max(r, a.X + 16 * fu); t = Math.Min(t, a.Y - (32 * L.Flame + 14) * fu);
            }
            if ((L.Decor & Decor.Sun) != 0) { l = Math.Min(l, -w * .9f); r = Math.Max(r, w * .9f); t = Math.Min(t, -h * 1.5f); }
            if ((L.Decor & Decor.Jet) != 0) { l = Math.Min(l, -w * 1.3f); r = Math.Max(r, w * 1.3f); }
            if ((L.Decor & Decor.Dots) != 0) { r = Math.Max(r, w * .6f); t = Math.Min(t, -h - 14 * s); }
            if ((L.Decor & Decor.Stars) != 0) { l = Math.Min(l, -w * .45f); r = Math.Max(r, w * .45f); t = Math.Min(t, -h - 14 * s); }
            if ((L.Decor & Decor.Flower) != 0) { r = Math.Max(r, w * .38f + 20 * s); t = Math.Min(t, -h * .47f - 16 * s); }
            if ((L.Decor & Decor.Notes) != 0) { l = Math.Min(l, -w * .47f - 20 * s); r = Math.Max(r, w * .47f + 24 * s); t = Math.Min(t, -h * .8f - 18 * s); }
            if ((L.Decor & Decor.Speed) != 0) { l = Math.Min(l, -w * .5f - 24 * s); r = Math.Max(r, w * .5f + 24 * s); }
            return new RectangleF(l, t, r - l, b - t);
        }

        public static Bitmap Render(Look L, out PointF origin)
        {
            EnsureFonts();
            float scale = CharacterArt.Scale;
            Sprite sprite = L.Sprite;
            bool closedEyes = (L.Decor & (Decor.Blink | Decor.Sleepy)) != 0;
            Bitmap bitmap = closedEyes ? sprite.FaceClosed(L.Left) : sprite.Face(L.Left);
            float w = sprite.W, h = sprite.H;
            int rotorH = (int)Math.Round(h * .46f);
            var matrix = new Matrix();
            if (L.Angle != 0) { matrix.Translate(L.Px, L.Py); matrix.Rotate(L.Angle); matrix.Translate(-L.Px, -L.Py); }
            matrix.Scale(L.Sx, L.Sy);
            var corners = new[] { new PointF(-w / 2, -h), new PointF(w / 2, -h), new PointF(w / 2, 0), new PointF(-w / 2, 0) };
            matrix.TransformPoints(corners);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (PointF p in corners) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
            float spriteTop = minY;

            RectangleF ext = DecorExtent(L, w, h, scale, rotorH);
            if (!ext.IsEmpty)
            {
                var ep = new[] { new PointF(ext.Left, ext.Top), new PointF(ext.Right, ext.Top) };
                matrix.TransformPoints(ep);
                foreach (PointF p in ep) { minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); }
                spriteTop = Math.Min(spriteTop, minY);
            }

            SizeF textSize = SizeF.Empty; RectangleF bubble = RectangleF.Empty;
            float tail = 7 * scale;
            if (!string.IsNullOrEmpty(L.Bubble))
            {
                using (var tmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(tmp))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    textSize = g.MeasureString(L.Bubble, bubbleFont, (int)(170 * scale), StringFormat.GenericTypographic);
                }
                float pad = 7 * scale;
                float bw = (float)Math.Ceiling(textSize.Width + pad * 2 + 2 * scale), bh = (float)Math.Ceiling(textSize.Height + pad * 1.4f + 2 * scale);
                bubble = new RectangleF(-bw / 2, spriteTop - 3 * scale - tail - bh, bw, bh);
                minX = Math.Min(minX, bubble.Left); maxX = Math.Max(maxX, bubble.Right); minY = Math.Min(minY, bubble.Top);
            }
            if (L.ZPhase >= 0) { minY = Math.Min(minY, spriteTop - 44 * scale); maxX = Math.Max(maxX, w / 2 + 22 * scale); minX = Math.Min(minX, -w / 2 - 22 * scale); }
            RectangleF badge = AffinityBadgeBounds(L, w, h, scale);
            if (!badge.IsEmpty) { minX = Math.Min(minX, badge.Left); maxX = Math.Max(maxX, badge.Right); minY = Math.Min(minY, badge.Top); maxY = Math.Max(maxY, badge.Bottom); }

            const int margin = 3;
            int bwid = (int)Math.Ceiling(maxX - minX) + margin * 2, bhei = (int)Math.Ceiling(maxY - minY) + margin * 2;
            origin = new PointF(-minX + margin, -minY + margin);
            var result = new Bitmap(Math.Max(1, bwid), Math.Max(1, bhei), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                CharacterArt.Quality(g);
                var full = new Matrix(); full.Translate(origin.X, origin.Y); full.Multiply(matrix);
                g.Transform = full;
                if ((L.Decor & (Decor.Skill | Decor.Sun | Decor.Jet)) != 0) DrawBackDecor(g, L, sprite, w, h, scale);
                if (L.Sx == 1f && L.Sy == 1f && L.Angle == 0f)
                {
                    g.ResetTransform();
                    g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                    int x = (int)Math.Round(origin.X - w / 2f), y = (int)Math.Round(origin.Y - h);
                    g.DrawImage(bitmap, new Rectangle(x, y, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                }
                else g.DrawImage(bitmap, new RectangleF(-w / 2, -h, w, h));

                // 스프라이트에 붙어 따라 움직이는 장식
                g.Transform = full;
                if (L.Flash > .01f)
                {
                    var cm = new ColorMatrix(new[] { new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, Math.Min(1f, L.Flash), 0 }, new float[] { 1f, 1f, .78f, 0, 1 } });
                    using (var attr = new ImageAttributes())
                    {
                        attr.SetColorMatrix(cm);
                        g.DrawImage(bitmap, new[] { new PointF(-w / 2, -h), new PointF(w / 2, -h), new PointF(-w / 2, 0) }, new RectangleF(0, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel, attr);
                    }
                }
                DrawDecor(g, L, sprite, w, h, scale, rotorH);
                g.ResetTransform(); g.TranslateTransform(origin.X, origin.Y);
                DrawAffinityBadge(g, L, badge, scale);
                if (L.ZPhase >= 0) DrawZ(g, L, w, h * L.Sy, scale);
                if (!bubble.IsEmpty) DrawBubble(g, L, bubble, textSize, tail, scale);
            }
            return result;
        }

        private static void DrawBubble(Graphics g, Look L, RectangleF r, SizeF textSize, float tail, float scale)
        {
            using (GraphicsPath path = RoundRect(r, 8 * scale))
            {
                var tri = new[] { new PointF(-5 * scale, r.Bottom - 1), new PointF(1 * scale, r.Bottom + tail), new PointF(6 * scale, r.Bottom - 1) };
                using (var pen = new Pen(Color.FromArgb(255, 30, 30, 30), 2 * scale) { LineJoin = LineJoin.Round })
                using (var fill = new SolidBrush(Color.White))
                {
                    g.FillPath(fill, path); g.DrawPath(pen, path);
                    g.FillPolygon(fill, tri); g.DrawLine(pen, tri[0], tri[1]); g.DrawLine(pen, tri[1], tri[2]);
                    using (var cover = new Pen(Color.White, 2.2f * scale))
                        g.DrawLine(cover, tri[0].X + 2.2f * scale, r.Bottom - .5f * scale, tri[2].X - 2.2f * scale, r.Bottom - .5f * scale);
                }
            }
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var text = new SolidBrush(Color.FromArgb(255, 40, 40, 40)))
            using (var fmt = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center })
            {
                var box = new RectangleF(r.Left + (r.Width - textSize.Width) / 2, r.Top + (r.Height - textSize.Height) / 2, textSize.Width + 2, textSize.Height + 2);
                g.DrawString(L.Bubble, bubbleFont, text, box, fmt);
            }
        }

        // 잠: Z 세 개가 시차를 두고(0 / 0.9 / 1.8초) 떠올라 흩어진다. 한 위상이면 한 덩어리로 미끄러지는 스티커처럼 보인다.
        private static void DrawZ(Graphics g, Look L, float w, float h, float s)
        {
            float dir = L.Left ? -1 : 1, baseX = dir * w * .24f, baseY = -h * .86f;
            float[] size = { 8f, 11f, 14f }; float[] phase = { 0f, .9f, 1.8f };
            for (int i = 0; i < 3; i++)
            {
                float t = L.ZTime;
                if (t < phase[i]) continue;
                float cycle = (((t - phase[i]) % 2.7f) + 2.7f) % 2.7f, k = cycle / 2.7f;
                int alpha = (int)(255 * Math.Max(0f, Math.Min(1f, Math.Min(cycle * 3f, 1 - k) * (k < .85f ? 1f : (1 - k) / .15f))));
                if (alpha <= 0) continue;
                float sz = size[i] * s, x = baseX + dir * (3 + cycle * 4.5f) * s, y = baseY - cycle * 10.5f * s;
                using (var path = new GraphicsPath())
                {
                    path.AddString(i == 0 ? "z" : "Z", ZFamily, 1, sz, new PointF(x - sz * .3f, y - sz), StringFormat.GenericTypographic);
                    using (var pen = new Pen(Color.FromArgb(alpha, 255, 255, 255), 2.4f * s) { LineJoin = LineJoin.Round })
                    using (var brush = new SolidBrush(Color.FromArgb(alpha, 70, 100, 170))) { g.DrawPath(pen, path); g.FillPath(brush, path); }
                }
            }
        }

        // 멍때리기: 생각 점 세 개가 차례로 (작은 것부터) 떠올랐다 사라진다.
        private static void DrawDots(Graphics g, float w, float h, float s, float t)
        {
            float[] radius = { 1.7f, 2.4f, 3.4f }, phase = { 0f, .75f, 1.5f };
            PointF[] at = { new PointF(w * .26f, -h * .98f), new PointF(w * .36f, -h * 1.07f), new PointF(w * .5f, -h * 1.2f) };
            for (int i = 0; i < 3; i++)
            {
                if (t < phase[i]) continue;
                float cycle = (((t - phase[i]) % 3.6f) + 3.6f) % 3.6f;
                int alpha = (int)(255 * Math.Max(0, Math.Min(1, Math.Sin(cycle / 3.6f * Math.PI))));
                if (alpha <= 0) continue;
                float r = radius[i] * s;
                using (var fill = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                using (var edge = new Pen(Color.FromArgb(alpha, 110, 104, 120), 1.3f * s)) { g.FillEllipse(fill, at[i].X - r, at[i].Y - r, r * 2, r * 2); g.DrawEllipse(edge, at[i].X - r, at[i].Y - r, r * 2, r * 2); }
            }
        }

        private static RectangleF AffinityBadgeBounds(Look L, float w, float h, float s)
        {
            if (string.IsNullOrEmpty(L.AffinityLabel)) return RectangleF.Empty;
            float bw;
            using (var img = new Bitmap(1, 1)) using (var g = Graphics.FromImage(img))
                bw = Math.Max(38 * s, g.MeasureString(L.AffinityLabel, affinityFont, PointF.Empty, StringFormat.GenericTypographic).Width + 22 * s);
            return new RectangleF(L.AffinityOnLeft ? -w * .5f - 3 * s - bw : w * .5f + 3 * s, -h * .57f - 9 * s, (float)Math.Ceiling(bw), 20 * s);
        }

        private static void DrawAffinityBadge(Graphics g, Look L, RectangleF b, float s)
        {
            if (b.IsEmpty) return;
            using (var path = RoundRect(b, 8 * s))
            using (var fill = new SolidBrush(Color.FromArgb(245, 255, 248, 246)))
            using (var border = new Pen(Color.FromArgb(225, 237, 196, 207), s)) { g.FillPath(fill, path); g.DrawPath(border, path); }
            using (var outline = new Pen(Color.FromArgb(218, 105, 139), s)) DrawHeart(g, b.Left + 8 * s, b.Top + 10 * s, 4 * s, outline);
            using (var text = new SolidBrush(Color.FromArgb(113, 73, 91)))
            using (var fmt = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(L.AffinityLabel, affinityFont, text, new RectangleF(b.Left + 16 * s, b.Top, b.Width - 17 * s, b.Height), fmt);
            }
        }

        private static void DrawHeart(Graphics g, float x, float y, float size, Pen outline)
        {
            using (var path = new GraphicsPath()) using (var fill = new SolidBrush(Color.FromArgb(235, 126, 154)))
            {
                path.AddBezier(x, y + size * .7f, x - size * 1.25f, y - size * .1f, x - size * .5f, y - size, x, y - size * .32f);
                path.AddBezier(x, y - size * .32f, x + size * .5f, y - size, x + size * 1.25f, y - size * .1f, x, y + size * .7f);
                g.FillPath(fill, path); g.DrawPath(outline, path);
            }
        }

        private static PointF[] EyesIn(Sprite sp, bool left, float w, float h)
        {
            PointF[] e = CharacterArt.EyePoints(sp, left);
            return new[] { new PointF(e[0].X - w / 2, e[0].Y - h), new PointF(e[1].X - w / 2, e[1].Y - h) };
        }

        private static void DrawDecor(Graphics g, Look L, Sprite sp, float w, float h, float s, int rotorH)
        {
            if (L.Decor == Decor.None) return;
            float t = L.DecorPhase;
            using (var ink = new Pen(Color.FromArgb(43, 36, 42), 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if ((L.Decor & Decor.Speed) != 0)
                {
                    float side = L.Left ? 1 : -1;
                    for (int k = 0; k < 4; k++)   // 바닥에서 피어오르는 먼지
                    {
                        float p = (float)((t * 3.2 + k * .25) % 1), px = side * (w * .5f + (2 + p * 14 + k * 2) * s), py = -1.5f * s - p * 5 * s;
                        using (var dust = new SolidBrush(Color.FromArgb((int)(150 * (1 - p)), 232, 224, 212)))
                            g.FillEllipse(dust, px - (1.8f + p * 2.4f) * s, py - (1.5f + p * 2f) * s, (3.6f + p * 4.8f) * s, (3f + p * 4f) * s);
                    }
                    using (var line = new Pen(Color.FromArgb(190, 120, 120, 135), 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        for (int i = 0; i < 3; i++)
                        {
                            float y = -h * (.22f + i * .26f), off = (float)((t * 70 + i * 9) % 12) * s;
                            float x0 = side * (w * .5f + 3 * s + off);
                            g.DrawLine(line, x0, y, x0 + side * (8 + i * 3) * s, y);
                        }
                }
                if ((L.Decor & Decor.Rotor) != 0) DrawRotor(g, sp, w, h, s, rotorH, t);
                DrawFrontDecor(g, L, sp, w, h, s);
                if ((L.Decor & Decor.Dots) != 0) DrawDots(g, w, h, s, t);
                if ((L.Decor & Decor.Tears) != 0 && sp.HasEyes) DrawTears(g, EyesIn(sp, L.Left, w, h), s, t, sp.EyeRad);
                if ((L.Decor & Decor.Spiral) != 0) foreach (PointF e in EyesIn(sp, L.Left, w, h)) DrawSpiral(g, e, s, ink, sp.EyeRad, t);
                if ((L.Decor & Decor.Stars) != 0)
                    using (var stars = new Pen(Color.FromArgb(222, 172, 71), 2 * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        for (int i = 0; i < 3; i++)
                        {
                            double a = t * 3 + i * Math.PI * 2 / 3;
                            float x = (float)Math.Cos(a) * w * .35f, y = -h - 6 * s + (float)Math.Sin(a) * 6 * s;
                            g.DrawLine(stars, x - 3 * s, y, x + 3 * s, y); g.DrawLine(stars, x, y - 3 * s, x, y + 3 * s);
                        }
                if ((L.Decor & Decor.Goggles) != 0) DrawGoggles(g, EyesIn(sp, L.Left, w, h), w, s, ink, sp.EyeRad);
                if ((L.Decor & Decor.Flower) != 0) DrawBouquet(g, w, h, s, ink);
                if ((L.Decor & Decor.Notes) != 0)
                    using (var note = new SolidBrush(Color.FromArgb(176, 111, 160)))
                    using (var stem = new Pen(Color.FromArgb(176, 111, 160), 2 * s))
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float x = side * (w * .47f + 8 * s), y = -h * .8f + (float)Math.Sin(t * 5 + side) * 4 * s;
                            g.FillEllipse(note, x - 3 * s, y, 6 * s, 4 * s);
                            g.DrawLine(stem, x + 2 * s, y + 2 * s, x + 2 * s, y - 10 * s); g.DrawLine(stem, x + 2 * s, y - 10 * s, x + 7 * s, y - 8 * s);
                        }
                if ((L.Decor & Decor.Cookie) != 0)
                {
                    PointF[] e = EyesIn(sp, L.Left, w, h);
                    PointF at = new PointF((e[0].X + e[1].X) / 2, (e[0].Y + e[1].Y) / 2 + .3f * h);
                    if (sp.HasMouth) { PointF m = sp.Mouth(L.Left); at = new PointF(m.X - w / 2, m.Y - h + 1.5f * s); }
                    DrawCookie(g, at, s * .62f, t);
                }
            }
        }

        /// <summary>우산처럼: 손으로 쥔 갈색 손잡이에서 회색 자루가 머리 위로 길게 올라가 그 끝에서 프로펠러가 돈다.</summary>
        private static void DrawRotor(Graphics g, Sprite sp, float w, float h, float s, int rotorH, float t)
        {
            float px = sp.HasGrip ? sp.GripP.X - w / 2 : w * .38f;       // 자루 가로 위치 = 쥔 손
            float gy = sp.HasGrip ? sp.GripP.Y - h : -h * .4f;           // 손 높이
            float hubY = -h - rotorH * .72f, radius = w * .56f;
            using (var edge = new Pen(Color.FromArgb(70, 62, 66), 1.4f * s))
            {
                // 자루: 손 아래까지 내려오는 길쭉한 막대
                using (var shaftOutline = new Pen(Color.FromArgb(70, 62, 66), 3.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (var shaft = new Pen(Color.FromArgb(158, 158, 170), 2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { g.DrawLine(shaftOutline, px, hubY, px, gy + 8 * s); g.DrawLine(shaft, px, hubY, px, gy + 8 * s); }
                // 손잡이(갈색): 쥔 손 아래로 삐져나온 부분이 우산 손잡이처럼 보인다
                if (sp.Kind != CharKind.Bulbasaur)
                    using (var handleOutline = new Pen(Color.FromArgb(70, 44, 30), 4.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    using (var handle = new Pen(Color.FromArgb(176, 112, 64), 3f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { g.DrawLine(handleOutline, px, gy - 1 * s, px, gy + 8 * s); g.DrawLine(handle, px, gy - 1 * s, px, gy + 8 * s); }
                // 위에서 본 빠른 날개: 길이가 |cos|로 깜빡이며 도는 것처럼 보인다(최소 12%).
                float spread = (float)Math.Max(.12, Math.Abs(Math.Cos(t * 38)));
                using (var disc = new SolidBrush(Color.FromArgb(40, 255, 236, 160)))
                    g.FillEllipse(disc, px - radius, hubY - radius * .16f, radius * 2, radius * .32f);
                for (int i = -1; i <= 1; i += 2)
                    using (var outline = new Pen(Color.FromArgb(150, 90, 80, 70), 5.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    using (var blade = new Pen(Color.FromArgb(255, 255, 236, 160), 3.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { g.DrawLine(outline, px, hubY, px + i * radius * spread, hubY); g.DrawLine(blade, px, hubY, px + i * radius * spread, hubY); }
                using (var hub = new SolidBrush(Color.FromArgb(165, 165, 176))) { g.FillEllipse(hub, px - 3 * s, hubY - 3 * s, 6 * s, 6 * s); g.DrawEllipse(edge, px - 3 * s, hubY - 3 * s, 6 * s, 6 * s); }
                // 자루를 움켜쥔 손: 털색 주먹이 자루 위에 얹힌다
                if (sp.Kind == CharKind.Bulbasaur) DrawVineGrip(g, sp, w, h, h / 62f, px, gy, t);   // 손이 없으니 덩굴로 자루를 감아 쥔다
                else
                {
                    var fist = new RectangleF(px - 3.6f * s, gy - 3.4f * s, 7.2f * s, 6.4f * s);
                    Color skinColor = sp.Kind == CharKind.Charmander ? Color.FromArgb(240, 124, 52) : sp.Kind == CharKind.Squirtle ? Color.FromArgb(160, 216, 208) : Color.FromArgb(242, 214, 100);
                    using (var fur = new SolidBrush(skinColor))
                    using (var line = new Pen(Color.FromArgb(150, 98, 40), 1.2f * s))
                    { g.FillEllipse(fur, fist); g.DrawEllipse(line, fist); g.DrawLine(line, px - 3 * s, gy - .2f * s, px + 3 * s, gy - .2f * s); }
                }
            }
        }

        // ───────── 번개 방출 ─────────
        internal const float SkillCharge = .75f, SkillEnd = 2.7f;

        /// <summary>번개의 세기: 모으는 동안 서서히 커지고, 방출 중엔 최대, 끝나갈 때 잦아든다.</summary>
        internal static float SkillLevel(float t)
        {
            if (t < SkillCharge) return .2f + .35f * (t / SkillCharge);
            if (t < SkillEnd - .55f) return 1f;
            return Math.Max(0f, 1f - (t - (SkillEnd - .55f)) / .55f);
        }

        /// <summary>몸 뒤에 번지는 노란 빛무리.</summary>
        private static void DrawThunderGlow(Graphics g, float w, float h, float t)
        {
            float level = SkillLevel(t), r = w * (.62f + .78f * level), cy = -h * .5f;
            float flick = t < SkillCharge ? 1f : 1f - .18f * (float)((int)(t * 22) % 2);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-r, cy - r * .95f, r * 2, r * 1.9f);
                using (var pg = new PathGradient2(path, Color.FromArgb((int)(150 * level * flick), 255, 238, 110), Color.FromArgb(0, 255, 230, 90)))
                    g.FillPath(pg.Brush, path);
            }
        }

        /// <summary>PathGradientBrush 를 using 으로 쓰기 위한 작은 포장.</summary>
        private sealed class PathGradient2 : IDisposable
        {
            public readonly PathGradientBrush Brush;
            public PathGradient2(GraphicsPath path, Color center, Color edge)
            { Brush = new PathGradientBrush(path) { CenterColor = center, SurroundColors = new[] { edge } }; }
            public void Dispose() { Brush.Dispose(); }
        }

        private static void ZigZag(Graphics g, PointF a, float angleRad, float len, float amp, int segs, Random rnd, float s, float level, bool fork)
        {
            var pts = new PointF[segs + 1];
            float dx = (float)Math.Cos(angleRad), dy = -(float)Math.Sin(angleRad), px = -dy, py = dx, sign = rnd.Next(2) == 0 ? 1 : -1;
            pts[0] = a;
            for (int i = 1; i <= segs; i++)
            {
                float d = len * i / segs, off = i == segs ? 0 : sign * amp * (.55f + (float)rnd.NextDouble() * .8f);
                pts[i] = new PointF(a.X + dx * d + px * off, Math.Min(-1 * s, a.Y + dy * d + py * off));
                sign = -sign;
            }
            using (var glow = new Pen(Color.FromArgb((int)(120 * level), 255, 226, 70), 6f * s * (.6f + .5f * level)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var mid = new Pen(Color.FromArgb((int)(235 * level), 255, 248, 150), 3f * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var core = new Pen(Color.FromArgb((int)(255 * level), 255, 255, 255), 1.3f * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            { g.DrawLines(glow, pts); g.DrawLines(mid, pts); g.DrawLines(core, pts); }
            if (fork && segs >= 4)
            {
                float side = rnd.Next(2) == 0 ? 1 : -1;
                ZigZag(g, pts[segs / 2], angleRad + side * (.55f + (float)rnd.NextDouble() * .35f), len * .38f, amp * .6f, 3, rnd, s, level, false);
            }
        }

        /// <summary>front=true: 모으는 동안 볼에서 튀는 불꽃(몸 앞) / false: 방출하는 큰 번개(몸 뒤에서 뻗어 나옴).</summary>
        private static void DrawThunder(Graphics g, Look L, Sprite sp, float w, float h, float s, float t, bool front)
        {
            float level = SkillLevel(t);
            if (level <= 0 || front != (t < SkillCharge)) return;
            var rnd = new Random((int)(t * 18) * 7919 + 13);
            if (t < SkillCharge)
            {
                // 모으는 중: 양 볼에서 작은 전기가 파직파직
                PointF[] e = EyesIn(sp, L.Left, w, h);
                PointF[] cheeks = { new PointF(e[0].X - w * .07f, e[0].Y + h * .08f), new PointF(e[1].X + w * .07f, e[1].Y + h * .08f) };
                for (int c = 0; c < 2; c++)
                    for (int k = 0; k < 2; k++)
                    {
                        float outward = c == 0 ? (float)Math.PI : 0f;
                        float ang = outward + ((float)rnd.NextDouble() - .5f) * 1.7f;
                        ZigZag(g, cheeks[c], ang, w * (.18f + .16f * level) * (.7f + (float)rnd.NextDouble() * .6f), w * .035f, 3, rnd, s, level, false);
                    }
                return;
            }
            // 방출: 몸 가운데에서 사방(위쪽 반원 + 옆)으로 번개가 뻗는다. 위쪽 것이 가장 길다.
            int n = Math.Max(3, (int)Math.Round(9 * level));
            PointF center = new PointF(0, -h * .5f);
            for (int k = 0; k < n; k++)
            {
                float deg = -14f + 208f * (n == 1 ? .5f : (float)k / (n - 1)) + ((float)rnd.NextDouble() - .5f) * 10f;
                float rad = deg * (float)Math.PI / 180f, up = (float)Math.Sin(Math.Max(0, rad));
                float len = w * (.65f + .5f * (float)rnd.NextDouble() + .55f * up) * (.55f + .45f * level);
                PointF a = new PointF(center.X + (float)Math.Cos(rad) * w * .34f, center.Y - (float)Math.Sin(rad) * h * .36f);
                ZigZag(g, a, rad, len, w * .075f, 5 + rnd.Next(2), rnd, s, level, true);
            }
        }

        private static void DrawTears(Graphics g, PointF[] eyes, float s0, float t, float rad)
        {
            float k = Math.Max(.7f, Math.Min(1.2f, rad / (3.4f * s0))), s = s0 * k;
            for (int i = 0; i < eyes.Length; i++)
            {
                PointF e = eyes[i]; float side = i == 0 ? -1 : 1;
                using (var streak = new Pen(Color.FromArgb(200, 160, 210, 255), 1.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(streak, e.X + side * .5f * s, e.Y + 2 * s, e.X + side * .8f * s, e.Y + (6 + 2 * (float)Math.Sin(t * 9 + i)) * s);
                for (int j = 0; j < 2; j++)
                {
                    float p = (t * 1.5f + j * .5f + i * .27f) % 1f;
                    float x = e.X + side * (1 + p * 1.6f) * s, y = e.Y + 4 * s + p * 14 * s, r = (1.9f - .7f * p) * s;
                    int a = (int)(235 * (1 - p * p));
                    using (var drop = new SolidBrush(Color.FromArgb(a, 120, 190, 255)))
                    using (var rim = new Pen(Color.FromArgb(a, 60, 110, 180), .8f * s0)) { g.FillEllipse(drop, x - r, y - r * 1.25f, r * 2, r * 2.5f); g.DrawEllipse(rim, x - r, y - r * 1.25f, r * 2, r * 2.5f); }
                }
            }
        }

        /// <summary>어질어질 눈: 눈을 덮는 흰 원 위에서 소용돌이가 빙글빙글 돈다.</summary>
        private static void DrawSpiral(Graphics g, PointF c, float s, Pen ink, float rad, float t)
        {
            float R = Math.Max(3.4f * s, rad * 1.7f);
            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, c.X - R, c.Y - R, R * 2, R * 2);
            using (var edge = new Pen(Color.FromArgb(150, 90, 70, 60), .8f * s)) g.DrawEllipse(edge, c.X - R, c.Y - R, R * 2, R * 2);
            var pts = new PointF[48];
            double spin = t * 9;
            for (int i = 0; i < pts.Length; i++)
            {
                double a = i * Math.PI / 8 + spin, r = R * .12 + R * .8 * i / (pts.Length - 1);
                pts[i] = new PointF(c.X + (float)(Math.Cos(a) * r), c.Y + (float)(Math.Sin(a) * r));
            }
            using (var line = new Pen(Color.FromArgb(43, 36, 42), Math.Max(1f, R * .2f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(line, pts);
        }

        private static void DrawGoggles(Graphics g, PointF[] eyes, float w, float s, Pen ink, float rad)
        {
            float lw = Math.Max(4.4f * s, rad * 2.0f), lh = lw * .8f;
            float y = (eyes[0].Y + eyes[1].Y) / 2;
            float x0 = Math.Min(eyes[0].X, eyes[1].X), x1 = Math.Max(eyes[0].X, eyes[1].X);
            using (var strap = new Pen(Color.FromArgb(43, 36, 42), 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(strap, x0 - lw - w * .12f, y, x1 + lw + w * .12f, y);
            foreach (PointF e in eyes)
            {
                var lens = new RectangleF(e.X - lw, e.Y - lh, lw * 2, lh * 2);
                using (var shape = RoundRect(lens, lw * .6f)) using (var glass = new SolidBrush(Color.FromArgb(150, 170, 218, 237)))
                { g.FillPath(glass, shape); g.DrawPath(ink, shape); }
                using (var shine = new Pen(Color.White, 1.2f * s)) g.DrawLine(shine, e.X - lw * .5f, e.Y - lh * .45f, e.X - lw * .1f, e.Y - lh * .62f);
            }
        }

        private static void DrawBouquet(Graphics g, float w, float h, float s, Pen ink)
        {
            float x = w * .38f, y = -h * .47f;
            using (var stem = new Pen(Color.FromArgb(90, 141, 97), 2.4f * s))
                for (int j = -1; j <= 1; j++) g.DrawLine(stem, x - 3 * s, y + 25 * s, x + j * 9 * s, y - (j == 0 ? 7 : 0) * s);
            using (var wrap = new SolidBrush(Color.FromArgb(255, 224, 231)))
            {
                var pts = new[] { new PointF(x - 13 * s, y + 8 * s), new PointF(x + 10 * s, y + 8 * s), new PointF(x - 2 * s, y + 28 * s) };
                g.FillPolygon(wrap, pts); g.DrawPolygon(ink, pts);
            }
            using (var paw = new SolidBrush(Color.White)) { g.FillEllipse(paw, x - 9 * s, y + 17 * s, 10 * s, 7 * s); g.DrawEllipse(ink, x - 9 * s, y + 17 * s, 10 * s, 7 * s); }
            for (int j = -1; j <= 1; j++)
            {
                float fx = x + j * 9 * s, fy = y - (j == 0 ? 9 : 0) * s;
                for (int i = 0; i < 5; i++)
                {
                    double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                    float px = fx + (float)Math.Cos(a) * 4 * s, py = fy + (float)Math.Sin(a) * 4 * s;
                    using (var petal = new SolidBrush(j == 0 ? Color.FromArgb(208, 183, 238) : Color.FromArgb(245, 164, 185)))
                    { g.FillEllipse(petal, px - 3.5f * s, py - 3.5f * s, 7 * s, 7 * s); g.DrawEllipse(ink, px - 3.5f * s, py - 3.5f * s, 7 * s, 7 * s); }
                }
                using (var center = new SolidBrush(Color.FromArgb(255, 218, 117))) { g.FillEllipse(center, fx - 3 * s, fy - 3 * s, 6 * s, 6 * s); g.DrawEllipse(ink, fx - 3 * s, fy - 3 * s, 6 * s, 6 * s); }
            }
        }

        private static void DrawCookie(Graphics g, PointF at, float s, float t)
        {
            float x = at.X, y = at.Y - ((int)(t / .21) % 2) * 1.5f * s;
            using (var cookie = new SolidBrush(Color.FromArgb(244, 195, 134)))
            using (var edge = new Pen(Color.FromArgb(49, 35, 31), 1.25f * s))
            { g.FillEllipse(cookie, x - 5 * s, y - 4 * s, 10 * s, 8 * s); g.DrawEllipse(edge, x - 5 * s, y - 4 * s, 10 * s, 8 * s); }
            using (var chip = new SolidBrush(Color.FromArgb(83, 51, 39)))
            { g.FillEllipse(chip, x - 2 * s, y - s, 1.8f * s, 1.8f * s); g.FillEllipse(chip, x + 2 * s, y + s, 1.5f * s, 1.5f * s); }
        }
    }
}
