namespace PolarSharp.Tests.Monads;

public sealed class OptionTests
{
    [Fact]
    public void Some_HasValueTrue()
    {
        var opt = Option<int>.Some(42);
        Assert.True(opt.HasValue);
    }

    [Fact]
    public void None_HasValueFalse()
    {
        var opt = Option<int>.None;
        Assert.False(opt.HasValue);
    }

    [Fact]
    public void Map_OnSome_TransformsValue()
    {
        var result = Option<int>.Some(5).Map(x => x * 2).Match(v => v, () => -1);
        Assert.Equal(10, result);
    }

    [Fact]
    public void Map_OnNone_ReturnsNone()
    {
        var result = Option<int>.None.Map(x => x * 2).Match(v => v, () => -1);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Bind_OnSome_ChainsOption()
    {
        var result = Option<int>.Some(3)
            .Bind(x => x > 0 ? Option<string>.Some("pos") : Option<string>.None)
            .Match(v => v, () => "none");
        Assert.Equal("pos", result);
    }

    [Fact]
    public void Bind_OnSome_ReturnsNoneWhenBinderReturnsNone()
    {
        var result = Option<int>.Some(-1)
            .Bind(x => x > 0 ? Option<string>.Some("pos") : Option<string>.None)
            .Match(v => v, () => "none");
        Assert.Equal("none", result);
    }

    [Fact]
    public void Bind_OnNone_ShortCircuits()
    {
        var called = false;
        Option<int>.None.Bind(x => { called = true; return Option<int>.Some(x); });
        Assert.False(called);
    }

    [Fact]
    public void Match_OnSome_InvokesOnSome()
    {
        var result = Option<int>.Some(7).Match(v => $"some:{v}", () => "none");
        Assert.Equal("some:7", result);
    }

    [Fact]
    public void Match_OnNone_InvokesOnNone()
    {
        var result = Option<int>.None.Match(v => $"some:{v}", () => "none");
        Assert.Equal("none", result);
    }
}
