#:package SkiaSharp@3.116.1
#:package SkiaSharp.NativeAssets.Win32@3.116.1
#:project ../RepoPaths/RepoPaths.csproj

// Generates the three controller-scheme reference images used by the in-game Help panel.
//
//     dotnet run --file gen-control-images.cs
//
// Out: client/src/Mirage.Client.Shell/assets/graphics/Controls{Keyboard,Xbox,Playstation}.png
//
// Each image is 800x600. The upper 470px holds the primary controls (Move / Run / Interact / Pick Up
// / Cycle / Chat); the bottom strip holds the ACTION BAR. There is no header — the Controls panel
// already labels each scheme with its own tab strip, so a title inside the picture was wasted height.
//
// ── PORTED FROM PYTHON, 2026-08-15 ──────────────────────────────────────────────────────────────
// Was generate_control_images.py and needed Pillow. Two things were broken and are fixed here rather
// than faithfully reproduced, because reproducing them would have meant shipping known-wrong art:
//
//   1. IT WROTE TO ../client-csharp/, a path that has not existed since the folder rename. The script
//      could not have run since, which is most of why the images went stale.
//
//   2. THE BOTTOM STRIP SAID "POTIONS" AND SHOWED THREE. It is now a four-slot ACTION BAR whose slots
//      the player BINDS — right-click an item or spell — so fixed "HP Potion / MP Potion / SP Potion"
//      captions described neither the count nor the concept. Read off GameplayScreen.Update: keyboard
//      1-4 (or NumPad), gamepad LT-or-RT plus X/Y/B/A. That face order is deliberate and worth not
//      "tidying": X/Y/B were the old HP/MP/SP potions, so existing muscle memory still lands on the
//      same three and slot 4 took the free button.
//
// BOTH TRIGGERS open the bar and the one held decides where the slot points: LT at the target, RT at
// the caster. RT wins when both are down, so aim switches without releasing first. The prepared spell
// has no self-cast on either scheme — that slot only ever holds a SubHp spell.
//
// Fonts are resolved by FAMILY rather than by path. The original hardcoded C:/Windows/Fonts/, which
// made a picture of a keyboard a Windows-only artifact for no reason.

using SkiaSharp;

const int W = 800, H = 600;

// ── Palette (matches the dark game-panel look) ───────────────────────────────
var BG = new SKColor(12, 12, 22);
var PANEL_BORDER = new SKColor(74, 74, 108);
var TITLE_COLOR = new SKColor(255, 210, 70);      // gold
var ACTION_COLOR = new SKColor(236, 236, 246);
var SUB_COLOR = new SKColor(150, 150, 174);
var GROUP_BG = new SKColor(20, 20, 38);
var GROUP_BORDER = new SKColor(92, 92, 134);
var KEY_BORDER = new SKColor(12, 12, 20);

// ── Fonts ────────────────────────────────────────────────────────────────────
// A monospace family, by name, with per-platform fallbacks. Skia's MatchFamily returns null for a
// family the system does not have, so this walks the list and only gives up at the end.
SKTypeface Face(bool bold)
{
    string[] candidates = ["Consolas", "DejaVu Sans Mono", "Liberation Mono", "Menlo", "Courier New"];
    var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
    foreach (var name in candidates)
        if (SKFontManager.Default.MatchFamily(name, style) is { } tf) return tf;
    // Last resort: whatever the system calls monospace. Metrics differ, so the layout may breathe
    // slightly, but a legible image beats a crash on a machine with an unusual font set.
    return SKFontManager.Default.MatchFamily(null, style) ?? SKTypeface.Default;
}
var MONO = Face(bold: false);
var MONO_B = Face(bold: true);

SKFont Fnt(float size, bool bold = true) => new(bold ? MONO_B : MONO, size);
var SECTION_F = Fnt(26);
var ACTION_F = Fnt(27);
var SUB_F = Fnt(19, bold: false);
var FACE_F = Fnt(27);
var PLUS_F = Fnt(32);
const int FACE_R = 26;        // face-button radius (under the row pitch so buttons don't touch)
const int BTN_CX = 168;       // horizontal center of the primary button column
const int ACTION_X = 348;     // left edge of the action text column

