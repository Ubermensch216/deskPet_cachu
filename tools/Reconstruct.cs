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
            Oval(g, shade, 292, 290, 56, 62); OvalLine(g, line, 2f, 292, 290, 56, 62);
            Poly(g, teal, 196, 128, 230, 134, 280, 138, 330, 140, 352, 165, 358, 210, 350, 260, 344, 305, 325, 340, 285, 354, 240, 354, 205, 352, 190, 300, 205, 240, 210, 170, 200, 140);
            Poly(g, shade, 200, 285, 250, 290, 300, 292, 346, 285, 344, 305, 325, 340, 285, 354, 240, 354, 205, 352, 190, 300);
            Line(g, line, 2.2f, 230, 134, 280, 138, 330, 140, 352, 165, 358, 210, 350, 260, 344, 305, 325, 340, 285, 354, 240, 354);
            Oval(g, spot, 246, 160, 62, 27); Oval(g, spot, 300, 205, 42, 23); Oval(g, spot, 282, 252, 30, 17);
            Oval(g, teal, 236, 304, 58, 56); OvalLine(g, line, 2f, 236, 304, 58, 56); Oval(g, spot, 246, 314, 24, 13);
            foreach (float cx in new float[] { 244, 260, 276 }) Poly(g, C(215, 235, 230), cx - 5, 355, cx + 5, 355, cx, 366);
            float[] bulb = { 196, 134, 190, 116, 204, 98, 234, 85, 274, 80, 314, 85, 344, 99, 360, 120, 364, 140, 300, 146, 230, 146 };
            Poly(g, C(70, 125, 60), 266, 86, 274, 50, 284, 86);
            Curve(g, C(155, 205, 110), .35f, bulb);
            using (var clip = new GraphicsPath())
            {
                clip.AddClosedCurve(P(bulb), .35f); var old = g.Clip; g.SetClip(clip);
                Poly(g, C(112, 165, 84), 276, 70, 380, 70, 380, 160, 276, 160); Poly(g, C(190, 230, 150), 190, 84, 240, 84, 228, 150, 190, 150); g.Clip = old;
            }
            using (var path = new GraphicsPath()) { path.AddClosedCurve(P(bulb), .35f); using (var pen = new Pen(C(60, 100, 55), 2.6f)) g.DrawPath(pen, path); }
            Line(g, C(70, 115, 60), 2.2f, 232, 144, 240, 92); Line(g, C(70, 115, 60), 2.2f, 274, 146, 275, 84); Line(g, C(70, 115, 60), 2.2f, 316, 146, 308, 92);
            Line(g, C(60, 100, 55), 2.2f, 266, 86, 274, 50, 284, 86);
        }
        else if (c == 4)
        {
            Color blue = C(160, 216, 208), cream = C(246, 232, 125), rim = C(212, 198, 95), black = C(18, 18, 18);
            Poly(g, blue, 548, 300, 585, 296, 596, 330, 598, 372, 560, 378, 546, 340);
            Line(g, black, 3.2f, 585, 298, 596, 330, 598, 372);
            Oval(g, blue, 532, 360, 74, 26); OvalLine(g, black, 3f, 532, 360, 74, 26);
            foreach (float cx in new float[] { 540, 554, 568 }) { Oval(g, C(205, 232, 230), cx - 5, 376, 10, 9); OvalLine(g, black, 2f, cx - 5, 376, 10, 9); }
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
