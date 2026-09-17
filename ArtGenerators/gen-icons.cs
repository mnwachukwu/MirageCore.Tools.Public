#:package SkiaSharp@3.116.1
#:package SkiaSharp.NativeAssets.Win32@3.116.1
#:project ../RepoPaths/RepoPaths.csproj

// Generates the application icon family from one drawing routine.
//
//     dotnet run --file gen-icons.cs
//
// Three icons, all the same mark — a lit tile on a dark grid, matching the site's favicon — telling
// each other apart by a badge in the bottom-right corner:
//
//     client      no badge        the plain mark is reserved for the game itself
//     editor      a pencil        it authors content
//     server      an open folder  it serves a world off disk
//
// Each app's installer carries that same app's icon. There is deliberately no separate installer mark:
// a fourth, disc-badged icon shared by all three made every Setup.exe look identical in a downloads
// folder, which is the one place the distinction actually matters.
//
// Drawn rather than rasterized. The mark is nine rounded rectangles and the badges are a handful of
// polygons, so the geometry lives here as numbers rather than in a file something else has to parse.
//
// Out:  assets/icons/{client,editor,server}.{ico,png,icns}
//       assets/icons/contact-sheet.png            (all three, every size, for eyeballing)
//       client/src/Mirage.Client.Shell/Icon.bmp   (MonoGame's window + taskbar icon)
//
// ── PORTED FROM PYTHON, 2026-08-15 ──────────────────────────────────────────────────────────────
// Was generate_app_icons.py and needed Pillow. The geometry below is a faithful port; the one
// deliberate change is that Skia anti-aliases its primitives where Pillow does not, so the original's
// 32x supersample existed purely to fake AA. It is KEPT anyway — drawing at 1024 and downsampling is
// also what makes the 16px and 24px sizes read as drawn rather than crushed, which is a separate
// benefit and the reason the contact sheet exists.

using SkiaSharp;

// ── Palette ──────────────────────────────────────────────────────────────────
// The accent #4fc9b7 is the brand mark and is shared with the site and both desktop apps. The ground
// and the unlit tiles are the forest-green neutrals the site is built on, so an icon and the window
// it opens agree.
var PLATE = new SKColor(8, 17, 14, 255);        // #08110e — deepest of the ramp, below the app background
var DIM = new SKColor(36, 70, 61, 255);         // #24463d — unlit tiles, dark enough to keep the atom loud
var LIT = new SKColor(79, 201, 183, 255);       // #4fc9b7

// ── Geometry, in the favicon's 32-unit space ─────────────────────────────────
const int UNITS = 32;
const float PLATE_RADIUS = 6f;
const int SS = 1024 / UNITS;        // supersample: 32 device pixels per unit
const int SIDE = UNITS * SS;

// The eight unlit tiles. Identical to public/favicon.svg in the site repo.
(int X, int Y, int W, int H)[] TILES =
[
    (4, 4, 7, 7), (13, 4, 7, 7), (22, 4, 6, 7),
    (4, 13, 7, 7), (22, 13, 6, 7),
    (4, 22, 7, 6), (13, 22, 7, 6), (22, 22, 6, 6),
];

// The atom, where the ninth tile used to be: a nucleus of three dots, two orbits crossing it, and an
// electron riding each one. The nucleus IS the core, which is what makes the figure say the name
// rather than decorate it.
//
// ⚠ What keeps this off React's mark is the DOTS, not the angles. React is three bare ellipses around
// a single small disc — nothing rides its paths and nothing sits in its middle but that disc. An
// electron on each orbit and a clustered nucleus are both things it does not have, and they read at a
// glance where the angle of the ellipses does not.
//
// A single orbit is no escape either: one ring around a sphere is a planet, whatever is at the centre.
//
// The nucleus carries the mark on its own at 16px, where the orbits have thinned below a pixel and the
// three dots have fused into one. That is the size the whole drawing is sized from: it degrades to a
// lit centre in a grid, which is what the mark was before the atom and still reads.
const float CENTER = 16f;
const float ORBIT_STROKE = 1.5f;

