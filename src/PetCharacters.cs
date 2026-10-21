using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Cachu
{
    /// <summary>
    /// 캐릭터마다 다른 몸짓과 장식 (원작 설정을 따른다):
    ///  · 카츄 — 볼에 전기를 모아 번개를 방출
    ///  · 파이리 — 꼬리 끝 불꽃은 건강·기분의 표시(평소엔 일렁, 신나면 활활, 화나면 확 타오르고, 배고프고 지치면 작아져 김이 남). 불꽃 뿜기
    ///  · 꼬부기 — 물대포, 등딱지에 숨기(Withdraw: 낮잠·슬라이딩·콕 눌림), 꼬부기단의 선글라스
    ///  · 이상해씨 — 등의 꽃봉오리가 햇빛을 모음(낮잠 땐 광합성), 덩굴채찍·잎날가르기(+솔라빔처럼 빛을 모으는 준비)
    /// </summary>
    internal sealed partial class Pet
    {
        private bool FacingLeft(Look L) { return L.Left; }

        // ───────────────────────── 상태별 몸짓 ─────────────────────────

        /// <summary>특기 발동 중의 몸짓: 모으기(0.75초) → 방출 → 잦아듦.</summary>
        private void SkillLook(ref Look L)
        {
            double t = StateT; L.Decor |= Decor.Skill; L.DecorPhase = (float)t;
            float face = L.Left ? -1 : 1;
            double level = SkillLevel((float)t);
            switch (Art.Kind)
            {
                case CharKind.Pikachu:
                {
                    // 볼에 전기를 모으며 파르르 떨다가(웅크림) 번쩍! 하고 방출, 잠시 번쩍인 뒤 잦아든다.
                    L.Sprite = Art.Angry; L.Left = false;
                    if (t < SkillCharge)
                    {
                        double k = t / SkillCharge;
                        Uni(ref L, 1 - .07 * k); L.Angle = (float)(Math.Sin(t * 47) * 1.6 * k);
                        lookJitterX = Math.Sin(t * 71) * (.3 + 1.2 * k) * S;
                    }
                    else
                    {
                        double u = t - SkillCharge, pulse = Math.Exp(-u * 4.5);
                        Uni(ref L, 1 + .07 * pulse + .012 * Math.Sin(t * 53)); L.Angle = (float)(Math.Sin(t * 61) * 1.8 * level);
                        lookJitterX = Math.Sin(t * 83) * 1.4 * level * S; L.HopY = (float)(-pulse * 3 * S);
                        L.Flash = (float)Math.Min(1, pulse * .85 + (level >= 1 && (int)(t * 22) % 2 == 0 ? .5 : 0));
                    }
                    break;
                }
                case CharKind.Charmander:
                {
                    // 숨을 크게 들이마시며 몸을 뒤로 젖혔다가, 앞으로 숙이며 불꽃을 뿜는다.
                    L.Sprite = Art.Stand;
                    if (t < SkillCharge)
                    {
                        double k = t / SkillCharge;
                        Uni(ref L, 1 + .05 * k); L.Angle = (float)(-face * 5 * k); lookJitterX = Math.Sin(t * 60) * .5 * k * S;
                    }
                    else
                    {
                        double u = t - SkillCharge, kick = Math.Exp(-u * 5);
                        L.Angle = (float)(face * (4 + 2 * Math.Sin(t * 37)) * level - face * 3 * kick); Uni(ref L, 1 - .03 * kick);
                        lookJitterX = Math.Sin(t * 71) * .7 * level * S;
                    }
                    break;
                }
                case CharKind.Squirtle:
                {
                    // 볼을 빵빵하게 부풀렸다가 물대포 발사! 반동으로 몸이 뒤로 밀린다.
                    L.Sprite = Art.Stand;
                    if (t < SkillCharge)
                    {
                        double k = t / SkillCharge;
                        Uni(ref L, 1 + .07 * Math.Sin(k * Math.PI * .5)); lookJitterX = Math.Sin(t * 55) * .45 * k * S;
                    }
                    else
                    {
                        double u = t - SkillCharge, kick = Math.Exp(-u * 6);
                        L.Angle = (float)(-face * (3.5 * level + 4 * kick)); lookJitterX = Math.Sin(t * 67) * .5 * level * S;
                        Uni(ref L, 1 + .03 * kick);
                    }
                    break;
                }
                default:
                {
                    // 이상해씨: 몸을 낮추고 꽃봉오리에 빛을 모은 다음, 덩굴채찍으로 철썩!
                    L.Sprite = Art.Stand;
                    if (t < SkillCharge) { double k = t / SkillCharge; Uni(ref L, 1 - .05 * k); lookJitterX = Math.Sin(t * 50) * .35 * k * S; }
                    else
                    {
                        double u = t - SkillCharge;
                        double lash = Math.Abs(Math.Sin(u * Math.PI / .72));
                        Uni(ref L, 1 - .02 + .02 * lash); L.Angle = (float)(Math.Sin(u * Math.PI / .72) * 1.6 * level);
                    }
                    break;
                }
            }
        }

        /// <summary>캐릭터 고유 장식(꼬리 불꽃·선글라스·광합성)을 모든 상태에 덧붙인다.</summary>
        private void CharacterLook(ref Look L, double now)
        {
            L.T = (float)animT;
            switch (Art.Kind)
            {
                case CharKind.Charmander:
                    L.Decor |= Decor.Flame; L.Flame = (float)FlameLevel();
                    break;
                case CharKind.Squirtle:
                    bool cool = State == PetState.Cheer || State == PetState.SecretDance || State == PetState.Hop || (State == PetState.Skill && StateT > SkillEnd - 1.0);
                    if (cool && (L.Decor & (Decor.Tears | Decor.Sleepy | Decor.Spiral | Decor.Blink)) == 0 && L.Sprite != Art.Shell) L.Decor |= Decor.Shades;
                    break;
                case CharKind.Bulbasaur:
                    if (State == PetState.Sleep || (State == PetState.Resting && restPose == RestPose.Sleep)) L.Decor |= Decor.Sun;
                    break;
            }
        }

        /// <summary>파이리 꼬리 불꽃의 세기: 1 = 평소. 기분과 건강을 그대로 보여 준다.</summary>
        private double FlameLevel()
        {
            double lv = 1;
            switch (State)
            {
                case PetState.Sleep: lv = .5; break;
                case PetState.Resting: lv = restPose == RestPose.Sleep ? .5 : 1; break;
                case PetState.Squish: lv = squishAmp >= .2 ? 1.85 : 1.1; break;
                case PetState.Hop: case PetState.Cheer: case PetState.SecretDance: lv = 1.3; break;
                case PetState.Sliding: lv = 1.25; break;
                case PetState.Eating: lv = 1.15; break;
                case PetState.TouchCry: case PetState.IdleCry: lv = .45; break;
                case PetState.Dizzy: lv = .9; break;
                case PetState.Skill:
                {
                    float t = (float)StateT, k = SkillLevel(t);
                    lv = t < SkillCharge ? 1 + .9 * (t / SkillCharge) : (k >= 1 ? 2.0 : 1 + k);
                    break;
                }
            }
            if (FriendData.Current.Hungry && IsCompanionFloorPose && State != PetState.Squish) lv = Math.Min(lv, .42);
            return lv;
        }

        // ───────────────────────── 그리기 보조 ─────────────────────────

        /// <summary>스프라이트 위치(왼쪽 위 원점) → 장식 좌표(바닥 가운데 원점).</summary>
        private static PointF Anc(Sprite sp, PointF p, bool left, float w, float h)
        { PointF q = sp.At(p, left); return new PointF(q.X - w / 2f, q.Y - h); }

        private static float Clamp01(double v) { return (float)Math.Max(0, Math.Min(1, v)); }

        private static void Glow(Graphics g, PointF c, float rx, float ry, Color center)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(c.X - rx, c.Y - ry, rx * 2, ry * 2);
                using (var pg = new PathGradientBrush(path) { CenterColor = center, SurroundColors = new[] { Color.FromArgb(0, center) } })
                    g.FillPath(pg, path);
            }
        }

        // ───────────────────────── 파이리: 꼬리 불꽃 ─────────────────────────

        private static GraphicsPath FlamePath(float wd, float hv, float sway)
        {
            var p = new GraphicsPath();
            p.AddBezier(-wd, 0, -wd * 1.25f, -hv * .45f, -wd * .25f, -hv * .62f, sway, -hv);
            p.AddBezier(sway, -hv, wd * .35f, -hv * .55f, wd * 1.3f, -hv * .42f, wd, 0);
            p.AddArc(-wd, -wd * .55f, wd * 2, wd * 1.1f, 0, 180);
            p.CloseFigure(); return p;
        }

        /// <summary>꼬리 끝(a)에서 타오르는 불꽃. level: .4 아주 작게(김이 남) / 1 평소(일렁) / 2 활활(불똥). 몸이 기울어도 불꽃은 위로 솟는다.</summary>
        private static void DrawFlame(Graphics g, PointF a, float level, float t, float u, float bodyAngle)
        {
            var state = g.Save();
            g.TranslateTransform(a.X, a.Y + 1.5f * u); g.RotateTransform(-bodyAngle);
            bool weak = level < .6f;
            float on = weak ? (float)(.62 + .38 * Math.Max(0, Math.Sin(t * 19) * Math.Sin(t * 7.3 + 1))) : 1f;
            float wd = 5.2f * u * (float)Math.Pow(level, .55), H = 14.5f * u * (float)Math.Pow(level, .85) * on + 2f * u;
            float sway = (float)(Math.Sin(t * 11) * 1.1 * u * (.45 + .5 * Math.Min(level, 1.6)));
            float hv = H * (1 + .12f * (float)Math.Sin(t * 17 + 1) + .07f * (float)Math.Sin(t * 29));
            Glow(g, new PointF(0, -hv * .42f), wd * 2.4f, hv * .78f, Color.FromArgb((int)(62 * Math.Min(1.3f, level) * on), 255, 190, 70));
            if (level > 1.25f)   // 양옆 곁불
                foreach (int side in new[] { -1, 1 })
                {
                    float lh = hv * (.42f + .06f * (float)Math.Sin(t * 21 + side)), lw = wd * .55f;
                    g.TranslateTransform(side * wd * 1.05f, 0);
                    using (var p = FlamePath(lw, lh, side * (float)Math.Sin(t * 13 + side) * u)) using (var b = new SolidBrush(Color.FromArgb(235, 235, 96, 46))) g.FillPath(b, p);
                    g.TranslateTransform(-side * wd * 1.05f, 0);
                }
            Color[] cols = { Color.FromArgb(240, 232, 86, 44), Color.FromArgb(245, 255, 160, 52), Color.FromArgb(250, 255, 228, 120) };
            float[] ks = { 1f, .72f, .44f };
            for (int i = 0; i < 3; i++)
                using (var p = FlamePath(wd * ks[i], hv * ks[i] * (i == 2 ? .85f : 1f), sway * ks[i])) using (var b = new SolidBrush(cols[i])) g.FillPath(b, p);
            if (level > 1.55f)   // 불똥
                for (int k = 0; k < 6; k++)
                {
                    float ph = (float)((t * 1.3 + k * .17) % 1), x = (float)Math.Sin(k * 7.1 + t * 2.3) * wd * 1.6f * (.4f + ph), y = -hv * (.5f + ph * 1.1f);
                    using (var b = new SolidBrush(Color.FromArgb((int)(220 * (1 - ph)), 255, 214, 100))) g.FillEllipse(b, x - 1f * u, y - 1f * u, 2f * u, 2f * u);
                }
            if (weak)    // 힘이 없을 땐 지직거리며 김이 오른다
                for (int k = 0; k < 3; k++)
                {
                    float ph = (float)((t * .7 + k * .33) % 1), x = (float)Math.Sin(k * 3 + t * 2) * 2.5f * u, y = -hv - ph * 18f * u, r = (2.2f + ph * 2.6f) * u;
                    using (var b = new SolidBrush(Color.FromArgb((int)(120 * (1 - ph)), 225, 228, 235))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                }
            g.Restore(state);
        }

        // ───────────────────────── 꼬부기: 선글라스(꼬부기단) ─────────────────────────

        private static void DrawShades(Graphics g, Look L, Sprite sp, float w, float h, float u)
        {
            PointF[] e = EyesIn(sp, L.Left, w, h);
            float rad = Math.Max(sp.EyeRad, 2.6f * u), lw = rad * 1.75f, lh = rad * 1.45f;
            float y = (e[0].Y + e[1].Y) / 2;
            using (var frame = new Pen(Color.FromArgb(18, 18, 24), 1.5f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(frame, e[0].X + lw * .8f, e[0].Y - lh * .15f, e[1].X - lw * .8f, e[1].Y - lh * .15f);                         // 코걸이
                g.DrawLine(frame, e[0].X - lw * .9f, e[0].Y - lh * .3f, e[0].X - lw * 1.9f, e[0].Y - lh * .9f);                          // 다리
                g.DrawLine(frame, e[1].X + lw * .9f, e[1].Y - lh * .3f, e[1].X + lw * 1.9f, e[1].Y - lh * .9f);
                foreach (PointF c in e)
                {
                    var lens = new RectangleF(c.X - lw, c.Y - lh, lw * 2, lh * 2);
                    using (var b = new SolidBrush(Color.FromArgb(246, 22, 24, 32))) g.FillEllipse(b, lens);
                    g.DrawEllipse(frame, lens);
                    using (var shine = new Pen(Color.FromArgb(190, 255, 255, 255), 1.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(shine, c.X - lw * .55f, c.Y - lh * .35f, c.X - lw * .1f, c.Y - lh * .62f);
                }
            }
        }

        // ───────────────────────── 이상해씨: 광합성 낮잠 ─────────────────────────

        private static void DrawSun(Graphics g, Look L, Sprite sp, float w, float h, float u, float t)
        {
            if (!sp.HasBulb) return;
            PointF b = Anc(sp, sp.BulbP, L.Left, w, h);
            // 햇살: 위쪽에서 비스듬히 내려와 꽃봉오리로 모인다
            for (int i = 0; i < 3; i++)
            {
                float k = (float)(.5 + .5 * Math.Sin(t * 1.3 + i * 1.7)), x0 = b.X - 30 * u + i * 26 * u;
                using (var br = new SolidBrush(Color.FromArgb((int)(34 + 32 * k), 255, 244, 150)))
                    g.FillPolygon(br, new[] { new PointF(x0 - 6 * u, b.Y - 70 * u), new PointF(x0 + 10 * u, b.Y - 70 * u), new PointF(b.X + 12 * u, b.Y), new PointF(b.X - 10 * u, b.Y) });
            }
            Glow(g, b, 26 * u, 22 * u, Color.FromArgb((int)(60 + 40 * Math.Sin(t * 2.1)), 255, 250, 170));
            for (int k = 0; k < 4; k++)   // 반짝임
            {
                float ph = (float)((t * .55 + k * .27) % 1), x = b.X + (float)Math.Sin(k * 2.7) * 24 * u, y = b.Y - 8 * u - ph * 26 * u, r = 2.6f * u * (1 - ph);
                using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - ph)), 255, 250, 190), 1.1f * u))
                { g.DrawLine(pen, x - r, y, x + r, y); g.DrawLine(pen, x, y - r, x, y + r); }
            }
        }

        // ───────────────────────── 꼬부기: 등딱지 슬라이딩의 물줄기 ─────────────────────────

        private static void DrawJet(Graphics g, Look L, float w, float h, float u, float t)
        {
            float face = L.Left ? -1 : 1, back = -face;
            for (int i = 0; i < 4; i++)
            {
                float ph = (float)((t * 3.3 + i * .25) % 1), len = (12 + ph * 20) * u, x = back * (w * .34f + ph * 14 * u), y = -h * (.18f + i * .1f) + (float)Math.Sin(t * 30 + i) * u;
                using (var pen = new Pen(Color.FromArgb((int)(200 * (1 - ph)), 150, 210, 255), (3.2f - ph * 1.6f) * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, x, y, x + back * len, y);
                using (var b = new SolidBrush(Color.FromArgb((int)(220 * (1 - ph)), 235, 247, 255))) g.FillEllipse(b, x + back * len - 1.5f * u, y - 1.5f * u - ph * 4 * u, 3 * u, 3 * u);
            }
        }

        // ───────────────────────── 특기 그리기 (몸 뒤 / 몸 앞) ─────────────────────────

        /// <summary>몸 뒤에 깔리는 효과: 빛무리·번개·덩굴·잎날·햇살.</summary>
        private static void DrawBackDecor(Graphics g, Look L, Sprite sp, float w, float h, float s)
        {
            float u = h / 62f, t = L.DecorPhase;
            if ((L.Decor & Decor.Sun) != 0) DrawSun(g, L, sp, w, h, u, L.T);
            if ((L.Decor & Decor.Jet) != 0) DrawJet(g, L, w, h, u, L.T);
            if ((L.Decor & Decor.Skill) == 0) return;
            switch (sp.Kind)
            {
                case CharKind.Pikachu: DrawThunderGlow(g, w, h, t); DrawThunder(g, L, sp, w, h, s, t, false); break;
                case CharKind.Charmander:
                    if (t >= SkillCharge) Glow(g, new PointF(0, -h * .5f), w * (.7f + .5f * SkillLevel(t)), h * .62f, Color.FromArgb((int)(95 * SkillLevel(t)), 255, 150, 50));
                    break;
                case CharKind.Bulbasaur: DrawBulbaBack(g, L, sp, w, h, u, t); break;
            }
        }

        /// <summary>몸 앞에 그려지는 효과: 불꽃 뿜기·물대포·모으는 동안의 불꽃/물방울.</summary>
        private static void DrawFrontDecor(Graphics g, Look L, Sprite sp, float w, float h, float s)
        {
            float u = h / 62f, t = L.DecorPhase;
            if ((L.Decor & Decor.Flame) != 0 && sp.HasTail) DrawFlame(g, Anc(sp, sp.TailP, L.Left, w, h), L.Flame, L.T, u, L.Angle);
            if ((L.Decor & Decor.Shades) != 0) DrawShades(g, L, sp, w, h, u);
            if ((L.Decor & Decor.Skill) == 0) return;
            switch (sp.Kind)
            {
                case CharKind.Pikachu: DrawThunder(g, L, sp, w, h, s, t, true); break;
                case CharKind.Charmander: DrawFireBreath(g, L, sp, w, h, u, t); break;
                case CharKind.Squirtle: DrawWaterGun(g, L, sp, w, h, u, t); break;
                case CharKind.Bulbasaur: DrawBulbaFront(g, L, sp, w, h, u, t); break;
            }
        }

        // ── 파이리: 불꽃 뿜기 ──
        private static void DrawFireBreath(Graphics g, Look L, Sprite sp, float w, float h, float u, float t)
        {
            if (!sp.HasMouth) return;
            PointF m = Anc(sp, sp.MouthP, L.Left, w, h);
            float face = L.Left ? -1 : 1;
            if (t < SkillCharge)
            {
                // 모으는 동안: 입 앞에서 작은 불씨가 깜빡
                float k = t / SkillCharge;
                for (int i = 0; i < 4; i++)
                {
                    float ph = (float)((t * 3 + i * .25) % 1), x = m.X + face * (3 + ph * 7) * u, y = m.Y - ph * 6 * u + (float)Math.Sin(t * 30 + i) * u, r = (1.2f + 2.2f * k) * u * (1 - ph * .6f);
                    using (var b = new SolidBrush(Color.FromArgb((int)(230 * (1 - ph)), 255, (int)(190 - 60 * ph), 70))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                }
                return;
            }
            float level = SkillLevel(t), grow = Clamp01((t - SkillCharge) / .22f) * (t < SkillEnd - .5f ? 1 : level);
            float len = w * 1.55f * grow;
            int n = 20;
            for (int i = n - 1; i >= 0; i--)
            {
                float s = (float)i / (n - 1), x = m.X + face * (s * len + 2 * u), y = m.Y - s * s * h * .07f + (float)Math.Sin(t * 28 + i * 1.3) * u * (1 + 2 * s);
                float r = u * (3.2f + 12.5f * s) * (.88f + .12f * (float)Math.Sin(t * 37 + i)) * Math.Max(.35f, level);
                int a = (int)(235 * (1 - s * .72f));
                Color c = s < .25f ? Color.FromArgb(a, 255, 246, 190) : s < .6f ? Color.FromArgb(a, 255, 178, 62) : Color.FromArgb(a, 232, 84, 42);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r * .9f, r * 2, r * 1.8f);
            }
            for (int k = 0; k < 7; k++)   // 불똥
            {
                float ph = (float)((t * 1.7 + k * .143) % 1), x = m.X + face * (len * (.35f + ph * .9f)), y = m.Y - (float)Math.Sin(k * 5.3) * 12 * u * ph - ph * 10 * u;
                using (var b = new SolidBrush(Color.FromArgb((int)(230 * (1 - ph)) * (grow > .2f ? 1 : 0), 255, 214, 100))) g.FillEllipse(b, x - 1.1f * u, y - 1.1f * u, 2.2f * u, 2.2f * u);
            }
        }

        // ── 꼬부기: 물대포 ──
        private static PointF WaterAt(PointF m, float face, float D, float peak, float h, float tau)
        { return new PointF(m.X + face * D * tau, m.Y * (1 - tau * tau) - peak * 4 * tau * (1 - tau)); }

        private static void DrawWaterGun(Graphics g, Look L, Sprite sp, float w, float h, float u, float t)
        {
            if (!sp.HasMouth) return;
            PointF m = Anc(sp, sp.MouthP, L.Left, w, h);
            float face = L.Left ? -1 : 1;
            if (t < SkillCharge)
            {
                float k = t / SkillCharge;     // 모으는 동안: 물방울이 입가에 맺힌다
                for (int i = 0; i < 5; i++)
                {
                    float ph = (float)((t * 2.2 + i * .2) % 1), x = m.X + face * (2 + i * 2.2f) * u, y = m.Y - ph * 9 * u, r = (1.2f + 1.8f * k) * u * (1 - ph * .5f);
                    using (var b = new SolidBrush(Color.FromArgb((int)(210 * (1 - ph)), 205, 235, 255))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
                    using (var pen = new Pen(Color.FromArgb((int)(210 * (1 - ph)), 90, 150, 220), .8f * u)) g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                }
                return;
            }
            float D = w * 1.5f, peak = h * .11f;
            float head = Clamp01((t - SkillCharge) / .3f), tail = Clamp01((t - (SkillEnd - .75f)) / .5f);
            if (tail >= head) return;
            int n = 30; var pts = new PointF[n];
            for (int i = 0; i < n; i++) pts[i] = WaterAt(m, face, D, peak, h, tail + (head - tail) * i / (n - 1));
            using (var outer = new Pen(Color.FromArgb(225, 66, 140, 232), 5.6f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var mid = new Pen(Color.FromArgb(235, 130, 200, 255), 3.7f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var hi = new Pen(Color.FromArgb(235, 238, 248, 255), 1.4f * u) { DashStyle = DashStyle.Custom, DashPattern = new[] { 3f, 2.2f }, DashOffset = -t * 14 })
            { g.DrawLines(outer, pts); g.DrawLines(mid, pts); g.DrawLines(hi, pts); }
            PointF tip = pts[n - 1];
            for (int k = 0; k < 6; k++)   // 흩날리는 물방울
            {
                float ph = (float)((t * 2.4 + k * .17) % 1), x = tip.X + face * ph * 7 * u + (float)Math.Sin(k * 4.1) * 3 * u, y = tip.Y - 3 * u + (float)Math.Cos(k * 2.3) * 3 * u + ph * ph * 9 * u, r = (1.4f - .6f * ph) * u;
                using (var b = new SolidBrush(Color.FromArgb((int)(220 * (1 - ph)), 170, 220, 255))) g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
            }
            if (head >= .97f)    // 땅에 닿아 철퍼덕 튀는 물
            {
                PointF gd = WaterAt(m, face, D, peak, h, 1f); float sp2 = Math.Min(1.6f, t - SkillCharge - .3f), fade = Clamp01(1 - (t - (SkillEnd - .5f)) / .5f);
                for (int i = 0; i < 2; i++)
                {
                    float rr = (4 + (sp2 * 16 + i * 7) % 24) * u;
                    using (var pen = new Pen(Color.FromArgb((int)(170 * fade * (1 - rr / (30 * u))), 200, 232, 255), 1.4f * u)) g.DrawEllipse(pen, gd.X - rr, gd.Y - rr * .26f, rr * 2, rr * .52f);
                }
                for (int k = 0; k < 7; k++)
                {
                    float ph = (float)((t * 2.2 + k * .14) % 1), vx = (k - 3) * 4.2f * u * ph, vy = -(11 + (k % 3) * 4) * u * (float)Math.Sin(ph * Math.PI);
                    using (var b = new SolidBrush(Color.FromArgb((int)(210 * fade * (1 - ph)), 175, 224, 255))) g.FillEllipse(b, gd.X + vx - 1.3f * u, gd.Y + vy - 1.3f * u, 2.6f * u, 2.6f * u);
                }
            }
        }

        // ── 이상해씨: 모으기(빛) + 덩굴채찍 + 잎날가르기 ──
        private static void DrawBulbaBack(Graphics g, Look L, Sprite sp, float w, float h, float u, float t)
        {
            if (!sp.HasBulb) return;
            PointF b = Anc(sp, sp.BulbP, L.Left, w, h);
            float level = SkillLevel(t);
            Glow(g, b, (26 + 22 * level) * u, (22 + 18 * level) * u, Color.FromArgb((int)(120 * Math.Min(1, level) + (t < SkillCharge ? 40 * t / SkillCharge : 0)), 255, 252, 170));
            if (t < SkillCharge) return;
            PointF v = new PointF(b.X, b.Y + h * .16f);
            float u0 = t - SkillCharge, fade = Clamp01(1 - (t - (SkillEnd - .5f)) / .5f);
            // 덩굴: 위에서 옆으로 내리치는 채찍질을 양쪽이 번갈아
            for (int side = -1; side <= 1; side += 2)
            {
                float tt = u0 - (side > 0 ? 0 : .3f); if (tt < 0) continue;
                float c = (tt % .75f) / .75f;                            // 한 번 내리치는 주기
                float ext = c < .6f ? (float)Math.Sin(c / .6f * Math.PI / 2) : (float)Math.Cos((c - .6f) / .4f * Math.PI / 2);
                float ang = (float)((100 - 95 * Math.Min(1, c / .6f)) * Math.PI / 180);    // 위쪽(100°)에서 옆(5°)으로
                float len = w * 1.45f * ext * fade; if (len < 3 * u) continue;
                int n = 14; var pts = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    float s = (float)i / (n - 1), wave = (float)Math.Sin(s * 6.2 - t * 16) * .09f * len * s;
                    float ux = side * (float)Math.Cos(ang), uy = -(float)Math.Sin(ang);
                    pts[i] = new PointF(v.X + ux * len * s - uy * wave, v.Y + uy * len * s + ux * wave);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    float s = (float)i / (n - 1);
                    using (var dark = new Pen(Color.FromArgb(235, 42, 100, 56), (7.4f - 3.6f * s) * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    using (var lite = new Pen(Color.FromArgb(240, 92, 176, 96), (4.8f - 2.6f * s) * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { g.DrawLine(dark, pts[i], pts[i + 1]); g.DrawLine(lite, pts[i], pts[i + 1]); }
                }
                PointF tp = pts[n - 1];
                using (var lf = new SolidBrush(Color.FromArgb(235, 112, 190, 96))) g.FillEllipse(lf, tp.X - 4.5f * u, tp.Y - 3 * u, 9 * u, 6 * u);
                if (c > .52f && c < .72f)   // 철썩! 휘두를 때 번쩍이는 궤적
                    using (var crack = new Pen(Color.FromArgb(150, 255, 255, 240), 1.6f * u)) g.DrawLine(crack, tp.X, tp.Y, tp.X + side * 8 * u, tp.Y + 4 * u);
            }
            // 잎날가르기: 잎사귀가 빙글빙글 날아간다
            for (int k = 0; k < 10; k++)
            {
                float t0 = .85f + k * .13f, tau = (t - t0) / .62f; if (tau <= 0 || tau >= 1) continue;
                float ang = (float)((k % 2 == 0 ? 10 + (k * 17) % 60 : 170 - (k * 17) % 60) * Math.PI / 180);
                float dist = w * 1.65f * tau, x = v.X + (float)Math.Cos(ang) * dist, y = v.Y - (float)Math.Sin(ang) * dist * .8f - 6 * u * (float)Math.Sin(tau * Math.PI);
                float rot = tau * 720f + k * 40, sz = 7.4f * u, a = 235 * (1 - tau * tau) * fade;
                var st = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(rot);
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(-sz, 0, -sz * .4f, -sz * .75f, sz * .4f, -sz * .75f, sz, 0); path.AddBezier(sz, 0, sz * .4f, sz * .75f, -sz * .4f, sz * .75f, -sz, 0);
                    using (var fill = new SolidBrush(Color.FromArgb((int)a, 118, 200, 92))) g.FillPath(fill, path);
                    using (var edge = new Pen(Color.FromArgb((int)a, 44, 110, 52), .9f * u)) { g.DrawPath(edge, path); g.DrawLine(edge, -sz, 0, sz, 0); }
                }
                g.Restore(st);
            }
        }

        private static void DrawBulbaFront(Graphics g, Look L, Sprite sp, float w, float h, float u, float t)
        {
            if (!sp.HasBulb || t >= SkillCharge) return;
            PointF b = Anc(sp, sp.BulbP, L.Left, w, h);
            float k = t / SkillCharge;
            for (int i = 0; i < 9; i++)   // 솔라빔처럼: 사방의 빛이 꽃봉오리로 빨려 든다
            {
                float ph = (float)((t * 1.7 + i / 9.0) % 1), ang = i * 0.7f + 1f, r = (62 * (1 - ph) + 6) * u;
                float x = b.X + (float)Math.Cos(ang) * r, y = b.Y + (float)Math.Sin(ang) * r * .8f, rr = (1.2f + 1.4f * k) * u * (.5f + ph);
                using (var br = new SolidBrush(Color.FromArgb((int)(235 * Math.Min(1, ph * 2)), 255, 252, 190))) g.FillEllipse(br, x - rr, y - rr, rr * 2, rr * 2);
            }
        }

        /// <summary>한 번 쓰는 이상해씨 덩굴 꾸밈: 우산 손잡이 대신 덩굴이 자루를 둘둘 감아 쥔다.</summary>
        private static void DrawVineGrip(Graphics g, Sprite sp, float w, float h, float u, float px, float gy, float t)
        {
            PointF b = new PointF(sp.HasBulb ? sp.BulbP.X - w / 2f : px, sp.HasBulb ? sp.BulbP.Y - h + h * .16f : gy);
            using (var dark = new Pen(Color.FromArgb(235, 42, 100, 56), 4.4f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var lite = new Pen(Color.FromArgb(240, 92, 176, 96), 2.6f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (int side in new[] { -1, 1 })
                {
                    float sway = (float)Math.Sin(t * 3 + side) * 1.2f * u;
                    var pts = new[] { b, new PointF(b.X + side * 9 * u, b.Y + 4 * u), new PointF(px + side * 5 * u, gy - 3 * u + sway), new PointF(px - side * 4 * u, gy + 1 * u), new PointF(px + side * 3 * u, gy + 5 * u) };
                    g.DrawCurve(dark, pts); g.DrawCurve(lite, pts);
                }
        }
    }
}
