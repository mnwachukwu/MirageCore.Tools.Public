#:project ../RepoPaths/RepoPaths.csproj
// Builds the nineteen maps of Mirage Source Remastered's sample world, and the four regions they sit in.
//
//   dotnet run --file gen-msr-world.cs                  what it would write, and what is stale
//   dotnet run --file gen-msr-world.cs -- --apply       write maps 1-19 and the four regions
//   dotnet run --file gen-msr-world.cs -- --apply --prune   and delete every map above 19
//
// ⚠ THIS OVERWRITES AUTHORED MAPS. Maps 1-19 are rewritten whole, so anything done to them in the
// editor is lost. Change the tables below and regenerate, or stop using this and author by hand —
// the two do not mix.
//
// ── What this is ────────────────────────────────────────────────────────────────────────────────
//
// The rest of MSR's content already exists and already agrees on three places: the 21 shops are seven
// per town, the three quest givers have eighteen quests each, and the 124 hostiles fill three level
// bands end to end with nothing outside one. The maps are what was missing, so they are derived from
// the content rather than invented beside it.
//
//   F0  Fenn's Landing          the crossroads, and the only map touching all three bands
//   L0-L5  The Reedmarsh        levels 1-20,    one town and five field maps
//   M0-M5  The Drowned Reach    levels 100-120, the same shape
//   X0-X5  The Cinderwaste      levels 235-255, the same shape
//
// Maps join at their EDGES rather than by warp, so the chain is a real walk: a town sits at the head
// of its band and every field map is the same distance from a bank and a repair. An edge with no
// neighbor is walled, which leaves the chain as the only way through.
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MirageTools;

bool apply = args.Contains("--apply");
bool prune = args.Contains("--prune");

string world = Path.Combine(RepoPaths.EngineRepo(), "modules", "msr", "world");
string mapsDir = Path.Combine(world, "maps");
string groupsDir = Path.Combine(world, "map_groups");

const int W = 16, H = 12;          // Constants.DefaultMapWidth/Height
const int MaxSlots = 20;           // Constants.MaxMapNpcs

// ── The art ─────────────────────────────────────────────────────────────────────────────────────
//
// Indices into assets/graphics/tiles/0_Tiles.bmp, which is 7 wide: index = row * 7 + column. A ground
// cell is a packed LayerCell, and sheet 0 with no animation packs to the bare tile number — so these
// are written as plain integers.
//
// ⚠ Zero is not usable. A LayerCell of 0 reads as "empty", so tile 0 and no tile are the same value.
var REEDMARSH = new Palette(
    Ground: [144, 151, 145],       // meadow green
    Rough: [185, 192, 178],        // the wet dark green of standing reed
    Wall: 141,                     // tree canopy
    Scatter: [146, 153, 188],      // bushes and a reed tuft
    Floor: 163,                    // packed sand, for the town
    Road: 163);

var DROWNED = new Palette(
    Ground: [373, 374, 371],       // wet flagstone
    Rough: [343, 344, 350],        // open water
    Wall: 372,                     // stone block
    Scatter: [360, 367],           // pale shallows
    Floor: 373,
    Road: 374);

var CINDERWASTE = new Palette(
    Ground: [467, 474, 468],       // burnt ground
    Rough: [477, 484, 478],        // ash drift
    Wall: 466,                     // slag
    Scatter: [362, 369],           // fire
    Floor: 474,
    Road: 467);

// ── The world ───────────────────────────────────────────────────────────────────────────────────
//
// One row per map, in map-number order, so map N is MAPS[N-1]. Links are written once here, from the
// map that owns the lower number, and the reverse link is filled in below — writing both by hand is
// how a one-way seam gets authored by accident.
Map M(int n, string key, string name, string display, int group, Palette art, bool town = false, int moral = 0)
    => new(n, key, name, display, group, art, town, moral);

