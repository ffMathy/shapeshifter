// Screenshots as proof, and the shape every check takes: one test per scenario, named exactly as the scenario
// (its DisplayName), run in the order they are written, one at a time.
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestMethodOrderer(typeof(Shapeshifter.Checks.InWrittenOrder))]
[assembly: TestCollectionOrderer(typeof(Shapeshifter.Checks.InWrittenOrder))]

namespace Shapeshifter.Checks;

/// <summary>
/// A check's scenarios. Once a scenario has run (passed or not), the screen kept as its proof, or else the screen
/// it ended on, is saved as <c>AIL_SHOTS_DIR/&lt;scenario as a slug&gt;.png</c>, which <c>ail adopt</c> collects;
/// then <see cref="AfterEach"/> runs.
/// </summary>
public abstract class Scenario : IAsyncLifetime
{
    public virtual ValueTask InitializeAsync()
    {
        Screen.Forget();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Screen.SaveProof(TestContext.Current.Test?.TestDisplayName ?? GetType().Name);
        await AfterEach();
        GC.SuppressFinalize(this);
    }

    /// <summary>What every scenario of the check ends with, after its proof is saved (bun's afterEach).</summary>
    protected virtual Task AfterEach() => Task.CompletedTask;
}

/// <summary>Scenarios in the order the check's file lists them, as the spec does (methods keep their declaration order in metadata).</summary>
public sealed class InWrittenOrder : ITestMethodOrderer, ITestCollectionOrderer
{
    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods) where TTestMethod : notnull, ITestMethod =>
        [.. testMethods.OrderBy(m => m is IXunitTestMethod x ? x.Method.MetadataToken : int.MaxValue)];

    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection> testCollections) where TTestCollection : ITestCollection =>
        [.. testCollections.OrderBy(c => c.TestCollectionDisplayName, StringComparer.Ordinal)];
}

public static class Screen
{
    static byte[]? kept;
    static Task? pending;

    /// <summary>The file name <c>ail adopt</c> expects for a scenario's screenshot (the same rule as AIL's own slugOf).</summary>
    public static string ShotName(string test)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(test.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 60) slug = slug[..60];
        return (slug.Length > 0 ? slug : "test") + ".png";
    }

    /// <summary>The whole screen of an X display, as PNG bytes; empty when it cannot be taken.</summary>
    public static byte[] Of(string? display = null)
    {
        var (exit, png) = Desktop.RunBytes("import", ["-window", "root", "png:-"], new Dictionary<string, string> { ["DISPLAY"] = display ?? Desktop.Display });
        return exit == 0 ? png : [];
    }

    /// <summary>
    /// Keeps the screen as it is now as the scenario's proof, in place of the screen it ends on. The checks' helpers
    /// call this at the moment they look at something that is gone by the end, such as the open picker or a
    /// notification; the last one kept wins.
    /// </summary>
    public static void Keep(string? display = null)
    {
        var png = Of(display);
        if (png.Length > 0) kept = png;
    }

    /// <summary>Like <see cref="Keep"/>, but without waiting for the screenshot: for helpers that time what is on screen.</summary>
    public static void KeepSoon(string? display = null)
    {
        var at = display ?? Desktop.Display;
        pending = Task.Run(() => Keep(at));
    }

    /// <summary>For a scenario played on a desktop of its own: keeps that desktop's screen before it closes, unless one was kept.</summary>
    public static void KeepIfNone(string display)
    {
        if (kept is null) Keep(display);
    }

    internal static void Forget()
    {
        kept = null;
        pending = null;
    }

    internal static async Task SaveProof(string test)
    {
        if (pending is not null) await pending;
        var dir = Environment.GetEnvironmentVariable("AIL_SHOTS_DIR");
        if (!string.IsNullOrEmpty(dir))
        {
            var png = kept ?? Of();
            if (png.Length > 0) await File.WriteAllBytesAsync(Path.Combine(dir, ShotName(test)), png);
        }
        Forget();
    }
}