// Pillow's anchor="mm" centers on BOTH axes; anchor="lm" is left-x, middle-y. Skia draws from a
// baseline, so the vertical centering is done here once rather than guessed at each call site.
void CMid(SKCanvas c, float x, float y, string text, SKFont font, SKColor color)
{
    using var p = new SKPaint { Color = color, IsAntialias = true };
    font.MeasureText(text, out var bounds);
    c.DrawText(text, x - bounds.MidX, y - bounds.MidY, font, p);
}
void CLeft(SKCanvas c, float x, float y, string text, SKFont font, SKColor color)
{
    using var p = new SKPaint { Color = color, IsAntialias = true };
    font.MeasureText(text, out var bounds);
    c.DrawText(text, x, y - bounds.MidY, font, p);
}

SKPaint Fill(SKColor c) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Fill };
SKPaint Stroke(SKColor c, float w) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w };

// ── Frame ────────────────────────────────────────────────────────────────────
(SKBitmap, SKCanvas) Frame()
{
    var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    var c = new SKCanvas(bmp);
    c.Clear(BG);
    using var border = Stroke(PANEL_BORDER, 3);
    c.DrawRect(SKRect.Create(1.5f, 1.5f, W - 3, H - 3), border);
    return (bmp, c);
}

void GroupBox(SKCanvas c, float x0, float y0, float x1, float y1)
{
    var r = new SKRoundRect(new SKRect(x0, y0, x1, y1), 10);
    using var bg = Fill(GROUP_BG);
    using var line = Stroke(GROUP_BORDER, 2);
    c.DrawRoundRect(r, bg);
    c.DrawRoundRect(r, line);
}

// ── Keyboard keycap ──────────────────────────────────────────────────────────
void Keycap(SKCanvas c, float cx, float cy, int w, int h, string label, float fsize = 30)
{
    float x0 = cx - w / 2f, y0 = cy - h / 2f, x1 = x0 + w, y1 = y0 + h;
    var baseC = new SKColor(66, 66, 80);
    var hi = new SKColor(104, 104, 126);
    var sh = new SKColor(34, 34, 46);
    const int bev = 4;

    using (var p = Fill(baseC)) c.DrawRect(new SKRect(x0, y0, x1, y1), p);
    using (var p = Fill(hi))
    {
        c.DrawRect(new SKRect(x0, y0, x1, y0 + bev), p);       // top highlight
        c.DrawRect(new SKRect(x0, y0, x0 + bev, y1), p);       // left highlight
    }
    using (var p = Fill(sh))
    {
        c.DrawRect(new SKRect(x0, y1 - bev, x1, y1), p);       // bottom shadow
        c.DrawRect(new SKRect(x1 - bev, y0, x1, y1), p);       // right shadow
    }
    using (var p = Stroke(KEY_BORDER, 2)) c.DrawRect(new SKRect(x0, y0, x1, y1), p);
    using var f = Fnt(fsize);
    CMid(c, cx, cy + 1, label, f, new SKColor(242, 242, 252));
}

void Wasd(SKCanvas c, float cx, float cy)
{
    const int k = 52, gap = 6;
    float top = cy - (k + gap) / 2f, bot = cy + (k + gap) / 2f;
    Keycap(c, cx, top, k, k, "W");
    Keycap(c, cx - (k + gap), bot, k, k, "A");
    Keycap(c, cx, bot, k, k, "S");
    Keycap(c, cx + (k + gap), bot, k, k, "D");
}

