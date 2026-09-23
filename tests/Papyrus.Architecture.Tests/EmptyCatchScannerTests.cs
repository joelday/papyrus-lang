using Xunit;

namespace Papyrus.Architecture.Tests;

public sealed class EmptyCatchScannerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("return;")]
    [InlineData("return null;")]
    [InlineData("return null!;")]
    [InlineData("return default;")]
    [InlineData("return default!;")]
    [InlineData("return default(int);")]
    [InlineData("return default(int)!;")]
    public void Swallowing_bodies_are_flagged(string body)
        => Assert.True(EmptyCatchScanner.IsSwallowingBody(body));

    [Theory]
    [InlineData("return defaultValue;")]
    [InlineData("return defaultBinding;")]
    [InlineData("return defaultType;")]
    [InlineData("return defaultValue!;")]
    [InlineData("throw;")]
    public void Recovery_returns_are_not_flagged(string body)
        => Assert.False(EmptyCatchScanner.IsSwallowingBody(body));

    [Fact]
    public void Catch_when_without_type_is_flagged()
    {
        const string source = """
            class C
            {
                void M()
                {
                    try { }
                    catch when (true) { }
                }
            }
            """;

        var hits = EmptyCatchScanner.ScanText(source);
        Assert.NotEmpty(hits);
    }

    [Fact]
    public void Planted_return_defaultValue_does_not_fail()
    {
        const string source = """
            class C
            {
                object M(object defaultValue)
                {
                    try { return defaultValue; }
                    catch { return defaultValue; }
                }
            }
            """;

        Assert.Empty(EmptyCatchScanner.ScanText(source));
    }
}
