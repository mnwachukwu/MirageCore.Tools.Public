# RepoPaths

A shared library that answers one question for both generators here: **where is the engine
checkout?**

```csharp
#:project ../RepoPaths/RepoPaths.csproj

string icons = Path.Combine(MirageTools.RepoPaths.EngineRepo(), "assets", "icons");
```

| Call | Returns |
|---|---|
| `EngineRepo()` | the sibling `MirageCore` checkout |
| `ToolsRepo()` | this repository |

## Why it exists at all

**`AppContext.BaseDirectory` cannot do this job.** These are file-based apps — `dotnet run --file x.cs`
— and the SDK builds them into `%TEMP%\dotnet\runfile\`, so BaseDirectory points nowhere near the
source. `[CallerFilePath]` is baked in by the compiler at the CALL SITE, which is the only thing that
locates a single-file script reliably, and the reason one shared library can serve both generators
instead of the same resolver pasted into each.

## The layout it assumes

Both repositories must sit under the same parent directory:

```
<parent>/
  MirageCore/         the engine
  MirageCore.Tools/   this repo
```

`EngineRepo()` walks up from the calling file looking for a sibling `MirageCore` that really contains
`shared/src/Mirage.Shared`. It is depth-independent, so moving a tool a folder deeper does not break
it, and anywhere else it throws with the layout spelled out rather than resolving to nothing.

⚠ **The folder NAME is what decides.** Mirage Source Remastered carries the same
`shared/src/Mirage.Shared` layout, so the probe cannot tell the two apart on contents alone. Pointed at
that one, every generator here would overwrite the other product’s shipped art.
