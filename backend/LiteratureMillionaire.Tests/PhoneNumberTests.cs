using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0501234567", "+994501234567")]
    [InlineData("994501234567", "+994501234567")]
    [InlineData("+994501234567", "+994501234567")]
    [InlineData("+994 50 123 45 67", "+994501234567")]
    [InlineData("050-123-45-67", "+994501234567")]
    [InlineData("(050) 123 45 67", "+994501234567")]
    [InlineData("0101234567", "+994101234567")]
    [InlineData("0991234567", "+994991234567")]
    public void AcceptedFormats_NormalizeToPlus994(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("501234567")]        // no leading 0 / country code
    [InlineData("05012345")]         // too short
    [InlineData("05012345678")]      // too long
    [InlineData("0121234567")]       // landline prefix
    [InlineData("+7501234567")]      // other country
    [InlineData("+99450123456a")]    // letters
    [InlineData("0501234567; DROP")] // junk
    public void RejectedInputs_ReturnFalse(string input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }
}