Map[] MAPS =
[
    M(1,  "F0", "fennslanding",  "Fenn's Landing",      1, REEDMARSH,   town: true, moral: 1),

    M(2,  "L0", "fennsclearing", "Fenn's Clearing",     2, REEDMARSH,   town: true, moral: 1),
    M(3,  "L1", "reedshallows",  "The Reed Shallows",   2, REEDMARSH),
    M(4,  "L2", "poachershedge", "The Poacher's Hedge", 2, REEDMARSH),
    M(5,  "L3", "graveboundfen", "The Gravebound Fen",  2, REEDMARSH),
    M(6,  "L4", "kilnroad",      "The Kiln Road",       2, REEDMARSH),
    M(7,  "L5", "warlordsshore", "The Warlord's Shore", 2, REEDMARSH),

    M(8,  "M0", "drydeck",       "The Dry Deck",        3, DROWNED,     town: true, moral: 1),
    M(9,  "M1", "theshelf",      "The Shelf",           3, DROWNED),
    M(10, "M2", "oarsmenswatch", "The Oarsmen's Watch", 3, DROWNED),
    M(11, "M3", "undertowstair", "The Undertow Stair",  3, DROWNED),
    M(12, "M4", "coralwall",     "The Coral Wall",      3, DROWNED),
    M(13, "M5", "sunkencourt",   "The Sunken Court",    3, DROWNED),

    M(14, "X0", "coldforge",     "The Cold Forge",      4, CINDERWASTE, town: true, moral: 1),
    M(15, "X1", "thecut",        "The Cut",             4, CINDERWASTE),
    M(16, "X2", "theridge",      "The Ridge",           4, CINDERWASTE),
    M(17, "X3", "moltenline",    "The Molten Line",     4, CINDERWASTE),
    M(18, "X4", "theashfall",    "The Ashfall",         4, CINDERWASTE),
    M(19, "X5", "ashenthrone",   "The Ashen Throne",    4, CINDERWASTE),
];

// The crossroads keeps its three roads on three different edges, so a player standing on it can see
// all three and picks one rather than discovering the others later.
Link[] LINKS =
[
    new(1, "left", 2), new(1, "down", 8), new(1, "right", 14),

    new(2, "down", 3), new(3, "down", 4), new(4, "down", 5), new(5, "down", 6), new(6, "down", 7),
    new(8, "down", 9), new(9, "down", 10), new(10, "down", 11), new(11, "down", 12), new(12, "down", 13),
    new(14, "down", 15), new(15, "down", 16), new(16, "down", 17), new(17, "down", 18), new(18, "down", 19),
];

// ── Who stands where ────────────────────────────────────────────────────────────────────────────
//
// Towns are pinned: a vendor behind a counter and a guard at a gate are worth authoring, and a
// shopkeeper who wanders off is a shop the player has to hunt for. Field mobs are not — they spawn
// wherever the map has room, which is what makes a field map different from a street.
Town[] TOWNS =
[
    // F0 has no shops: three waymarkers, one by each road, and nothing else. Wick names the marsh,
    // Sounder the stair down to the Reach, Osk the road into the Cinderwaste.
    new(1, Vendors: [], Giver: 0, Guards: [], Ambient: [],
        Posted: [(175, 2), (176, 8), (177, 14)]),
    new(2, Vendors: [125, 127, 128, 129, 130, 131, 132], Giver: 126, Guards: [169, 170],
        Ambient: [149, 150, 151, 152, 153, 160, 161, 162]),
    new(8, Vendors: [133, 135, 136, 137, 138, 139, 140], Giver: 134, Guards: [171, 172],
        Ambient: [154, 155, 156, 163, 164, 165]),
    new(14, Vendors: [141, 143, 144, 145, 146, 147, 148], Giver: 142, Guards: [173, 174],
        Ambient: [157, 158, 159, 166, 167, 168]),
];

// Every one of the 124 hostiles, on the map whose tier holds its level. Read straight off the layout
// plan's tables, which derive membership from the level spans rather than assigning it by hand.
var FIELD = new Dictionary<int, int[]>
{
    [3]  = [1, 2, 21, 22, 48, 49, 23, 3, 50, 4, 24, 51],
    [4]  = [5, 25, 52, 6, 26, 7, 27, 8, 28, 9, 29],
    [5]  = [10, 30, 31, 113, 114, 11, 32, 12, 41, 13, 14, 33, 34, 42],
    [6]  = [15, 35, 16, 36, 17, 37, 44, 18, 38, 39, 43, 19, 20, 40, 45],
    [7]  = [46, 47, 115, 116],

    [9]  = [53, 54, 55, 56, 57, 59],
    [10] = [58, 60, 61, 62, 63, 64, 65],
    [11] = [117, 118, 66, 67, 68, 70, 69, 71, 72],
    [12] = [73, 74, 75, 77, 76, 78, 79, 80],
    [13] = [81, 82, 119, 120],

    [15] = [83, 84, 85, 86, 87, 88, 89],
    [16] = [90, 91, 92, 93, 94, 95, 96, 97, 122],
    [17] = [98, 121, 99, 100, 101, 102, 103, 104, 105],
    [18] = [106, 107, 108, 109, 110, 111, 124],
    [19] = [112, 123],
};

