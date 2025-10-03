using System.Net;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Deck.Api.Middleware;

public class ExceptionMappingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (FluentValidation.ValidationException ve)
        {
            ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ctx.Response.WriteAsJsonAsync(new { error = "ValidationFailed", details = ve.Errors.Select(e => e.ErrorMessage) });
        }
        catch (KeyNotFoundException)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = "NotFound" });
        }
        catch (InvalidOperationException ioe) when (ioe.Message.Contains("ETag"))
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new { error = "ETagConflict" });
        }
        catch (DbUpdateConcurrencyException)
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new { error = "ConcurrencyConflict" });
        }
        catch (ArgumentException ae)
        {
            ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ctx.Response.WriteAsJsonAsync(new { error = "ValidationFailed", details = ae.Message });
        }
        catch (OperationCanceledException)
        {
            ctx.Response.StatusCode = 499; // StatusCodes.Status499 ClientClosedRequest 
            await ctx.Response.WriteAsJsonAsync(new { error = "ClientClosedRequest" });
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "InternalServerError" });
        }
    }
}