// Tilt, and the two radii, per orbit. Deliberately not a mirrored pair and deliberately not the same
// ellipse twice: a symmetric X reads as the diagram of an atom, and an uneven one reads as a drawing
// of this one.
(float Angle, float Rx, float Ry)[] ORBITS = [(-24f, 12.4f, 4.9f), (52f, 11.5f, 4.2f)];

// Which orbit each electron rides, and where on it, as that ellipse's own parameter in degrees.
//
// ⚠ THREE electrons, not one per orbit, and the wide one carries two. Two crossed ellipses have
// their far ends on opposite diagonals, so taking one end from each always lands both on the same
// side of the mark — two tops, or two lefts. A diagonal pair has to share an orbit, and once one
// orbit holds two the third is what keeps the figure off a straight line.
//
// They fill the top-right, bottom-left, and top-left. Bottom-right is left empty for the badge, a
// 4.3-unit circle at (25, 25), so the editor and server icons read as balanced rather than crowded.
(int Orbit, float T)[] ELECTRONS = [(0, 345f), (0, 155f), (1, 180f)];
const float ELECTRON_R = 1.6f;

// Three dots, offset from the centre and from each other, on no particular triangle.
//
// ⚠ Every pair OVERLAPS, and the margin is small: the earlier spread missed by a tenth of a unit on
// all three pairs, which drew three separate circles at every size. What should read is a core with
// some size to it, so if these move, check that each pair's centres stay closer than its radii sum.
(float Dx, float Dy, float R)[] NUCLEUS = [(0.15f, -1.35f, 1.75f), (-1.35f, 0.75f, 1.6f), (1.25f, 0.95f, 1.5f)];

// The badge sits on the bottom-right tile, drawn in the accent directly on the grid. An earlier
// version put the glyph on a filled circle, which made the badge half the width of the icon and
// turned every glyph into a silhouette fighting a hard circular edge.
const float BADGE_CX = 25.0f, BADGE_CY = 25.0f, BADGE_R = 4.3f;

int[] ICO_SIZES = [16, 24, 32, 48, 64, 128, 256];

float U(float v) => v * SS;                                   // unit space -> supersampled pixels
SKPoint Badge(float dx, float dy) => new(BADGE_CX + dx * BADGE_R, BADGE_CY + dy * BADGE_R);

void Poly(SKCanvas c, IEnumerable<SKPoint> pts, SKColor fill)
{
    using var path = new SKPath();
    bool first = true;
    foreach (var p in pts)
    {
        if (first) { path.MoveTo(U(p.X), U(p.Y)); first = false; }
        else path.LineTo(U(p.X), U(p.Y));
    }
    path.Close();
    using var paint = new SKPaint { Color = fill, IsAntialias = true, Style = SKPaintStyle.Fill };
    c.DrawPath(path, paint);
}

// Grows a polygon about its centroid. Used to paint a slightly larger copy in the badge color
// underneath a shape, leaving a gap around it — two adjacent dark shapes on one badge otherwise merge
// into a blob at icon sizes, and outlining is what keeps them legible as separate parts.
SKPoint[] Expand(SKPoint[] pts, float amount)
{
    float cx = pts.Average(p => p.X), cy = pts.Average(p => p.Y);
    return [.. pts.Select(p => new SKPoint(cx + (p.X - cx) * (1 + amount), cy + (p.Y - cy) * (1 + amount)))];
}

// A pencil lying at 45°, tip toward the top-right. Built along an axis rather than as literal corner
// coordinates, so the proportions stay right if the badge is ever resized. Kept narrow with a long
// taper on purpose: a fatter version with a stubby point read as a plain diagonal bar at 32px.
void DrawPencil(SKCanvas c)
{
    float dx = MathF.Cos(-MathF.PI / 4), dy = MathF.Sin(-MathF.PI / 4);
    float px = -dy, py = dx, w = 0.30f;
    SKPoint At(float t, float s) => Badge(dx * t + px * s, dy * t + py * s);

    Poly(c, [At(-1.05f, w), At(0.05f, w), At(0.05f, -w), At(-1.05f, -w)], LIT);   // body
    Poly(c, [At(0.05f, w), At(1.10f, 0), At(0.05f, -w)], LIT);                    // the long tip
    Poly(c, [At(-0.62f, w), At(-0.44f, w), At(-0.44f, -w), At(-0.62f, -w)], PLATE); // ferrule notch
}