// ── Gamepad parts ────────────────────────────────────────────────────────────
void Dpad(SKCanvas c, float cx, float cy, int s)
{
    int arm = (int)(s * 0.30), L = (int)(s * 0.5);
    var dark = new SKColor(48, 48, 62);
    var hi = new SKColor(84, 84, 106);
    using (var p = Fill(KEY_BORDER))
    {
        c.DrawRect(new SKRect(cx - arm - 2, cy - L - 2, cx + arm + 2, cy + L + 2), p);
        c.DrawRect(new SKRect(cx - L - 2, cy - arm - 2, cx + L + 2, cy + arm + 2), p);
    }
    using (var p = Fill(dark))
    {
        c.DrawRect(new SKRect(cx - arm, cy - L, cx + arm, cy + L), p);
        c.DrawRect(new SKRect(cx - L, cy - arm, cx + L, cy + arm), p);
    }
    using (var p = Fill(hi))
    {
        c.DrawRect(new SKRect(cx - arm, cy - L, cx + arm, cy - L + 4), p);
        c.DrawRect(new SKRect(cx - L, cy - arm, cx - L + 4, cy + arm), p);
    }
    int a = (int)(arm * 0.5);
    var notch = Fill(new SKColor(150, 150, 175));
    void Tri(params SKPoint[] pts)
    {
        using var path = new SKPath();
        path.MoveTo(pts[0]); path.LineTo(pts[1]); path.LineTo(pts[2]); path.Close();
        c.DrawPath(path, notch);
    }
    Tri(new(cx, cy - L + 6), new(cx - a, cy - L + 6 + a), new(cx + a, cy - L + 6 + a));
    Tri(new(cx, cy + L - 6), new(cx - a, cy + L - 6 - a), new(cx + a, cy + L - 6 - a));
    Tri(new(cx - L + 6, cy), new(cx - L + 6 + a, cy - a), new(cx - L + 6 + a, cy + a));
    Tri(new(cx + L - 6, cy), new(cx + L - 6 - a, cy - a), new(cx + L - 6 - a, cy + a));
    notch.Dispose();
}

void Stick(SKCanvas c, float cx, float cy, int r, string? label = null)
{
    using (var p = Fill(new SKColor(26, 26, 38))) c.DrawCircle(cx, cy, r, p);
    using (var p = Stroke(KEY_BORDER, 3)) c.DrawCircle(cx, cy, r, p);
    int rc = (int)(r * 0.62);
    using (var p = Fill(new SKColor(70, 70, 92))) c.DrawCircle(cx, cy, rc, p);
    using (var p = Stroke(new SKColor(18, 18, 28), 2)) c.DrawCircle(cx, cy, rc, p);
    if (label is not null)
    {
        // An LS/RS marker on the cap rather than a cosmetic highlight dot — the two sticks do
        // different jobs here and the picture has to say which is which.
        using var f = Fnt(Math.Max(12, (int)(rc * 0.95)));
        CMid(c, cx, cy + 1, label, f, new SKColor(236, 236, 246));
    }
}

void XboxFace(SKCanvas c, float cx, float cy, int r, string letter, SKColor color)
{
    var dk = new SKColor((byte)(color.Red / 2), (byte)(color.Green / 2), (byte)(color.Blue / 2));
    using (var p = Fill(color)) c.DrawCircle(cx, cy, r, p);
    using (var p = Stroke(dk, 3)) c.DrawCircle(cx, cy, r, p);
    CMid(c, cx, cy + 1, letter, FACE_F, letter == "Y" ? new SKColor(22, 22, 28) : SKColors.White);
}

void PsFace(SKCanvas c, float cx, float cy, int r, string symbol)
{
    using (var p = Fill(new SKColor(34, 34, 46))) c.DrawCircle(cx, cy, r, p);
    using (var p = Stroke(KEY_BORDER, 3)) c.DrawCircle(cx, cy, r, p);
    int s = (int)(r * 0.5);
    switch (symbol)
    {
        case "triangle":
            {
                using var path = new SKPath();
                path.MoveTo(cx, cy - s);
                path.LineTo(cx - (int)(s * 0.92), cy + (int)(s * 0.62));
                path.LineTo(cx + (int)(s * 0.92), cy + (int)(s * 0.62));
                path.Close();
                using var p = Stroke(new SKColor(88, 214, 156), 4);
                p.StrokeJoin = SKStrokeJoin.Round;
                c.DrawPath(path, p);
                break;
            }
        case "circle":
            using (var p = Stroke(new SKColor(232, 84, 100), 4)) c.DrawCircle(cx, cy, s, p);
            break;
        case "cross":
            using (var p = Stroke(new SKColor(96, 152, 232), 5))
            {
                c.DrawLine(cx - s, cy - s, cx + s, cy + s, p);
                c.DrawLine(cx - s, cy + s, cx + s, cy - s, p);
            }
            break;
        case "square":
            using (var p = Stroke(new SKColor(232, 112, 182), 4))
                c.DrawRect(new SKRect(cx - s, cy - s, cx + s, cy + s), p);
            break;
    }
}

