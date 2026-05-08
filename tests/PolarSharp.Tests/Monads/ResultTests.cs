namespace PolarSharp.Tests.Monads;

public sealed class ResultTests
{
    // ── Construction ─────────────────────────────────────────────────────────────

    [Fact]
    public void Success_SetsIsSuccessTrue()
    {
        var r = Result<int, string>.Success(42);
        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
    }

    [Fact]
    public void Failure_SetsIsFailureTrue()
    {
        var r = Result<int, string>.Failure("oops");
        Assert.False(r.IsSuccess);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Success_ThrowsOnNullValue()
        => Assert.Throws<ArgumentNullException>(
            () => Result<string, int>.Success(null!));

    [Fact]
    public void Failure_ThrowsOnNullError()
        => Assert.Throws<ArgumentNullException>(
            () => Result<int, string>.Failure(null!));

    // ── Map ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var r = Result<int, string>.Success(3).Map(x => x * 2);
        Assert.True(r.IsSuccess);
        var value = r.Match(v => v, _ => -1);
        Assert.Equal(6, value);
    }

    [Fact]
    public void Map_OnFailure_PassesThroughError()
    {
        var r = Result<int, string>.Failure("err").Map(x => x * 2);
        Assert.True(r.IsFailure);
        var error = r.Match(_ => "no", e => e);
        Assert.Equal("err", error);
    }

    [Fact]
    public void Map_ThrowsOnNullMapper()
        => Assert.Throws<ArgumentNullException>(
            () => Result<int, string>.Success(1).Map<int>(null!));

    // ── Bind ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_OnSuccess_ChainsOperation()
    {
        var r = Result<int, string>.Success(5)
            .Bind(x => x > 0
                ? Result<string, string>.Success($"pos:{x}")
                : Result<string, string>.Failure("non-pos"));

        var value = r.Match(v => v, _ => "fail");
        Assert.Equal("pos:5", value);
    }

    [Fact]
    public void Bind_OnSuccess_ShortCircuitsOnInnerFailure()
    {
        var r = Result<int, string>.Success(-1)
            .Bind(x => x > 0
                ? Result<string, string>.Success("pos")
                : Result<string, string>.Failure("non-pos"));

        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Bind_OnFailure_ShortCircuits()
    {
        var called = false;
        var r = Result<int, string>.Failure("err")
            .Bind(x => { called = true; return Result<int, string>.Success(x); });

        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    // ── BindAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BindAsync_OnSuccess_ChainsOperation()
    {
        var r = await Result<int, string>.Success(10)
            .BindAsync(x => Task.FromResult(Result<int, string>.Success(x + 1)));

        var value = r.Match(v => v, _ => -1);
        Assert.Equal(11, value);
    }

    [Fact]
    public async Task BindAsync_OnFailure_ShortCircuits()
    {
        var called = false;
        var r = await Result<int, string>.Failure("err")
            .BindAsync(x => { called = true; return Task.FromResult(Result<int, string>.Success(x)); });

        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    // ── Match ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_OnSuccess_InvokesOnSuccess()
    {
        var result = Result<int, string>.Success(7)
            .Match(v => $"ok:{v}", e => $"err:{e}");
        Assert.Equal("ok:7", result);
    }

    [Fact]
    public void Match_OnFailure_InvokesOnFailure()
    {
        var result = Result<int, string>.Failure("bad")
            .Match(v => $"ok:{v}", e => $"err:{e}");
        Assert.Equal("err:bad", result);
    }

    // ── MatchAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_OnSuccess_InvokesOnSuccess()
    {
        var result = await Result<int, string>.Success(3)
            .MatchAsync(
                v => Task.FromResult($"ok:{v}"),
                e => Task.FromResult($"err:{e}"));
        Assert.Equal("ok:3", result);
    }

    [Fact]
    public async Task MatchAsync_OnFailure_InvokesOnFailure()
    {
        var result = await Result<int, string>.Failure("x")
            .MatchAsync(
                v => Task.FromResult($"ok:{v}"),
                e => Task.FromResult($"err:{e}"));
        Assert.Equal("err:x", result);
    }

    // ── ToString ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_OnSuccess_ContainsValue()
        => Assert.Contains("42", Result<int, string>.Success(42).ToString());

    [Fact]
    public void ToString_OnFailure_ContainsError()
        => Assert.Contains("err", Result<int, string>.Failure("err").ToString());
}
