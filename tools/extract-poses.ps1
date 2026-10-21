# ref/source_sheet.png (체크무늬·별 배경이 그림에 박혀 있는 6포즈 시트) → 배경 제거한 포즈별 PNG(ref/pose1~6.png)
param([string]$Src = "$PSScriptRoot\..\ref\source_sheet.png", [string]$OutDir = "$PSScriptRoot\..\ref")
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Drawing; using System.Drawing.Imaging; using System.Collections.Generic;
public static class PoseExtract {
  static bool IsBg(byte r, byte g, byte b) {
    
    if (r > 218 && g > 218 && Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14) return true;
    if (r > 236 && g > 236 && b > 150 && b < 230 && r - b < 80 && Math.Abs(r - g) < 10) return true;
    return false;
  }
  public static void Run(string src, string outDir) {
    Bitmap bmp = new Bitmap(src); int W = bmp.Width, H = bmp.Height;
    BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] px = new byte[d.Stride * H]; System.Runtime.InteropServices.Marshal.Copy(d.Scan0, px, 0, px.Length); int st = d.Stride; bmp.UnlockBits(d);
    bool[] bg = new bool[W * H]; Stack<int> stack = new Stack<int>();
    Action<int,int> push = delegate(int x, int y) { if (x < 0 || y < 0 || x >= W || y >= H) return; int i = y * W + x; if (bg[i]) return;
      int o = y * st + x * 4; if (!IsBg(px[o + 2], px[o + 1], px[o])) return; bg[i] = true; stack.Push(i); };
    for (int x = 0; x < W; x++) { push(x, 0); push(x, H - 1); } for (int y = 0; y < H; y++) { push(0, y); push(W - 1, y); }
    while (stack.Count > 0) { int i = stack.Pop(); int x = i % W, y = i / W; push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1); }
    
    bool[] seen = new bool[W * H];
    for (int sy0 = 0; sy0 < H; sy0++) for (int sx0 = 0; sx0 < W; sx0++) {
      int i0 = sy0 * W + sx0; if (bg[i0] || seen[i0]) continue; int o0 = sy0 * st + sx0 * 4; if (!IsBg(px[o0 + 2], px[o0 + 1], px[o0])) continue;
      List<int> comp = new List<int>(); Stack<int> s3 = new Stack<int>(); s3.Push(i0); seen[i0] = true;
      while (s3.Count > 0) { int j = s3.Pop(); comp.Add(j); int jx = j % W, jy = j / W;
        int[] nb = { jx > 0 ? j - 1 : -1, jx < W - 1 ? j + 1 : -1, jy > 0 ? j - W : -1, jy < H - 1 ? j + W : -1 };
        foreach (int k in nb) { if (k < 0 || seen[k] || bg[k]) continue; int ko = (k / W) * st + (k % W) * 4; if (!IsBg(px[ko + 2], px[ko + 1], px[ko])) continue; seen[k] = true; s3.Push(k); } }
      if (comp.Count > 40) foreach (int j in comp) bg[j] = true;
    }
    bool[] alive = new bool[W * H];
    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) alive[y * W + x] = !bg[y * W + x];
    bool[] kill = new bool[W * H];
    for (int y = 1; y < H - 1; y++) for (int x = 1; x < W - 1; x++) {
      int i = y * W + x; if (!alive[i]) continue;
      bool near = bg[i - 1] || bg[i + 1] || bg[i - W] || bg[i + W]; if (!near) continue;
      int o = y * st + x * 4; int r = px[o + 2], g = px[o + 1], b = px[o];
      if (r > 200 && g > 190) kill[i] = true;   
    }
    for (int i = 0; i < kill.Length; i++) if (kill[i]) alive[i] = false;
    
    int[] cx = { 0, 295, 570, W }, cy = { 0, 285, H };
    int n = 0;
    for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++) {
      int x0 = cx[col], x1 = cx[col + 1], y0 = cy[row], y1 = cy[row + 1];
      int[] label = new int[W * H]; List<int> sizes = new List<int>(); sizes.Add(0);
      for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) {
        int i = y * W + x; if (!alive[i] || label[i] != 0) continue;
        int id = sizes.Count; sizes.Add(0); Stack<int> s2 = new Stack<int>(); s2.Push(i); label[i] = id;
        while (s2.Count > 0) { int j = s2.Pop(); sizes[id]++; int jx = j % W, jy = j / W;
          for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int nx = jx + dx, ny = jy + dy; if (nx < x0 || nx >= x1 || ny < y0 || ny >= y1) continue; int k = ny * W + nx; if (alive[k] && label[k] == 0) { label[k] = id; s2.Push(k); } } }
      }
      int big = 0; for (int k = 1; k < sizes.Count; k++) if (sizes[k] > sizes[big] || big == 0) big = k;
      int l = W, t = H, r2 = -1, b2 = -1;
      for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int lb = label[y * W + x]; if (lb != 0 && (lb == big || sizes[lb] > 25)) { if (x < l) l = x; if (y < t) t = y; if (x > r2) r2 = x; if (y > b2) b2 = y; } }
      int pad = 3; l -= pad; t -= pad; r2 += pad; b2 += pad; int ow = r2 - l + 1, oh = b2 - t + 1;
      Bitmap o2 = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
      for (int y = 0; y < oh; y++) for (int x = 0; x < ow; x++) {
        int sx = l + x, sy = t + y; if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue; int i = sy * W + sx; int lb = label[i];
        if (lb == 0 || !(lb == big || sizes[lb] > 25)) continue; int o = sy * st + sx * 4;
        
        int a = 255; if ((sx > 0 && !alive[i - 1]) || (sx < W - 1 && !alive[i + 1]) || (sy > 0 && !alive[i - W]) || (sy < H - 1 && !alive[i + W])) a = 190;
        o2.SetPixel(x, y, Color.FromArgb(a, px[o + 2], px[o + 1], px[o]));
      }
      n++; o2.Save(System.IO.Path.Combine(outDir, "pose" + n + ".png"), ImageFormat.Png); o2.Dispose();
      Console.WriteLine("pose" + n + ": " + ow + "x" + oh);
    }
  }
}
'@
[PoseExtract]::Run((Resolve-Path $Src).Path, (Resolve-Path $OutDir).Path)
