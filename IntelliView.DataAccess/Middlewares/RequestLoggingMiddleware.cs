using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IntelliView.DataAccess.Middlewares
{
  public class RequestLoggingMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
      _next = next;
      _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
      // Start the timer
      var stopwatch = Stopwatch.StartNew();

      try
      {
        // Continue down the middleware pipeline
        await _next(context);
      }
      finally
      {
        // Record endpoint response time regardless of success/failure
        stopwatch.Stop();

        // Log in the required format
        _logger.LogInformation(
            $"[{DateTime.Now:HH:mm:ss} INF] HTTP {context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode} in {stopwatch.Elapsed.TotalMilliseconds:F4} ms");
      }
    }
  }

  // Extension method to make it easy to add the middleware to the pipeline
  public static class RequestLoggingMiddlewareExtensions
  {
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
      return app.UseMiddleware<RequestLoggingMiddleware>();
    }
  }
}