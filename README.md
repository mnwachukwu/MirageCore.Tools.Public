# MirageCore.Tools.Public

**Art generators for [Mirage Core](https://github.com/mnwachukwu/MirageCore).** Two programs that draw shipped image assets as geometry
rather than exporting them from a design file.

```
dotnet run --file ArtGenerators/gen-icons.cs
dotnet run --file ArtGenerators/gen-control-images.cs
```

Both write into the engine repository, which has to sit **beside this one**:

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
| `RepoPaths/` | where the sibling checkouts are |

**The world tooling is not here.** The VB6 converter, the content generators, and the balance
simulations write Mirage Source Remastered's own world, and they live in that project's tools
repository. Core has no bundled world to generate.

⚠ **Neither generator has a dry run.** They write their output the moment they start. That is safe —
they only ever overwrite their own generated assets, in known locations — but it is the opposite of
the content generators' default next door, so do not assume nothing happened without `--apply`.
