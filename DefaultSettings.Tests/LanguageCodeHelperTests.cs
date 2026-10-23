using Jellyfin.Plugin.DefaultSettings.Services;
using Xunit;

namespace DefaultSettings.Tests;

public class LanguageCodeHelperTests
{
    [Theory]
    [InlineData("de", "deu")]
    [InlineData("ger", "deu")]
    [InlineData("deu", "deu")]
    [InlineData("German", "deu")]
    [InlineData("deutsch", "deu")]
    [InlineData("ja", "jpn")]
    [InlineData("jpn", "jpn")]
    [InlineData("en", "eng")]
    [InlineData("eng", "eng")]
    public void Normalize_ReturnsCanonicalCode(string input, string expected)
    {
        Assert.Equal(expected, LanguageCodeHelper.Normalize(input));
    }

    [Theory]
    [InlineData("de", "deu", true)]
    [InlineData("ger", "deu", true)]
    [InlineData("german", "de", true)]
    [InlineData("ja", "jpn", true)]
    [InlineData("en", "eng", true)]
    [InlineData("deu", "eng", false)]
    [InlineData("jpn", "deu", false)]
    public void AreEquivalent_CorrectlyComparesCodes(string lang1, string lang2, bool expected)
    {
        Assert.Equal(expected, LanguageCodeHelper.AreEquivalent(lang1, lang2));
    }
}
