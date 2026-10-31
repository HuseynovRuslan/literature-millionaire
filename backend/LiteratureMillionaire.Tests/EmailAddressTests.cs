using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class EmailAddressTests
{
    [Theory]
    [InlineData("ali@mail.az", "ali@mail.az")]
    [InlineData("  Ali.Veli@Gmail.COM ", "ali.veli@gmail.com")]
    [InlineData("a+quiz@sub.domain.az", "a+quiz@sub.domain.az")]
    public void Normalises_to_the_trimmed_lower_case_address(string input, string expected)
    {
        Assert.True(EmailAddress.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ali")]
    [InlineData("ali@gmail")]
    [InlineData("ali@gmail.")]
    [InlineData("@gmail.com")]
    [InlineData("Ali <ali@gmail.com>")]
    [InlineData("ali@gmail.com, veli@gmail.com")]
    public void Refuses_anything_that_is_not_one_bare_address(string? input)
    {
        Assert.False(EmailAddress.TryNormalize(input, out _));
    }

    [Fact]
    public void Masks_all_but_the_first_letter_and_the_domain()
    {
        Assert.Equal("l***@mail.az", EmailAddress.Mask("Leyla@Mail.az"));
        Assert.Equal("***", EmailAddress.Mask("not an address"));
    }
}
