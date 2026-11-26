// ail-derived-from: Capture@60164061625cee49
// End-to-end check for Capture: its scenarios, played on an X11 desktop against the running app. Copies are made by
// other apps (the target app and xclip); what the history holds is seen by picking from it with the paste gesture
// and reading what lands in the text box.
using System.Text;
using static Shapeshifter.Checks.CaptureDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class CaptureChecks(CaptureAppFixture target) : Scenario, IClassFixture<CaptureAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); await WaitForNoNotification(); }

    [Fact(DisplayName = "each copy joins the history, newest first", Timeout = 30_000)]
    public async Task EachCopyJoins()
    {
        var id = Unique();
        await App.Copy($"older {id}");
        await App.Copy($"newer {id}");
        Assert.Equal($"older {id}", await PasteFromHistory(App, "v"));
    }

    [Fact(DisplayName = "copying something again moves it to the top instead of adding it twice", Timeout = 40_000)]
    public async Task CopyingAgainMovesToTop()
    {
        var id = Unique();
        await App.Copy($"before {id}");
        await App.Copy($"again {id}");
        await App.Copy($"between {id}");
        await App.Copy($"again {id}");
        await WatchNotifications(1500); // a tip may show here; the tips checks need to know
        await WaitForNoNotification();
        Assert.Equal($"again {id}", await PasteFromHistory(App));
        Assert.Equal($"between {id}", await PasteFromHistory(App, "v"));
        // Added twice, the second item down would be the first copy of "again".
        Assert.Equal($"before {id}", await PasteFromHistory(App, "v", "v"));
    }

    [Fact(DisplayName = "a password manager's secret is not recorded", Timeout = 30_000)]
    public async Task SecretIsNotRecorded()
    {
        var id = Unique();
        await App.Copy($"plain {id}");
        await WaitForNoNotification();
        await App.Send($"copy-secret {Convert.ToBase64String(Encoding.UTF8.GetBytes($"secret {id}"))}");
        var seen = await WatchNotifications(1500);
        Assert.Empty(seen);
        // The newest item in the history is still the plain copy.
        Assert.Equal($"plain {id}", await PasteFromHistory(App, "v", "Up"));
    }

    [Fact(DisplayName = "an app that writes the clipboard several times at once adds one item", Timeout = 30_000)]
    public async Task SeveralWritesAddOneItem()
    {
        var id = Unique();
        await App.Copy($"steady {id}");
        await XCopy($"draft {id}", "UTF8_STRING", 0);
        await XCopy($"final {id}", "UTF8_STRING", 900);
        Assert.Equal($"final {id}", await PasteFromHistory(App));
        Assert.Equal($"steady {id}", await PasteFromHistory(App, "v"));
    }

    [Fact(DisplayName = "pasting from the history adds nothing to it", Timeout = 30_000)]
    public async Task PastingAddsNothing()
    {
        var id = Unique();
        await App.Copy($"picked {id}");
        await App.Copy($"last {id}");
        await WaitForNoNotification();
        await App.Prepare();
        var paste = PasteFromHistory(App, "v");
        var seen = WatchNotifications(4000);
        Assert.Equal($"picked {id}", await paste);
        Assert.Empty(await seen);
        Assert.Null(Notification());
        // The pasted item moved to the top; nothing new came in above it.
        Assert.Equal($"last {id}", await PasteFromHistory(App, "v"));
        await Sleep(100);
    }
}