void Pill(SKCanvas c, float cx, float cy, int w, int h, string label, SKColor fill, float fsize = 18)
{
    var r = new SKRoundRect(SKRect.Create(cx - w / 2f, cy - h / 2f, w, h), h / 2f);
    using (var p = Fill(fill)) c.DrawRoundRect(r, p);
    using (var p = Stroke(KEY_BORDER, 3)) c.DrawRoundRect(r, p);
    using var f = Fnt(fsize);
    CMid(c, cx, cy + 1, label, f, new SKColor(236, 236, 246));
}

// ── Action-bar strip (shared bottom band) ────────────────────────────────────
// The slots are BOUND rather than fixed, and HOW MANY there are is the game's: a world declares its
// bar and may declare none at all. Four are drawn because four keycaps fit the strip, and the caption
// says so — a picture of four numbered keys with no explanation invites the reader to assume both that
// the numbers mean something inherent and that there are four of them.
// The strip is 130px tall and has to carry three rows — title, buttons, captions. The modifier
// reminder therefore sits INLINE with the title rather than on a row of its own: given its own line it
// collided with "ACTION BAR" above and pushed the captions off the bottom edge of the canvas.
//
// ⚠ There is no room for a fourth row, and the three images share this position. The gamepad layouts
// run about 50px lower than the keyboard's, so lifting the strip to make room drives the divider
// through their shoulder-button box — which is why the note that the slot COUNT is the game's lives in
// the Controls panel's prose rather than here.
const int STRIP_TOP = 470, STRIP_TITLE_Y = 492, STRIP_ROW_Y = 536, STRIP_LABEL_Y = 576;
// Evenly spaced, and far enough right that the gamepad's "+" (drawn 46px left of each button) still
// clears the frame. An earlier hand-picked set bunched the last two together.
int[] SLOT_CX = [150, 335, 520, 705];

void Divider(SKCanvas c)
{
    using var p = Stroke(GROUP_BORDER, 2);
    c.DrawLine(20, STRIP_TOP, W - 21, STRIP_TOP, p);
}

void BarKeyboard(SKCanvas c)
{
    Divider(c);
    CLeft(c, 70, STRIP_TITLE_Y, "ACTION BAR", SECTION_F, TITLE_COLOR);
    // Ctrl is named here rather than on a row of its own: the strip below IS the action bar, so a
    // "1-4" row above it would list the same four keys twice. Mirrors where the pad names its triggers.
    CLeft(c, 250, STRIP_TITLE_Y, "Hold", SUB_F, SUB_COLOR);
    Pill(c, 334, STRIP_TITLE_Y, 74, 26, "Ctrl", new SKColor(44, 44, 60), 14);
    CLeft(c, 382, STRIP_TITLE_Y, "for self  —  right-click to bind", SUB_F, SUB_COLOR);
    for (int i = 0; i < 4; i++)
    {
        Keycap(c, SLOT_CX[i], STRIP_ROW_Y, 54, 46, (i + 1).ToString(), 30);
        CMid(c, SLOT_CX[i], STRIP_LABEL_Y, $"Slot {i + 1}", SUB_F, ACTION_COLOR);
    }
}