var REGIONS = new (int Num, string Name, string Display, int Boot)[]
{
    (1, "crossroads", "Fenn's Landing", 1),
    (2, "reedmarsh", "The Reedmarsh", 2),
    (3, "drownedreach", "The Drowned Reach", 8),
    (4, "cinderwaste", "The Cinderwaste", 14),
};

// ── Building one map ────────────────────────────────────────────────────────────────────────────

var rng = new Random(20260918);     // fixed, so regenerating an untouched table changes no file

// Where each edge's opening sits. Two tiles wide, centered, so a player walking into the edge finds
// it without hunting along the wall.
bool IsGap(string dir, int x, int y) => dir switch
{
    "up" => y == 0 && (x == W / 2 - 1 || x == W / 2),
    "down" => y == H - 1 && (x == W / 2 - 1 || x == W / 2),
    "left" => x == 0 && (y == H / 2 - 1 || y == H / 2),
    "right" => x == W - 1 && (y == H / 2 - 1 || y == H / 2),
    _ => false,
};

JsonObject BuildMap(Map m, Dictionary<string, int> links, List<(int Npc, int? X, int? Y, string? Dir)> npcs)
{
    var art = m.Art;
    var tiles = new JsonArray();

    for (int x = 0; x < W; x++)
    {
        var column = new JsonArray();
        for (int y = 0; y < H; y++)
        {
            bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
            bool opening = links.Keys.Any(d => IsGap(d, x, y));

            var cell = new JsonObject();
            if (edge && !opening)
            {
                // A walled edge. The chain is the only way off a map, so every other edge is solid.
                cell["type"] = "Blocked";
                cell["ground"] = new JsonArray(art.Wall);
            }
            else
            {
                int ground = m.Town || opening
                    ? art.Floor
                    : Pick(art, x, y);
                cell["ground"] = new JsonArray(ground);

                // Cover to fight around, kept out of the middle band so the walk through is never
                // blocked and a crowded map still has somewhere to stand.
                bool corridor = y >= H / 2 - 2 && y <= H / 2 + 1;
                if (!m.Town && !edge && !corridor && rng.Next(100) < 12)
                {
                    cell["type"] = "Blocked";
                    cell["ground"] = new JsonArray(ground, art.Scatter[rng.Next(art.Scatter.Length)]);
                }
            }

            column.Add(cell);
        }

        tiles.Add(column);
    }

    var o = new JsonObject
    {
        ["name"] = m.Name,
        ["displayName"] = m.Display,
        ["revision"] = 1,
        ["mapGroup"] = m.Group,
        ["music"] = 0,
        ["up"] = links.GetValueOrDefault("up"),
        ["down"] = links.GetValueOrDefault("down"),
        ["left"] = links.GetValueOrDefault("left"),
        ["right"] = links.GetValueOrDefault("right"),
        ["exitMap"] = 0,
        ["exitX"] = 0,
        ["exitY"] = 0,
        ["greetingSpeaker"] = "",
        ["joinSay"] = "",
        ["leaveSay"] = "",
        ["lights"] = new JsonArray(),
    };

    // MSR's own field on a map. Left off a field map so it takes its region's answer, which is open.
    if (m.Moral != 0) o["attributes"] = new JsonObject { ["moral"] = m.Moral };

    var entries = new JsonArray();
    foreach (var (npc, px, py, dir) in npcs)
    {
        var e = new JsonObject { ["npc"] = npc };
        if (px is not null && py is not null) { e["pinX"] = px; e["pinY"] = py; }
        if (dir is not null) e["pinDir"] = dir;
        entries.Add(e);
    }

    o["npcs"] = entries;
    o["tile"] = tiles;
    return o;
}

// Ground varies tile to tile so a field does not read as one flat color, and the rough band runs
// along the bottom of a map where the next one begins.
int Pick(Palette art, int x, int y)
{
    bool rough = y > H - 4 && rng.Next(100) < 55;
    var from = rough ? art.Rough : art.Ground;
    return from[(x * 3 + y * 5 + rng.Next(2)) % from.Length];
}

// Where an edge's road begins, one tile inside the wall, facing the way a player leaves by it.
(int X, int Y, string Face) RoadPost(string dir) => dir switch
{
    "up" => (W / 2, 2, "Up"),
    "down" => (W / 2, H - 3, "Down"),
    "left" => (2, H / 2, "Left"),
    _ => (W - 3, H / 2, "Right"),
};