// An open folder: a back panel with a tab, and a front flap leaning away from it. The flap is painted
// over an expanded copy of itself in the badge color so a gap separates it from the back panel —
// without that the two merge and the badge reads as an undifferentiated blob well before 32px.
void DrawFolder(SKCanvas c)
{
    SKPoint[] back = [Badge(-1.00f, 0.30f), Badge(-1.00f, -0.86f), Badge(-0.30f, -0.86f),
                      Badge(-0.08f, -0.50f), Badge(0.74f, -0.50f), Badge(0.74f, 0.30f)];
    Poly(c, back, LIT);

    // Wider at the bottom than the top, which is what reads as "open" rather than as a second
    // rectangle sitting in front.
    SKPoint[] flap = [Badge(-0.74f, -0.16f), Badge(0.90f, -0.16f), Badge(1.14f, 0.72f), Badge(-1.00f, 0.72f)];
    Poly(c, Expand(flap, 0.16f), PLATE);
    Poly(c, flap, LIT);
}

var BADGES = new (string Name, Action<SKCanvas>? Draw)[]
{
    ("client", null),
    ("editor", DrawPencil),
    ("server", DrawFolder),
};

SKBitmap Render(Action<SKCanvas>? badge)
{
    var bmp = new SKBitmap(SIDE, SIDE, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    var plate = new SKRoundRect(SKRect.Create(0, 0, SIDE, SIDE), U(PLATE_RADIUS));
    // Clip up front. The badge is deliberately allowed to overflow the rounded corner while drawing;
    // clipping trims it to the silhouette instead of leaving a bulge. (Pillow had no clip, so the
    // original composited an alpha mask afterwards to the same end.)
    canvas.ClipRoundRect(plate, antialias: true);

    using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
    paint.Color = PLATE;
    canvas.DrawRoundRect(plate, paint);

    paint.Color = DIM;
    foreach (var (x, y, w, h) in TILES)
        canvas.DrawRoundRect(SKRect.Create(U(x), U(y), U(w), U(h)), U(1), U(1), paint);

    paint.Color = LIT;
    for (int i = 0; i < ORBITS.Length; i++)
    {
        var (angle, rx, ry) = ORBITS[i];

        canvas.Save();
        canvas.RotateDegrees(angle, U(CENTER), U(CENTER));

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = U(ORBIT_STROKE);
        canvas.DrawOval(U(CENTER), U(CENTER), U(rx), U(ry), paint);

        // Inside the same rotation, so an electron is on the path by construction rather than by a
        // second piece of arithmetic that has to be kept in step with the ellipse.
        paint.Style = SKPaintStyle.Fill;
        foreach (var (orbit, degrees) in ELECTRONS)
        {
            if (orbit != i) continue;
            double t = degrees * Math.PI / 180.0;
            canvas.DrawCircle(U(CENTER + rx * (float)Math.Cos(t)),
                              U(CENTER + ry * (float)Math.Sin(t)),
                              U(ELECTRON_R), paint);
        }
        canvas.Restore();
    }

    // Last, so the cluster covers where the orbits meet rather than being crossed by them.
    paint.Style = SKPaintStyle.Fill;
    foreach (var (dx, dy, r) in NUCLEUS)
        canvas.DrawCircle(U(CENTER + dx), U(CENTER + dy), U(r), paint);

    badge?.Invoke(canvas);
    return bmp;
}

// Mitchell is the closest of Skia's resamplers to Pillow's Lanczos for downscaling — slightly softer,
// and without the ringing Lanczos can put on a hard accent edge at 16px.
SKBitmap Scaled(SKBitmap master, int size) =>
    master.Resize(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                  new SKSamplingOptions(SKCubicResampler.Mitchell))
    ?? throw new InvalidOperationException($"resize to {size} failed");

byte[] Png(SKBitmap bmp)
{
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// ── .ico ─────────────────────────────────────────────────────────────────────
// EVERY size is a PNG payload, which is what Pillow wrote and what therefore already went through the
// publish pipeline, Velopack's Setup.exe and Explorer's thumbnail cache without complaint.
//
// The alternative — raw DIBs below 256, which is the older and more widely-parsed layout — was tried
// and reverted. Uncompressed 32bpp bitmaps took the file from 20 KB to 107 KB apiece, and the only
// thing that buys is pre-Vista Windows, which a .NET 10 application cannot run on regardless. Paying
// five times the size for compatibility the rest of the stack has already ruled out is not a trade.
void WriteIco(string path, SKBitmap master, int[] sizes)
{
    var payloads = new List<byte[]>();
    foreach (int size in sizes)
    {
        using var scaled = Scaled(master, size);
        payloads.Add(Png(scaled));
    }

    using var outStream = new MemoryStream();
    using var ow = new BinaryWriter(outStream);
    ow.Write((short)0); ow.Write((short)1); ow.Write((short)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        ow.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // 0 means 256
        ow.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        ow.Write((byte)0); ow.Write((byte)0);
        ow.Write((short)1); ow.Write((short)32);
        ow.Write(payloads[i].Length); ow.Write(offset);
        offset += payloads[i].Length;
    }
    foreach (var p in payloads) ow.Write(p);
    File.WriteAllBytes(path, outStream.ToArray());
}

// ── .icns ────────────────────────────────────────────────────────────────────
// A trivial container — 'icns', total length, then typed chunks — and every type used here takes a
// PNG payload, so it can be assembled without Apple's iconutil (which only exists on macOS, and this
// has to run on the machine that builds).
void WriteIcns(string path, SKBitmap master)
{
    (string Code, int Size)[] types =
    [
        ("icp4", 16), ("icp5", 32), ("icp6", 64),
        ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
    ];
    using var chunks = new MemoryStream();
    byte[] be = new byte[4];   // hoisted: a stackalloc inside the loop is a stack-growth hazard
    foreach (var (code, size) in types)
    {
        using var scaled = Scaled(master, size);
        byte[] png = Png(scaled);
        chunks.Write(System.Text.Encoding.ASCII.GetBytes(code));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(be, png.Length + 8);
        chunks.Write(be);
        chunks.Write(png);
    }
    using var file = new MemoryStream();
    file.Write(System.Text.Encoding.ASCII.GetBytes("icns"));
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(be, (int)chunks.Length + 8);
    file.Write(be);
    file.Write(chunks.ToArray());
    File.WriteAllBytes(path, file.ToArray());
}

// ── Icon.bmp ─────────────────────────────────────────────────────────────────
// The BMP MonoGame uses for the client's window and taskbar icon. Separate from everything else
// because MonoGame does not read the executable's icon for this: SdlGameWindow looks for an embedded
// resource — <EntryNamespace>.Icon.bmp, then a bare Icon.bmp — and falls through to MonoGame.bmp, its
// own logo, embedded in the framework assembly. Ship neither and every window shows MonoGame branding.
//
// Hand-rolled because it has to be a BITMAPV4HEADER with explicit channel masks. The route in is
// SDL_LoadBMP, and a plain BITMAPINFOHEADER at 32bpp leaves the fourth channel formally undefined;
// SDL is entitled to read it as padding and often does, which turns the rounded corners into black
// ones. V4 states the alpha mask outright and removes the question.
void WriteWindowBmp(string path, SKBitmap master, int size = 256)
{
    using var icon = Scaled(master, size);
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);

    int bits = size * size * 4;
    const int headerSize = 108;                 // V4
    int offset = 14 + headerSize;

    w.Write((byte)'B'); w.Write((byte)'M');
    w.Write(offset + bits); w.Write((short)0); w.Write((short)0); w.Write(offset);

    w.Write(headerSize); w.Write(size); w.Write(size);
    w.Write((short)1); w.Write((short)32);
    w.Write(3);                                 // BI_BITFIELDS
    w.Write(bits);
    w.Write(2835); w.Write(2835);               // ~72 DPI
    w.Write(0); w.Write(0);
    w.Write(0x00FF0000); w.Write(0x0000FF00); w.Write(0x000000FF); w.Write(unchecked((int)0xFF000000));
    w.Write(0x57696E20);                        // 'Win ' — sRGB-ish, and what SDL expects to ignore
    w.Write(new byte[36]);                      // endpoints
    w.Write(0); w.Write(0); w.Write(0);         // gamma

    for (int y = size - 1; y >= 0; y--)         // bottom-up, BGRA to match the masks above
        for (int x = 0; x < size; x++)
        {
            var c = icon.GetPixel(x, y);
            w.Write(c.Blue); w.Write(c.Green); w.Write(c.Red); w.Write(c.Alpha);
        }
    File.WriteAllBytes(path, ms.ToArray());
}

// ── Contact sheet ────────────────────────────────────────────────────────────
// Every variant at every size on one strip, for judging the small sizes by eye. This is the only
// artifact here nothing consumes — it exists so a person can decide whether 16px still reads.
void WriteContactSheet(string path, (string Name, SKBitmap Master)[] masters)
{
    int[] sizes = [16, 24, 32, 48, 64, 128];
    const int pad = 12, label = 92;
    int biggest = sizes.Max();
    int width = label + sizes.Sum(s => s + pad);
    int height = pad + masters.Length * (biggest + pad);

    using var sheet = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using var c = new SKCanvas(sheet);
    // Lighter than PLATE on purpose — the sheet exists to judge the silhouette, which needs the icon's
    // own ground to read as darker than what surrounds it.
    c.Clear(new SKColor(27, 22, 52, 255));

    using var font = new SKFont(SKTypeface.Default, 13);
    using var text = new SKPaint { Color = new SKColor(237, 234, 248, 255), IsAntialias = true };

    for (int row = 0; row < masters.Length; row++)
    {
        int y = pad + row * (biggest + pad);
        c.DrawText(masters[row].Name, 10, y + biggest / 2 + 5, font, text);
        int x = label;
        foreach (int size in sizes)
        {
            using var scaled = Scaled(masters[row].Master, size);
            c.DrawBitmap(scaled, x, y + (biggest - size) / 2f);
            x += size + pad;
        }
    }
    File.WriteAllBytes(path, Png(sheet));
}

// ── Main ─────────────────────────────────────────────────────────────────────
// The game repo is a SIBLING of this one — see the tools README. Everything written below lands over
// there, because the icons are committed with the game and only the script that draws them lives here.
string engineRoot = MirageTools.RepoPaths.EngineRepo();
string outDir = Path.Combine(engineRoot, "assets", "icons");
Directory.CreateDirectory(outDir);

var built = new List<(string Name, SKBitmap Master)>();
foreach (var (name, badge) in BADGES)
{
    var master = Render(badge);
    built.Add((name, master));

    using (var ico = Scaled(master, 1024)) WriteIco(Path.Combine(outDir, $"{name}.ico"), master, ICO_SIZES);
    using (var png = Scaled(master, 512)) File.WriteAllBytes(Path.Combine(outDir, $"{name}.png"), Png(png));
    WriteIcns(Path.Combine(outDir, $"{name}.icns"), master);
    Console.WriteLine($"  {name,-10} ico + png + icns");
}

string windowIcon = Path.Combine(engineRoot, "client", "src", "Mirage.Client.Shell", "Icon.bmp");
WriteWindowBmp(windowIcon, built.First(b => b.Name == "client").Master);
Console.WriteLine($"  {"client",-10} Icon.bmp  ->  {Path.GetRelativePath(engineRoot, windowIcon)}");

WriteContactSheet(Path.Combine(outDir, "contact-sheet.png"), [.. built]);
foreach (var (_, m) in built) m.Dispose();

Console.WriteLine($"\nwrote {BADGES.Length * 3 + 2} files under {Path.GetRelativePath(engineRoot, outDir)}");
return 0;
