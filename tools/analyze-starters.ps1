# 분석용: source_starters.png 를 외곽선(어두운 선)으로 둘러싸인 색 영역으로 나누고 목록·확인용 이미지를 만든다.
param([string]$Src = "$PSScriptRoot\..\ref\source_starters.png", [string]$OutDir = "$PSScriptRoot\..\obj")
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Collections.Generic; using System.Text;
public static class StarterAnalyze {
  public static int[] Label; public static int W, H; public static byte[] Px; public static int Stride;
  public static bool IsBg(int o) { return Px[o] > 238 && Px[o + 1] > 238 && Px[o + 2] > 238; }
  public static bool IsDark(int o) { return (Px[o + 2] * 30 + Px[o + 1] * 59 + Px[o] * 11) / 100 < 105; }
  public static string Run(string src, string outDir) {
    Bitmap bmp = new Bitmap(src); W = bmp.Width; H = bmp.Height;
    BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    Px = new byte[d.Stride * H]; System.Runtime.InteropServices.Marshal.Copy(d.Scan0, Px, 0, Px.Length); Stride = d.Stride; bmp.UnlockBits(d);
    Label = new int[W * H]; int next = 0; List<long[]> info = new List<long[]>();   
    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
      int i = y * W + x, o = y * Stride + x * 4; if (Label[i] != 0 || IsBg(o) || IsDark(o)) continue;
      next++; long[] a = new long[] { 0, 0, 0, W, H, 0, 0, 0, 0, 0 }; Stack<int> st = new Stack<int>(); st.Push(i); Label[i] = next;
      while (st.Count > 0) { int j = st.Pop(); int jx = j % W, jy = j / W; int jo = jy * Stride + jx * 4;
        a[0]++; a[1] += jx; a[2] += jy; if (jx < a[3]) a[3] = jx; if (jy < a[4]) a[4] = jy; if (jx > a[5]) a[5] = jx; if (jy > a[6]) a[6] = jy; a[7] += Px[jo + 2]; a[8] += Px[jo + 1]; a[9] += Px[jo];
        int[] nb = { jx > 0 ? j - 1 : -1, jx < W - 1 ? j + 1 : -1, jy > 0 ? j - W : -1, jy < H - 1 ? j + W : -1 };
        foreach (int k in nb) { if (k < 0 || Label[k] != 0) continue; int ko = (k / W) * Stride + (k % W) * 4; if (IsBg(ko) || IsDark(ko)) continue; Label[k] = next; st.Push(k); } }
      info.Add(a);
    }
    StringBuilder sb = new StringBuilder();
    for (int k = 0; k < info.Count; k++) { long[] a = info[k]; if (a[0] < 25) continue;
      sb.AppendLine(string.Format("{0,3} n={1,5} c=({2,3},{3,3}) bbox=({4},{5})-({6},{7}) rgb=({8},{9},{10})", k + 1, a[0], a[1] / a[0], a[2] / a[0], a[3], a[4], a[5], a[6], a[7] / a[0], a[8] / a[0], a[9] / a[0])); }
    
    Bitmap vis = new Bitmap(W * 2, H * 2); Random rnd = new Random(5); Color[] pal = new Color[next + 1];
    for (int k = 0; k <= next; k++) pal[k] = Color.FromArgb(60 + rnd.Next(190), 60 + rnd.Next(190), 60 + rnd.Next(190));
    using (Graphics g = Graphics.FromImage(vis)) { g.Clear(Color.White);
      for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { int o = y * Stride + x * 4, l = Label[y * W + x];
        Color c = l != 0 ? pal[l] : (IsBg(o) ? Color.White : Color.Black);
        vis.SetPixel(x * 2, y * 2, c); vis.SetPixel(x * 2 + 1, y * 2, c); vis.SetPixel(x * 2, y * 2 + 1, c); vis.SetPixel(x * 2 + 1, y * 2 + 1, c); }
      Font f = new Font("Arial", 8); for (int k = 0; k < info.Count; k++) { long[] a = info[k]; if (a[0] < 60) continue; g.DrawString((k + 1).ToString(), f, Brushes.Black, a[1] / a[0] * 2 - 6, a[2] / a[0] * 2 - 6); } }
    vis.Save(System.IO.Path.Combine(outDir, "starters_regions.png"), ImageFormat.Png);
    return sb.ToString();
  }
}
'@
[StarterAnalyze]::Run((Resolve-Path $Src).Path, (Resolve-Path $OutDir).Path)
