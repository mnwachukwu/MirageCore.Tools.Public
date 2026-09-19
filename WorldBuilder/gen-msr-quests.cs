#:project ../RepoPaths/RepoPaths.csproj
// Converts Mirage Source Remastered's 54 quests into the three families MSR's Compass scripts declare.
//
//   dotnet run --file gen-msr-quests.cs                what it would write
//   dotnet run --file gen-msr-quests.cs -- --apply     write quest/, questgoal/ and questgate/
//
// ── Where the quests come from ──────────────────────────────────────────────────────────────────
//
// The Mirage Source Remastered checkout, which has to sit beside this one. Its server carried quests
// as one record holding a list of objectives and a list of reward items; Core's record families hold
// fields and not lists, so one source quest becomes one Quest row plus a QuestGoal row per objective
// and a QuestGate row per class allowed to take it.
//
//   quest/      54 rows   name, giver, what it pays, how often it re-opens
//   questgoal/  54 rows   one per objective, all of them Kill
//   questgate/  60 rows   only for the 18 quests that name their classes
//
// ⚠ Read-only against MirageSourceRemastered. Nothing here writes to that checkout.
using System.Text.Json;
using System.Text.Json.Nodes;
using MirageTools;

bool apply = args.Contains("--apply");

string msr = Path.Combine(Path.GetDirectoryName(RepoPaths.EngineRepo())!, "MirageSourceRemastered");
string from = Path.Combine(msr, "server", "src", "Mirage.Server.Host", "world", "quests");
string world = Path.Combine(RepoPaths.EngineRepo(), "modules", "msr", "world");

if (!Directory.Exists(from))
{
    Console.Error.WriteLine($"no quests at {from} — MirageSourceRemastered has to be checked out beside the engine");
    return 1;
}

// How often a finished quest re-opens. The script's own numbering, from declarations/quests.cm:
// nought is once and for all, and the rest name the period that has to pass.
int Cadence(string? name) => name switch
{
    "Daily" => 1,
    "Weekly" => 2,
    "Monthly" => 3,
    "Seasonal" or "Seasonally" => 4,
    _ => 0,
};

var quests = new List<(int Num, JsonObject Row)>();
var goals = new List<JsonObject>();
var gates = new List<JsonObject>();
int longestDescription = 0, repeats = 0, chained = 0;

foreach (string path in Directory.GetFiles(from, "quest*.json")
             .OrderBy(p => int.Parse(Path.GetFileNameWithoutExtension(p)[5..])))
{
    int num = int.Parse(Path.GetFileNameWithoutExtension(path)[5..]);
    var q = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    string description = (string?)q["description"] ?? "";
    longestDescription = Math.Max(longestDescription, description.Length);

    var rewards = q["rewardItems"]?.AsArray();
    var repeated = q["repeatRewardItems"]?.AsArray();
    if (repeated is { Count: > 0 }) repeats++;
    if ((int?)q["prereqQuest"] is > 0) chained++;

    // The source carries a list and this holds one, which is every case in the data: no quest pays
    // two different items. A second would be dropped silently, so it is worth saying out loud.
    if (rewards is { Count: > 1 }) Console.Error.WriteLine($"  quest{num}: {rewards.Count} reward items, only the first is kept");

    quests.Add((num, new JsonObject
    {
        ["name"] = (string?)q["name"] ?? "",
        ["description"] = description,
        ["giver"] = (int?)q["giverNpc"] ?? 0,
        // Nought, in every one of the 54. The script reads that as "back to whoever gave it", which
        // with one giver per town is the whole of the rule.
        ["turnIn"] = (int?)q["turnInNpc"] ?? 0,
        ["reqLevel"] = (int?)q["reqLevel"] ?? 0,
        ["reqStr"] = (int?)q["reqStr"] ?? 0,
        ["reqDef"] = (int?)q["reqDef"] ?? 0,
        ["reqSpd"] = (int?)q["reqSpd"] ?? 0,
        ["reqInt"] = (int?)q["reqInt"] ?? 0,
        ["prereq"] = (int?)q["prereqQuest"] ?? 0,
        ["rewardExp"] = (int?)q["rewardExp"] ?? 0,
        ["rewardItem"] = rewards is { Count: > 0 } ? (int?)rewards[0]!["itemNum"] ?? 0 : 0,
        ["rewardMany"] = rewards is { Count: > 0 } ? (int?)rewards[0]!["quantity"] ?? 0 : 0,
        ["cadence"] = Cadence((string?)q["cadence"]),
        ["repeatExp"] = (int?)q["repeatRewardExp"] ?? 0,
        ["repeatItem"] = repeated is { Count: > 0 } ? (int?)repeated[0]!["itemNum"] ?? 0 : 0,
        ["repeatMany"] = repeated is { Count: > 0 } ? (int?)repeated[0]!["quantity"] ?? 0 : 0,
    }));

    foreach (var o in q["objectives"]?.AsArray() ?? [])
    {
        string kind = (string?)o!["kind"] ?? "";
        if (kind != "Kill")
        {
            // A goal nothing advances leaves a quest unfinishable, so it is refused rather than written.
            Console.Error.WriteLine($"  quest{num}: '{kind}' goal skipped — only Kill is counted");
            continue;
        }

        goals.Add(new JsonObject
        {
            ["forQuest"] = num,
            ["quarry"] = (int?)o["target"] ?? 0,
            ["many"] = (int?)o["count"] ?? 1,
        });
    }

    // No row at all means every class may take it, so the 36 ungated quests write nothing here.
    foreach (var c in q["allowedClasses"]?.AsArray() ?? [])
        gates.Add(new JsonObject { ["forQuest"] = num, ["forClass"] = (int?)c ?? 0 });
}

var opts = new JsonSerializerOptions { WriteIndented = true };

void WriteFamily(string folder, IEnumerable<(int Num, JsonObject Row)> rows)
{
    string dir = Path.Combine(world, folder);
    if (!apply) return;

    Directory.CreateDirectory(dir);
    foreach (var (num, row) in rows)
        File.WriteAllText(Path.Combine(dir, $"{folder}{num}.json"), row.ToJsonString(opts) + "\n");
}

WriteFamily("quest", quests);
WriteFamily("questgoal", goals.Select((g, i) => (i + 1, g)));
WriteFamily("questgate", gates.Select((g, i) => (i + 1, g)));

Console.WriteLine(apply ? "WROTE" : "would write (pass --apply)");
Console.WriteLine($"  quest/      {quests.Count,3} rows   {chained} in a chain, {repeats} paying a repeat reward");
Console.WriteLine($"  questgoal/  {goals.Count,3} rows");
Console.WriteLine($"  questgate/  {gates.Count,3} rows   {gates.Select(g => (int)g["forQuest"]!).Distinct().Count()} quests gated by class");
Console.WriteLine();

var byGiver = quests.GroupBy(q => (int)q.Row["giver"]!).OrderBy(g => g.Key);
foreach (var g in byGiver) Console.WriteLine($"  NPC {g.Key}: {g.Count()} quests");

var byCadence = quests.GroupBy(q => (int)q.Row["cadence"]!).OrderBy(g => g.Key);
Console.WriteLine("  re-opens: " + string.Join(", ", byCadence.Select(g =>
    $"{g.Count()} {g.Key switch { 1 => "daily", 2 => "weekly", 3 => "monthly", 4 => "seasonally", _ => "never" }}")));

// The declared ceiling is what an editor truncates a field to, so prose longer than it survives on
// disk and is cut the first time somebody opens that record and saves.
Console.WriteLine($"  longest description: {longestDescription} characters");
return 0;