void BarGamepad(SKCanvas c, bool xbox, string lt, string rt, object[] faces)
{
    Divider(c);
    CLeft(c, 70, STRIP_TITLE_Y, "ACTION BAR", SECTION_F, TITLE_COLOR);
    // "Hold [LT] target [RT] self" inline with the title. WHICH trigger is the whole instruction here:
    // both open the bar and the one held decides where the slot points, so naming only one — or joining
    // them with "or" — would teach the player that the choice does not matter. The binding hint gives up
    // its words to make room, since a slot with nothing in it explains itself the moment it is pressed.
    // Laid out left to right against the 790px inner edge: a pill is CENTRED on its x and spans 27px
    // either side, while CLeft starts text at its x, so every gap below is the previous run's width.
    CLeft(c, 250, STRIP_TITLE_Y, "Hold", SUB_F, SUB_COLOR);
    var triggerFill = new SKColor(44, 44, 60);
    Pill(c, 320, STRIP_TITLE_Y, 54, 26, lt, triggerFill, 14);
    CLeft(c, 356, STRIP_TITLE_Y, "target", SUB_F, SUB_COLOR);
    Pill(c, 452, STRIP_TITLE_Y, 54, 26, rt, triggerFill, 14);
    CLeft(c, 488, STRIP_TITLE_Y, "self", SUB_F, SUB_COLOR);
    CLeft(c, 552, STRIP_TITLE_Y, "—  right-click to bind", SUB_F, SUB_COLOR);

    for (int i = 0; i < 4; i++)
    {
        float cx = SLOT_CX[i];
        CMid(c, cx - 46, STRIP_ROW_Y, "+", PLUS_F, ACTION_COLOR);
        if (xbox)
        {
            var (letter, color) = ((string, SKColor))faces[i];
            XboxFace(c, cx, STRIP_ROW_Y, FACE_R, letter, color);
        }
        else PsFace(c, cx, STRIP_ROW_Y, FACE_R, (string)faces[i]);
        CMid(c, cx, STRIP_LABEL_Y, $"Slot {i + 1}", SUB_F, ACTION_COLOR);
    }
}

// ── Builders ─────────────────────────────────────────────────────────────────
SKBitmap BuildKeyboard()
{
    var (bmp, c) = Frame();
    // Move (WASD cluster). Ctrl+WASD turns the same keys into a face-only input, so it rides as a
    // sub-line under "Move" instead of consuming a whole row.
    GroupBox(c, 70, 16, 266, 134);
    Wasd(c, BTN_CX, 75);
    CLeft(c, ACTION_X, 75 - 11, "Move", ACTION_F, ACTION_COLOR);
    CLeft(c, ACTION_X, 75 + 13, "(Hold Ctrl: face only)", SUB_F, SUB_COLOR);

    (int Cy, string Label, int W, int H, int Fs, string Action, string? Sub)[] rows =
    [
        (165, "Shift", 120, 46, 24, "Run", "(Hold)"),
        (225, "E", 54, 46, 30, "Interact", "(Shops, Signs, Conversations)"),
        (285, "F", 54, 46, 30, "Pick Up", null),
        (345, "Tab", 84, 46, 24, "Cycle Target", "(+Shift Reverse, +Ctrl Self)"),
        (405, "Enter", 112, 46, 24, "Chat", null),
    ];
    foreach (var (cy, label, w, h, fs, action, sub) in rows)
    {
        Keycap(c, BTN_CX, cy, w, h, label, fs);
        if (sub is not null)
        {
            CLeft(c, ACTION_X, cy - 11, action, ACTION_F, ACTION_COLOR);
            CLeft(c, ACTION_X, cy + 13, sub, SUB_F, SUB_COLOR);
        }
        else CLeft(c, ACTION_X, cy, action, ACTION_F, ACTION_COLOR);
    }
    BarKeyboard(c);
    c.Dispose();
    return bmp;
}

