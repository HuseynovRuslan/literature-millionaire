using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class LeaderboardNameMaskerTests
{
    [Theory]
    [InlineData("Ayşə Məmmədova", "Ayşə M.")]
    [InlineData("Ruslan Hüseynov", "Ruslan H.")]
    [InlineData("Əli Vəli oğlu Şükürlü", "Əli Ş.")]
    [InlineData("Ruslan", "R***")]
    [InlineData("   Ayşə    Məmmədova   ", "Ayşə M.")]
    [InlineData("\tÇingiz\nƏliyev\r", "Çingiz Ə.")]
    [InlineData("", "***")]
    public void Name_is_normalized_and_masked_without_breaking_Azerbaijani_letters(string input, string expected) =>
        Assert.Equal(expected, LeaderboardNameMasker.Mask(input));
}
