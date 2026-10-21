using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Pass 1: classify each pixel of the group picture to a character (1 Bulbasaur, 2 Pikachu, 3 Charmander, 4 Squirtle).
// Pass 2: draw missing parts (see Reconstruct) and save one PNG per character.
public static class StarterExtract
{
    static int W, H, St; static byte[] Px; static bool[] Bg; static int[] Lab;

    static bool InEllipse(int x, int y, double cx, double cy, double rx, double ry)
    { double dx = (x - cx) / rx, dy = (y - cy) / ry; return dx * dx + dy * dy <= 1; }

    // confident classification; 0 = decide later (outlines, whites, anti-aliased mixes)
    static int Classify(int x, int y, int r, int g, int b)
    {
        bool green = g >= r + 12 && b >= r * .6;
        if (green)
        {
            if (x < 345) return 1;
            if (x >= 380 && x <= 500 && y < 172) return 3;   // Charmander teal eyes
            if (x >= 520) return 4;
            return 0;
        }
        if (r > 130 && g < 85 && b < 125)                       // crimson / red eyes
        { if (x < 260) return 1; if (x > 520) return 4; return 0; }
        bool pink = r > 150 && g < 150 && b >= 85 && r - g > 50;
        if (pink)
        {
            if (x < 215) return 1;
            if (x < 345) return 2;
            if (x >= 420 && x <= 535 && y >= 160 && y <= 212) return 3;
            if (x > 540 && y < 200) return 4;
            return 0;
        }
        bool warmSat = r > 225 && b < 115 && g < 225;
        if (warmSat && x > 592 && y > 155 && y < 295) return 3;  // tail flame
        bool orange = r >= 200 && g >= 92 && g <= 152 && b < 100;
        if (orange) return x >= 372 ? 3 : 2;
        bool brown = r >= 140 && r <= 205 && g >= 68 && g <= 118 && b >= 38 && b <= 92;
        if (brown) return x >= 385 ? 3 : 2;
        bool cream = r > 215 && g > 200 && b >= 90 && b < 160;
        bool olive = r >= 190 && r <= 232 && g >= 175 && g <= 210 && b >= 45 && b < 95;
        if (cream || olive)
        {
            if (InEllipse(x, y, 472, 290, 50, 75)) return 3;
            if (x >= 520) return 4;
            return 0;
        }
        bool yellow = r >= 238 && g >= 185 && g <= 228 && b < 105;
        if (yellow) return x < 450 ? 2 : 0;
        bool pShadow = r >= 205 && r <= 238 && g >= 150 && g < 188 && b < 112;
        if (pShadow) return x < 450 ? 2 : 0;
        return 0;
    }

    public static void Run(string src, string outDir, string debug)
    {
        Bitmap bmp = new Bitmap(src); W = bmp.Width; H = bmp.Height;
        BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Px = new byte[d.Stride * H]; System.Runtime.InteropServices.Marshal.Copy(d.Scan0, Px, 0, Px.Length); St = d.Stride; bmp.UnlockBits(d);
        Bg = new bool[W * H]; Lab = new int[W * H];
        Stack<int> stack = new Stack<int>();
        Action<int, int> pushBg = delegate (int x, int y) { if (x < 0 || y < 0 || x >= W || y >= H) return; int i = y * W + x; if (Bg[i]) return; int o = y * St + x * 4; if (Px[o] < 236 || Px[o + 1] < 236 || Px[o + 2] < 236) return; Bg[i] = true; stack.Push(i); };
        for (int x = 0; x < W; x++) { pushBg(x, 0); pushBg(x, H - 1); } for (int y = 0; y < H; y++) { pushBg(0, y); pushBg(W - 1, y); }
        while (stack.Count > 0) { int i = stack.Pop(); int x = i % W, y = i / W; pushBg(x + 1, y); pushBg(x - 1, y); pushBg(x, y + 1); pushBg(x, y - 1); }

        Queue<int> q = new Queue<int>();
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            int i = y * W + x; if (Bg[i]) continue; int o = y * St + x * 4;
            int c = Classify(x, y, Px[o + 2], Px[o + 1], Px[o]); if (c != 0) { Lab[i] = c; q.Enqueue(i); }
        }
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = i % W, y = i / W;
            int[] nb = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
            foreach (int k in nb) { if (k < 0 || Bg[k] || Lab[k] != 0) continue; Lab[k] = Lab[i]; q.Enqueue(k); }
        }
        string[] names = { "", "bulbasaur", "pikachu", "charmander", "squirtle" };
        for (int c = 1; c <= 4; c++)
        {
            int l = W, t = H, r = -1, b = -1;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (Lab[y * W + x] == c) { if (x < l) l = x; if (y < t) t = y; if (x > r) r = x; if (y > b) b = y; }
            Console.WriteLine(names[c] + " bbox " + l + "," + t + " - " + r + "," + b);
        }
        // debug sheet: each character on a gray background
        Bitmap sheet = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        Color[] tint = { Color.White, Color.FromArgb(120, 255, 120), Color.FromArgb(255, 255, 120), Color.FromArgb(255, 150, 120), Color.FromArgb(120, 200, 255) };
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            int o = y * St + x * 4, c = Lab[y * W + x];
            Color px = c == 0 ? Color.FromArgb(80, 80, 80) : Color.FromArgb(Px[o + 2], Px[o + 1], Px[o]);
            if (c != 0) { int e = EdgeKind(x, y); if (e == 1) px = Color.Magenta; }
            sheet.SetPixel(x, y, px);
        }
        sheet.Save(System.IO.Path.Combine(outDir, "..\\obj\\starters_masks.png"), ImageFormat.Png);
        Reconstruct.Run(Px, St, W, H, Lab, Bg, outDir);
    }

    // 1 when the pixel's label differs from a 4-neighbour's non-background label (character boundary)
    static int EdgeKind(int x, int y)
    {
        int c = Lab[y * W + x]; if (c == 0) return 0;
        int[][] d = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        foreach (int[] v in d) { int nx = x + v[0], ny = y + v[1]; if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue; int l2 = Lab[ny * W + nx]; if (l2 != 0 && l2 != c) return 1; }
        return 0;
    }
}

