using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Pass 2: keep the pixels of one character, redraw what was hidden or cut off, save ref\<name>.png.
// Anchors for the program are given in "original picture coordinates + Pad".
public static class Reconstruct
{
    const int Pad = 60;
    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
    static PointF[] P(params float[] v) { PointF[] a = new PointF[v.Length / 2]; for (int i = 0; i < a.Length; i++) a[i] = new PointF(v[i * 2], v[i * 2 + 1]); return a; }

    public static void Run(byte[] px, int st, int W, int H, int[] lab, bool[] bg, string outDir)
    {
        string[] names = { "", "bulbasaur", "", "charmander", "squirtle" };
        foreach (int c in new[] { 1, 3, 4 })
        {
            bool[] keep = new bool[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (lab[i] != c) continue; int p = y * st + x * 4; int r = px[p + 2], g = px[p + 1], b = px[p];
                if (c == 3 && x >= 598 && y < 276 && (x >= 640 || y < 268)) continue;   // tail flame: redrawn by the program
                if (c == 1 && x >= 183 && y < 140 && g > r && b < r - 5) continue;      // bulb fragments: redrawn
                bool white = r > 236 && g > 236 && b > 236, dark = (r * 30 + g * 59 + b * 11) / 100 < 100;
                if (c == 1 && dark && x > 185 && Near(lab, W, H, x, y, 2, 3)) continue;                 // other friend's outline on the cut edge
                if (c == 3 && white && (y >= 205 || x > 500 || (x < 404 && y > 142) || (x < 422 && y > 192))) continue;
                if (c == 3 && dark && x > 500 && y < 190) continue;                                      // neighbour's outline                                  // gaps between friends
                if (c == 3 && g > r + 10 && x > 502) continue;                                           // blue arm pixels of the neighbour
                if (c == 4 && white && y > 135) continue;
                if (c == 4 && r > 190 && b < 105) continue;                                              // flame edge pixels
                if (c == 4 && y >= 205 && x < 665 && Near(lab, W, H, x, y, 3, 6)) continue;              // belly plate edge next to Charmander: redrawn
                keep[i] = true;
            }
            KeepBig(keep, W, H);
            Bitmap ext = new Bitmap(W + Pad * 2, H + Pad * 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (!keep[i]) continue; int p = y * st + x * 4;
                bool edge = (x > 0 && bg[i - 1]) || (x < W - 1 && bg[i + 1]) || (y > 0 && bg[i - W]) || (y < H - 1 && bg[i + W]);
                ext.SetPixel(x + Pad, y + Pad, Color.FromArgb(edge ? 220 : 255, px[p + 2], px[p + 1], px[p]));
            }
            Bitmap canvas = new Bitmap(ext.Width, ext.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(Pad, Pad); Back(c, g);
                g.ResetTransform(); g.DrawImage(ext, 0, 0);
                g.TranslateTransform(Pad, Pad); Front(c, g);
            }
            canvas.Save(System.IO.Path.Combine(outDir, names[c] + ".png"), ImageFormat.Png);
            Console.WriteLine("saved " + names[c] + ".png");
        }
        SaveShell(System.IO.Path.Combine(outDir, "squirtle_shell.png"));
    }

    static bool Near(int[] lab, int W, int H, int x, int y, int who, int rad)
    {
        for (int dy = -rad; dy <= rad; dy++) for (int dx = -rad; dx <= rad; dx++)
        { int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue; if (lab[ny * W + nx] == who) return true; }
        return false;
    }

