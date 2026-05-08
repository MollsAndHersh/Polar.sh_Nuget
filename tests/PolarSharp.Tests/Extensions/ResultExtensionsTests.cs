using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PolarSharp.Extensions;

namespace PolarSharp.Tests.Extensions;

public sealed class ResultExtensionsTests
{
    // ── ToHttpResult<T> ───────────────────────────────────────────────────────────

    [Fact]
    public void ToHttpResult_OnSuccess_InvokesOnSuccess()
    {
        var result = Result<int, PolarError>.Success(42)
            .ToHttpResult(v => Results.Ok(v));

        Assert.IsType<Ok<int>>(result);
    }

    [Fact]
    public void ToHttpResult_OnFailure_MapsToError()
    {
        var result = Result<int, PolarError>
            .Failure(new NotFoundError("not found", "req-1"))
            .ToHttpResult(v => Results.Ok(v));

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public void ToHttpResult_ThrowsOnNullCallback()
    {
        var r = Result<int, PolarError>.Success(1);
        Assert.Throws<ArgumentNullException>(() => r.ToHttpResult(null!));
    }

    // ── PolarError.ToHttpResult ───────────────────────────────────────────────────

    [Fact]
    public void NotFoundError_MapsTo404()
    {
        IResult result = new NotFoundError("missing", "r1").ToHttpResult();
        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public void AuthenticationError_MapsTo401()
    {
        IResult result = new AuthenticationError("no auth", "r2").ToHttpResult();
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public void AuthorizationError_MapsTo403()
    {
        IResult result = new AuthorizationError("forbidden", "r3").ToHttpResult();
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public void ValidationError_MapsTo422()
    {
        var fields = new List<FieldValidationError>
        {
            new("email", "must be valid email"),
        };
        IResult result = new ValidationError("invalid", "r4", fields).ToHttpResult();
        var statusCode = result as IStatusCodeHttpResult;
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, statusCode?.StatusCode);
    }

    [Fact]
    public void RateLimitError_MapsTo429()
    {
        IResult result = new RateLimitError("throttled", "r5", TimeSpan.FromSeconds(30))
            .ToHttpResult();
        var statusCode = result as IStatusCodeHttpResult;
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusCode?.StatusCode);
    }

    [Fact]
    public void ServerError_MapsTo502()
    {
        IResult result = new ServerError("upstream failed", "r6").ToHttpResult();
        var statusCode = result as IStatusCodeHttpResult;
        Assert.Equal(StatusCodes.Status502BadGateway, statusCode?.StatusCode);
    }

    [Fact]
    public void ToHttpResult_ThrowsOnNullError()
    {
        PolarError? error = null;
        Assert.Throws<ArgumentNullException>(() => error!.ToHttpResult());
    }
}
