using System.Runtime.CompilerServices;

namespace MirageTools;

/// <summary>
/// Where the sibling repositories are, worked out rather than hardcoded to one machine’s drive letter.
///
/// <para><b><see cref="CallerFilePathAttribute"/>, not <c>AppContext.BaseDirectory</c>.</b> These are
/// file-based apps (<c>dotnet run --file x.cs</c>) and the SDK builds them into
/// <c>%TEMP%\dotnet\runfile\</c>, so BaseDirectory points nowhere near the source. CallerFilePath is
/// baked in by the compiler at the CALL SITE, which is the only reliable way a single-file script can
/// find where it lives — and the reason this can be one shared library rather than the same ten lines
/// pasted into every generator.</para>
/// </summary>
public static class RepoPaths
{
    /// <summary>The sibling engine checkout, found by walking up from the CALLING FILE for a directory
    /// with a <c>MirageCore</c> beside it. Depth-independent on purpose, so moving a generator a folder
    /// deeper cannot break it.</summary>
    ///
    /// <remarks>⚠ <c>MirageCore</c>, not <c>MirageSourceRemastered</c>. Both repositories carry a
    /// <c>shared/src/Mirage.Shared</c>, so the probe below cannot tell them apart and the folder NAME is
    /// what decides. Pointed at the wrong one, every generator here would quietly overwrite the other
    /// product’s shipped art.</remarks>
    public static string EngineRepo([CallerFilePath] string callerFile = "")
    {
        for (string? dir = Path.GetDirectoryName(callerFile); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string? parent = Path.GetDirectoryName(dir);
            if (parent is null) break;
            string candidate = Path.Combine(parent, "MirageCore");
            // Probe for a folder that only a real checkout has, not merely the name — a stray empty
            // directory would otherwise send every generator somewhere harmless-looking and wrong.
            if (Directory.Exists(Path.Combine(candidate, "shared", "src", "Mirage.Shared")))
                return candidate;
        }
        throw new DirectoryNotFoundException(
            $"No sibling MirageCore checkout found above '{callerFile}'. " +
            "The two repositories must sit in the same parent directory — see the tools README.");
    }

    /// <summary>This tools repository, found the same way.</summary>
    public static string ToolsRepo([CallerFilePath] string callerFile = "")
    {
        for (string? dir = Path.GetDirectoryName(callerFile); dir is not null; dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, "ArtGenerators")) && File.Exists(Path.Combine(dir, "README.md")))
                return dir;
        throw new DirectoryNotFoundException($"Could not locate the tools repository root above '{callerFile}'.");
    }
}
