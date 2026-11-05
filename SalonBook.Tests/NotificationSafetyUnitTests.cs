using SalonBook.Api.Services;
using Xunit;

namespace SalonBook.Tests;

public class NotificationSafetyUnitTests
{
    [Fact]
    public void Truncate_TruncatesStringsExceedingMaxLength()
    {
        var longString = new string('A', 2500);
        var truncated = NotificationService.Truncate(longString, 2000);

        Assert.Equal(2000, truncated.Length);
        Assert.Equal(new string('A', 2000), truncated);
    }

    [Fact]
    public void Truncate_PreservesStringsWithinMaxLength()
    {
        var shortString = "Short Subject";
        var truncated = NotificationService.Truncate(shortString, 200);

        Assert.Equal(shortString, truncated);
    }

    [Fact]
    public void Truncate_HandlesNullAndEmptySafely()
    {
        Assert.Equal(string.Empty, NotificationService.Truncate(null, 200));
        Assert.Equal(string.Empty, NotificationService.Truncate(string.Empty, 200));
    }

    [Fact]
    public void TruncateNullable_HandlesNullAndLongStringsSafely()
    {
        Assert.Null(NotificationService.TruncateNullable(null, 1000));
        Assert.Null(NotificationService.TruncateNullable(string.Empty, 1000));

        var longError = new string('E', 1500);
        var truncatedError = NotificationService.TruncateNullable(longError, 1000);

        Assert.NotNull(truncatedError);
        Assert.Equal(1000, truncatedError.Length);
    }
}