    /// <summary>Squirtle pulled into its shell: plastron seen from the front, limb holes with blue stubs, head hole with two peeking eyes.</summary>
    static void SaveShell(string file)
    {
        Color brown = C(150, 92, 46), cream = C(246, 232, 125), black = C(22, 18, 18), blue = C(160, 216, 208), hole = C(52, 36, 34);
        using (Bitmap bmp = new Bitmap(230, 200, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float[] rimPts = { 115, 8, 190, 36, 214, 104, 192, 172, 115, 194, 38, 172, 16, 104, 40, 36 };
            Curve(g, brown, .32f, rimPts);
            using (var path = new GraphicsPath()) { path.AddClosedCurve(P(rimPts), .32f); using (var pen = new Pen(black, 4.5f)) g.DrawPath(pen, path); }
            Line(g, C(110, 66, 30), 3f, 115, 14, 115, 30); Line(g, C(110, 66, 30), 3f, 30, 60, 44, 70); Line(g, C(110, 66, 30), 3f, 200, 60, 186, 70);
            float[] inPts = { 115, 26, 176, 48, 196, 104, 178, 160, 115, 178, 52, 160, 34, 104, 54, 48 };
            Curve(g, cream, .32f, inPts);
            using (var path = new GraphicsPath()) { path.AddClosedCurve(P(inPts), .32f); using (var pen = new Pen(black, 3.6f)) g.DrawPath(pen, path); }
            Poly(g, cream, 82, 104, 98, 78, 132, 78, 148, 104, 132, 130, 98, 130);
            Line(g, black, 3.2f, 82, 104, 98, 78, 132, 78, 148, 104, 132, 130, 98, 130, 82, 104);
            Line(g, black, 3.2f, 98, 78, 80, 50); Line(g, black, 3.2f, 132, 78, 152, 50); Line(g, black, 3.2f, 148, 104, 190, 104);
            Line(g, black, 3.2f, 132, 130, 152, 160); Line(g, black, 3.2f, 98, 130, 78, 160); Line(g, black, 3.2f, 82, 104, 40, 104);
            // limb holes with blue stubs
            foreach (float[] h in new float[][] { new float[] { 52, 62, -6, -4 }, new float[] { 178, 62, 6, -4 }, new float[] { 52, 148, -6, 4 }, new float[] { 178, 148, 6, 4 } })
            {
                Oval(g, hole, h[0] - 20, h[1] - 14, 40, 28); OvalLine(g, black, 3f, h[0] - 20, h[1] - 14, 40, 28);
                Oval(g, blue, h[0] - 12 + h[2], h[1] - 7 + h[3], 24, 16); OvalLine(g, black, 2.4f, h[0] - 12 + h[2], h[1] - 7 + h[3], 24, 16);
            }
            // head hole with peeking eyes
            Oval(g, hole, 84, 12, 62, 42); OvalLine(g, black, 3.4f, 84, 12, 62, 42);
            Oval(g, blue, 92, 22, 46, 28); OvalLine(g, black, 2.6f, 92, 22, 46, 28);
            foreach (float ex in new float[] { 104, 126 }) { Oval(g, Color.White, ex - 6, 26, 12, 15); Oval(g, C(170, 30, 30), ex - 3.5f, 30, 8, 10); Oval(g, black, ex - 2, 33, 4, 5); }
            bmp.Save(file, ImageFormat.Png);
        }
    }

    static void KeepBig(bool[] m, int W, int H)
    {
        int[] id = new int[W * H]; List<int> sizes = new List<int>(); sizes.Add(0);
        for (int s = 0; s < m.Length; s++)
        {
            if (!m[s] || id[s] != 0) continue;
            int n = sizes.Count; sizes.Add(0); Stack<int> st = new Stack<int>(); st.Push(s); id[s] = n;
            while (st.Count > 0)
            {
                int j = st.Pop(); sizes[n]++; int jx = j % W, jy = j / W;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                { int nx = jx + dx, ny = jy + dy; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue; int k = ny * W + nx; if (m[k] && id[k] == 0) { id[k] = n; st.Push(k); } }
            }
        }
        int big = 0; for (int k = 1; k < sizes.Count; k++) if (sizes[k] > sizes[big]) big = k;
        for (int i = 0; i < m.Length; i++) if (m[i] && id[i] != big && sizes[id[i]] < 400) m[i] = false;
    }

    static void Poly(Graphics g, Color c, params float[] v) { using (var b = new SolidBrush(c)) g.FillPolygon(b, P(v)); }
    static void Curve(Graphics g, Color c, float tension, params float[] v) { using (var b = new SolidBrush(c)) g.FillClosedCurve(b, P(v), FillMode.Winding, tension); }
    static void Line(Graphics g, Color c, float w, params float[] v) { using (var p = new Pen(c, w) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(p, P(v)); }
    static void Oval(Graphics g, Color c, float x, float y, float w, float h) { using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, w, h); }
    static void OvalLine(Graphics g, Color c, float pw, float x, float y, float w, float h) { using (var p = new Pen(c, pw)) g.DrawEllipse(p, x, y, w, h); }

    static void Back(int c, Graphics g)
    {
        if (c == 1)
        {
            Color teal = C(128, 200, 168), shade = C(100, 150, 140), line = C(70, 120, 110), spot = C(30, 115, 115);
            Oval(g, shade, 296, 292, 52, 60); OvalLine(g, line, 2f, 296, 292, 52, 60);
            float[] body = { 196, 130, 250, 128, 310, 132, 346, 150, 359, 190, 357, 240, 347, 290, 330, 326, 300, 346, 250, 355, 212, 352, 190, 300, 205, 240, 210, 170 };
            Curve(g, teal, .4f, body);
            using (var clip = new GraphicsPath())
            {
                clip.AddClosedCurve(P(body), .4f); var old = g.Clip; g.SetClip(clip);
                Poly(g, shade, 180, 284, 380, 276, 380, 380, 180, 380); g.Clip = old;
            }
            using (var path = new GraphicsPath()) { path.AddClosedCurve(P(body), .4f); using (var pen = new Pen(line, 2.2f)) g.DrawPath(pen, path); }
            Oval(g, spot, 250, 160, 62, 27); Oval(g, spot, 303, 205, 42, 23); Oval(g, spot, 284, 252, 30, 17);
            Oval(g, teal, 240, 300, 52, 62); OvalLine(g, line, 2f, 240, 300, 52, 62); Oval(g, spot, 250, 312, 22, 13);
            foreach (float cx in new float[] { 248, 264, 280 }) Poly(g, C(215, 235, 230), cx - 5, 358, cx + 5, 358, cx, 368);
            // bulb: a plump two-tone seed with a sprout tip
            float[] bulb = { 206, 120, 222, 90, 254, 70, 292, 64, 328, 76, 352, 100, 358, 128, 340, 152, 300, 162, 252, 160, 216, 144 };
            Poly(g, C(70, 125, 60), 278, 72, 288, 36, 300, 72);
            Curve(g, C(150, 204, 108), .45f, bulb);
            using (var clip = new GraphicsPath())
            {
                clip.AddClosedCurve(P(bulb), .45f); var old = g.Clip; g.SetClip(clip);
                Poly(g, C(104, 160, 80), 292, 50, 380, 50, 380, 180, 292, 180);
                Poly(g, C(190, 232, 150), 200, 60, 238, 70, 232, 170, 200, 170);
                g.Clip = old;
            }
            using (var path = new GraphicsPath()) { path.AddClosedCurve(P(bulb), .45f); using (var pen = new Pen(C(58, 98, 52), 2.6f)) g.DrawPath(pen, path); }
            using (var pen = new Pen(C(66, 110, 58), 2.2f)) { g.DrawCurve(pen, P(236, 158, 244, 112, 262, 78)); g.DrawCurve(pen, P(290, 162, 292, 112, 292, 68)); g.DrawCurve(pen, P(336, 150, 328, 112, 316, 76)); }
            Line(g, C(58, 98, 52), 2.2f, 278, 72, 288, 36, 300, 72);
        }
        else if (c == 4)
        {
            Color blue = C(160, 216, 208), cream = C(246, 232, 125), rim = C(212, 198, 95), black = C(18, 18, 18);
            Poly(g, blue, 548, 300, 585, 296, 596, 330, 598, 372, 560, 378, 546, 340);
            Line(g, black, 3.2f, 585, 298, 596, 330, 598, 372);
            Oval(g, blue, 540, 362, 62, 22); OvalLine(g, black, 3f, 540, 362, 62, 22);
            foreach (float cx in new float[] { 548, 561, 574 }) { Oval(g, C(205, 232, 230), cx - 4.5f, 376, 9, 8); OvalLine(g, black, 2f, cx - 4.5f, 376, 9, 8); }
            Poly(g, cream, 522, 205, 556, 199, 600, 200, 650, 200, 690, 203, 694, 225, 693, 262, 690, 285, 662, 300, 645, 330, 625, 340, 580, 342, 552, 334, 536, 306, 524, 262, 520, 225);
            Poly(g, rim, 552, 330, 580, 341, 625, 339, 648, 330, 642, 320, 600, 326, 560, 320);
            Line(g, black, 3.4f, 524, 205, 526, 260, 540, 306, 560, 333); Line(g, black, 3.4f, 560, 333, 585, 341, 625, 340, 650, 330);
            Line(g, black, 3.1f, 556, 202, 618, 202, 662, 207, 674, 212);
            Line(g, black, 3.1f, 583, 253, 664, 254); Line(g, black, 3.1f, 664, 254, 673, 277);
            Poly(g, cream, 598, 184, 672, 184, 672, 208, 598, 208);
            Poly(g, blue, 620, 174, 660, 174, 662, 192, 618, 192);
            Poly(g, C(224, 130, 132), 622, 146, 658, 146, 658, 173, 622, 173);
            Line(g, black, 3.2f, 618, 174, 638, 177, 658, 173);
            Line(g, black, 3.4f, 614, 190, 634, 193, 656, 189);
        }
    }

    static void Front(int c, Graphics g)
    {
        if (c == 3)
        {
            Color orange = C(240, 122, 50), edge = C(140, 72, 40);
            Oval(g, C(176, 98, 64), 400, 368, 60, 20); Oval(g, orange, 396, 366, 56, 19); OvalLine(g, edge, 1.6f, 396, 366, 56, 19);
            Oval(g, C(176, 98, 64), 518, 368, 60, 20); Oval(g, orange, 516, 366, 58, 19); OvalLine(g, edge, 1.6f, 516, 366, 58, 19);
            foreach (float cx in new float[] { 404, 418, 432, 528, 543, 558 }) { Oval(g, C(250, 240, 215), cx - 4, 380, 8, 8); OvalLine(g, edge, 1.2f, cx - 4, 380, 8, 8); }
            Oval(g, orange, 626, 261, 14, 14);
        }
    }
}
