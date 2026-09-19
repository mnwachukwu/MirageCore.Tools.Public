# MirageCore.Tools.Public

**Generators for [Mirage Core](https://github.com/mnwachukwu/MirageCore).** Two that draw shipped image
assets as geometry rather than exporting them from a design file, and two that build the sample
world's records from the content that already describes it.

```
dotnet run --file ArtGenerators/gen-icons.cs
dotnet run --file ArtGenerators/gen-control-images.cs
dotnet run --file WorldBuilder/gen-msr-world.cs
dotnet run --file WorldBuilder/gen-msr-quests.cs
```

All of them write into the engine repository, which has to sit **beside this one**:

```
D:\Repos\
    MirageCore\              the engine
    MirageCore.Tools.Public\
```

`RepoPaths.EngineRepo()` finds it by name. ⚠ The name is what decides — Mirage Source Remastered
carries the same `shared/src/Mirage.Shared` layout, so a probe for the folder contents alone would not
tell the two apart, and pointing this at the wrong one overwrites the other product's shipped art.

## What is here, and what is not

| | |
|---|---|
| `ArtGenerators/` | the application icons and the control-scheme reference images |
| `WorldBuilder/` | the nineteen maps of the sample world, and the 54 quests that fill its towns |
| `RepoPaths/` | where the sibling checkouts are |

**The VB6 converter and the balance simulations are not here.** They act on Mirage Source
Remastered's own authored content rather than producing anything Core ships, and they live in that
project's tools repository.

`gen-msr-quests.cs` additionally reads a **MirageSourceRemastered** checkout beside this one, which
is where those 54 quests are authored. Without it that one program has nothing to convert and says
so; the other three do not need it.

⚠ **The two art generators have no dry run.** They write their output the moment they start. That is
safe — they only ever overwrite their own generated assets, in known locations — but it is the
opposite of the two world builders, which report and write nothing until `--apply`.

⚠ **`gen-msr-world.cs` rewrites maps 1-19 whole**, so anything done to them in the editor is lost.
Change its tables and regenerate, or author by hand; the two do not mix.