SKBitmap BuildGamepad(bool xbox)
{
    var (bmp, c) = Frame();
    string lb = xbox ? "LB" : "L1", lt = xbox ? "LT" : "L2";
    string rb = xbox ? "RB" : "R1", rt = xbox ? "RT" : "R2";

    // Move (D-pad + left stick) on top; right stick (face-only) on a second row. The face-button
    // column underneath is a tight fit between this box and the shoulder box, so the Move box must
    // not bleed down into it.
    GroupBox(c, 70, 12, 272, 128);
    Dpad(c, 124, 52, 72);
    // r=21, not 28. At 28 the two sticks spanned y 24-80 and 68-124 — a twelve-pixel overlap, with the
    // RS cap eating the bottom of LS. The box is only 116 tall and has to hold both, so the radius is
    // what gives; 21 leaves a 6px gap between them and 7px under RS.
    Stick(c, 216, 52, 21, "LS");
    CLeft(c, ACTION_X, 52, "Move", ACTION_F, ACTION_COLOR);
    Stick(c, 216, 100, 21, "RS");
    CLeft(c, ACTION_X, 100, "Face Direction", ACTION_F, ACTION_COLOR);

    (int Cy, object Btn, string Action, string? Sub)[] faces = xbox
        ?
        [
            (168, ("B", new SKColor(214, 69, 59)), "Run", "(Hold)"),
            (233, ("X", new SKColor(59, 124, 214)), "Interact", null),
            (298, ("A", new SKColor(107, 191, 58)), "Pick Up", null),
        ]
        :
        [
            (168, "circle", "Run", "(Hold)"),
            (233, "square", "Interact", null),
            (298, "cross", "Pick Up", null),
        ];

    // Slot order mirrors GameplayScreen.Update: X, Y, B, A. X/Y/B were the old HP/MP/SP potions, so
    // muscle memory survived the change to a bound bar; A was the button left over.
    object[] barFaces = xbox
        ? [("X", new SKColor(59, 124, 214)), ("Y", new SKColor(230, 185, 59)),
           ("B", new SKColor(214, 69, 59)), ("A", new SKColor(107, 191, 58))]
        : ["square", "triangle", "circle", "cross"];

    foreach (var (cy, btn, action, sub) in faces)
    {
        if (xbox) { var (letter, color) = ((string, SKColor))btn; XboxFace(c, BTN_CX, cy, FACE_R, letter, color); }
        else PsFace(c, BTN_CX, cy, FACE_R, (string)btn);
        if (sub is not null)
        {
            CLeft(c, ACTION_X, cy - 11, action, ACTION_F, ACTION_COLOR);
            CLeft(c, ACTION_X, cy + 13, sub, SUB_F, SUB_COLOR);
        }
        else CLeft(c, ACTION_X, cy, action, ACTION_F, ACTION_COLOR);
    }

    // Shoulders -> next / prev target, with both held together for self. Triggers are reserved for
    // the action-bar modifier, so they do not appear in the cycle-target group.
    // The box cannot grow: the Cast face button sits just above it and the action-bar divider just
    // below. So the PILLS shrink instead — at h=34 starting at y=372 the LB pill's top edge landed on
    // 355, inside the 2px border drawn across 354-356, and clipped it.
    GroupBox(c, 70, 354, 220, 466);
    var bumper = new SKColor(62, 62, 80);
    Pill(c, 145, 375, 90, 30, lb, bumper);
    Pill(c, 145, 411, 90, 30, rb, bumper);
    Pill(c, 110, 447, 50, 26, lb, bumper, 14);
    CMid(c, 145, 447, "+", PLUS_F, ACTION_COLOR);
    Pill(c, 180, 447, 50, 26, rb, bumper, 14);
    CLeft(c, ACTION_X, 375, "Next Target", ACTION_F, ACTION_COLOR);
    CLeft(c, ACTION_X, 411, "Prev Target", ACTION_F, ACTION_COLOR);
    CLeft(c, ACTION_X, 447, "Target Self", ACTION_F, ACTION_COLOR);

    BarGamepad(c, xbox, lt, rt, barFaces);
    c.Dispose();
    return bmp;
}

// ── Main ─────────────────────────────────────────────────────────────────────
string outDir = Path.Combine(MirageTools.RepoPaths.EngineRepo(),
    "client", "src", "Mirage.Client.Shell", "assets", "graphics");

void Save(SKBitmap bmp, string name)
{
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    string path = Path.Combine(outDir, name);
    File.WriteAllBytes(path, data.ToArray());
    Console.WriteLine($"  {name,-26} {data.Size / 1024,4} KB");
    bmp.Dispose();
}

Console.WriteLine($"Using font: {MONO_B.FamilyName}\n");
Save(BuildKeyboard(), "ControlsKeyboard.png");
Save(BuildGamepad(xbox: true), "ControlsXbox.png");
Save(BuildGamepad(xbox: false), "ControlsPlaystation.png");
Console.WriteLine($"\nwrote 3 files to {Path.GetRelativePath(MirageTools.RepoPaths.EngineRepo(), outDir)}");
return 0;