// ── Where a town's people stand ─────────────────────────────────────────────────────────────────
//
// A row of vendors along the top wall with the giver in front of them, guards either side of the
// road out, and the rest of the cast down the sides where a player passes them on the way through.
//
// A town with no vendors is the crossroads, where the whole cast is one person per road: three
// exits, and somebody at each saying which one kills you. Posting them by the LINK rather than by
// coordinates keeps that true if the roads are ever rearranged above.
List<(int, int?, int?, string?)> PlaceTown(Town t, Dictionary<string, int> links)
{
    var placed = new List<(int, int?, int?, string?)>();

    if (t.Posts.Length > 0)
    {
        // ⚠ Each one is posted by the road to the band they talk about, found from the links rather
        // than written as a coordinate: a waymarker standing at the wrong road sends a level 1
        // character down the one that kills them, which is the single thing this map exists to stop.
        foreach (var (npc, leadsTo) in t.Posts)
        {
            string dir = links.First(l => l.Value == leadsTo).Key;
            var (px, py, face) = RoadPost(dir);
            placed.Add((npc, px, py, face));
        }

        return placed;
    }

    int x = 2;
    foreach (int vendor in t.Vendors)
    {
        placed.Add((vendor, x, 2, "Down"));
        x += 2;
    }

    if (t.Giver != 0) placed.Add((t.Giver, W / 2, 5, "Down"));

    // At the road out, facing the way a player leaves.
    var (gx, gy) = links.ContainsKey("down") ? (W / 2, H - 3)
                 : links.ContainsKey("left") ? (2, H / 2)
                 : (W - 3, H / 2);
    for (int i = 0; i < t.Guards.Length; i++)
        placed.Add((t.Guards[i], gx + (i == 0 ? -1 : 1), gy, "Down"));

    int ax = 2, ay = 7;
    foreach (int amb in t.Ambient)
    {
        placed.Add((amb, ax, ay, null));
        ax += 3;
        if (ax >= W - 2) { ax = 2; ay += 2; }
    }

    return placed;
}

// ── Writing ─────────────────────────────────────────────────────────────────────────────────────

var opts = new JsonSerializerOptions { WriteIndented = true };
var report = new StringBuilder();
int written = 0, overSlots = 0;

var byMap = MAPS.ToDictionary(m => m.Num);
var linksFor = MAPS.ToDictionary(m => m.Num, _ => new Dictionary<string, int>());
string Opposite(string d) => d switch { "up" => "down", "down" => "up", "left" => "right", _ => "left" };

foreach (var l in LINKS)
{
    linksFor[l.From][l.Dir] = l.To;
    linksFor[l.To][Opposite(l.Dir)] = l.From;
}

foreach (var m in MAPS)
{
    var npcs = new List<(int, int?, int?, string?)>();

    var town = TOWNS.FirstOrDefault(t => t.Map == m.Num);
    if (town is not null) npcs.AddRange(PlaceTown(town, linksFor[m.Num]));
    else if (FIELD.TryGetValue(m.Num, out var mobs))
        npcs.AddRange(mobs.Select(n => ((int)n, (int?)null, (int?)null, (string?)null)));

    if (npcs.Count > MaxSlots)
    {
        report.AppendLine($"  ⚠ {m.Key} {m.Display}: {npcs.Count} spawns, {MaxSlots} slots");
        overSlots++;
        npcs = npcs.Take(MaxSlots).ToList();
    }

    var json = BuildMap(m, linksFor[m.Num], npcs);
    string path = Path.Combine(mapsDir, $"map{m.Num}.json");
    if (apply) File.WriteAllText(path, json.ToJsonString(opts) + "\n");
    written++;

    string joins = string.Join(", ", linksFor[m.Num].OrderBy(k => k.Key)
        .Select(k => $"{k.Key} → {byMap[k.Value].Key}"));
    report.AppendLine($"  map{m.Num,-3} {m.Key}  {m.Display,-22} {npcs.Count,2} spawns   {joins}");
}

// ── Who stands still ────────────────────────────────────────────────────────────────────────────
//
// ⚠ A verb used on a creature reaches it by the SQUARE it was standing on, so a shopkeeper who takes
// a step between the right-click and the server answering is a shop that cannot be opened. It reads
// as an intermittent bug rather than as a wandering NPC, because the menu appears and then the verb
// does nothing.
//
// So anybody a player goes to ON PURPOSE holds their tile. Read off the shop records, the quest
// records, and the posts above rather than listed here: a keeper added to a shop, a giver moved to
// another townsperson, or a fourth road off the crossroads all stay right without anybody
// remembering this step.
int pinned = 0, alreadyStill = 0;
var standStill = new SortedSet<int>();

