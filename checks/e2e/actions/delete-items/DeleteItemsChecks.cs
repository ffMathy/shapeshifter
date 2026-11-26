// ail-derived-from: DeleteItems@1ac33b2bee96303c
// End-to-end check for DeleteItems: Delete removes the chosen item from the history while the picker is up. It
// reads the item count on screen (OCR) and what arrives in another app.
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class DeleteItemsChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    [Fact(DisplayName = "Delete removes the chosen item", Timeout = 40_000)]
    public async Task DeleteRemovesTheChosenItem()
    {
        var id = ReadableId();
        foreach (var t in new[] { "keep", "drop", "top" }) await App.Copy($"{t} {id}");
        await App.Prepare();
        await OpenPicker(1);
        var before = await ItemCountOnScreen();
        await Tap("Delete");
        await Sleep(400);
        Assert.Equal(before - 1, await ItemCountOnScreen());
        Assert.DoesNotContain($"drop {id}", ReadPicker("history"));
        await Tap("Escape");
        await KeyUp("ctrl");
        await Sleep(300);
        await App.Prepare();
        await OpenPicker(1);
        await KeyUp("ctrl");
        Assert.Equal($"keep {id}", await App.WaitForText($"keep {id}"));
    }

    [Fact(DisplayName = "after Delete the next item is chosen, and letting go of Ctrl pastes it", Timeout = 40_000)]
    public async Task AfterDeleteTheNextIsChosen()
    {
        var id = ReadableId();
        foreach (var t in new[] { "first", "second", "third" }) await App.Copy($"{t} {id}");
        await App.Prepare();
        await OpenPicker(1);
        await Tap("Delete");
        await KeyUp("ctrl");
        Assert.False(await WaitForPicker(false));
        Assert.Equal($"first {id}", await App.WaitForText($"first {id}"));
    }
}
