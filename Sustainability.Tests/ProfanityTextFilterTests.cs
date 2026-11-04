using ProfanityService.Services;

namespace Sustainability.Tests;

public class ProfanityTextFilterTests
{
    [Fact]
    public void Filter_replaces_each_whole_word_in_one_pass()
    {
        var result = ProfanityTextFilter.Filter(
            "  Damn, this is hell. Damnation is allowed.  ",
            ["damn", "hell"]);

        Assert.Equal("****, this is ****. damnation is allowed.", result.FilteredText);
        Assert.Equal(["damn", "hell"], result.MatchedWords);
    }

    [Fact]
    public void Filter_reports_repeated_matches_once()
    {
        var result = ProfanityTextFilter.Filter("crap crap", ["crap"]);

        Assert.Equal("**** ****", result.FilteredText);
        Assert.Equal(["crap"], result.MatchedWords);
    }

    [Fact]
    public void Filter_preserves_multi_word_entries()
    {
        var result = ProfanityTextFilter.Filter(
            "This contains very bad language.",
            ["very bad"]);

        Assert.Equal("this contains ******** language.", result.FilteredText);
        Assert.Equal(["very bad"], result.MatchedWords);
    }
}