foreach (string shop in Directory.GetFiles(Path.Combine(world, "shops"), "shop*.json"))
    if ((int?)JsonNode.Parse(File.ReadAllText(shop))!["keeper"] is > 0 and var k) standStill.Add(k);

string questDir = Path.Combine(world, "quest");
if (Directory.Exists(questDir))
    foreach (string q in Directory.GetFiles(questDir, "quest*.json"))
        if ((int?)JsonNode.Parse(File.ReadAllText(q))!["giver"] is > 0 and var g) standStill.Add(g);

// A waymarker's whole job is being at the mouth of the road they name, so one that strolls off is a
// signpost in the wrong field — and the level 1 character it was put there for walks into the
// Cinderwaste instead.
foreach (var town in TOWNS)
    foreach (var (npc, _) in town.Posts) standStill.Add(npc);

foreach (int npc in standStill)
{
    string path = Path.Combine(world, "npcs", $"npc{npc}.json");
    if (!File.Exists(path)) { Console.Error.WriteLine($"  npc{npc} is named by a shop or a quest and does not exist"); continue; }

    var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    if ((string?)record["behavior"] == "Stationary") { alreadyStill++; continue; }

    record["behavior"] = "Stationary";
    if (apply) File.WriteAllText(path, record.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    pinned++;
}

foreach (var (num, name, display, boot) in REGIONS)
{
    var g = new JsonObject
    {
        ["name"] = name,
        ["displayName"] = display,
        ["music"] = 0,
        ["playersPassThrough"] = true,
        ["alwaysLit"] = true,
        ["exitMap"] = boot,
        ["exitX"] = W / 2,
        ["exitY"] = H / 2,
        ["greetingSpeaker"] = "",
        ["joinSay"] = "",
        ["leaveSay"] = "",
    };
    if (apply) File.WriteAllText(Path.Combine(groupsDir, $"map_group{num}.json"), g.ToJsonString(opts) + "\n");
}

var stale = Directory.GetFiles(mapsDir, "map*.json")
    .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f)[3..], out int n) && n > MAPS.Length)
    .OrderBy(f => f).ToList();

if (prune && apply) foreach (string f in stale) File.Delete(f);

Console.WriteLine(apply ? "WROTE" : "would write (pass --apply)");
Console.Write(report);
Console.WriteLine($"  {REGIONS.Length} regions, boot points at the towns");
Console.WriteLine($"  {standStill.Count} keepers, givers and waymarkers hold their tile "
                  + $"({pinned} changed, {alreadyStill} already still)");
Console.WriteLine();
Console.WriteLine($"{written} maps, {FIELD.Values.Sum(v => v.Length)} hostiles placed, "
                  + $"{TOWNS.Sum(t => t.Vendors.Length + t.Guards.Length + t.Ambient.Length + t.Posts.Length + (t.Giver == 0 ? 0 : 1))} townsfolk");
if (overSlots > 0) Console.WriteLine($"⚠ {overSlots} map(s) over the {MaxSlots}-slot cap, truncated");
if (stale.Count > 0)
    Console.WriteLine(prune && apply
        ? $"pruned {stale.Count} map(s) above {MAPS.Length}"
        : $"⚠ {stale.Count} map(s) above {MAPS.Length} are stale — pass --prune to delete them");

// ── The shapes the tables above are written in ──────────────────────────────────────────────────

/// <summary>The art one band is built from: its ground, the wetter or burnt version that runs along
/// a map's far edge, what its walls are made of, and what is scattered for cover.</summary>
record Palette(int[] Ground, int[] Rough, int Wall, int[] Scatter, int Floor, int Road);

/// <summary>One edge join, written once from the lower-numbered map.</summary>
record Link(int From, string Dir, int To);

record Map(int Num, string Key, string Name, string Display, int Group, Palette Art, bool Town, int Moral);

/// <summary>A town's cast. Vendors keep a shop, the giver holds all eighteen of the band's quests,
/// and the rest are there to be walked past.</summary>
record Town(int Map, int[] Vendors, int Giver, int[] Guards, int[] Ambient,
            (int Npc, int LeadsTo)[]? Posted = null)
{
    /// <summary>Who stands by a road, and which map that road leads to.</summary>
    public (int Npc, int LeadsTo)[] Posts => Posted ?? [];
}
