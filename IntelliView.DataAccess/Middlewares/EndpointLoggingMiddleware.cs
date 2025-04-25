using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace IntelliView.DataAccess.Middlewares
{
  public class EndpointLoggingMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly ILogger<EndpointLoggingMiddleware> _logger;
    private readonly HashSet<string> _endpointsToLog;

    public EndpointLoggingMiddleware(RequestDelegate next, ILogger<EndpointLoggingMiddleware> logger, IEnumerable<string> endpointsToLog)
    {
      _next = next;
      _logger = logger;
      _endpointsToLog = new HashSet<string>(endpointsToLog, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
      // Check if this endpoint should be logged
      string path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

      // If no endpoints are specified or this endpoint is in the list to log
      if (_endpointsToLog.Count == 0 || _endpointsToLog.Any(endpoint => path.Contains(endpoint.ToLowerInvariant())))
      {
        var stopwatch = Stopwatch.StartNew();

        try
        {
          // Continue down the middleware pipeline
          await _next(context);
        }
        finally
        {
          // Record and log the request information
          stopwatch.Stop();
          var elapsed = stopwatch.Elapsed.TotalMilliseconds;

          // Format the log in the required format
          _logger.LogInformation(
              $"[{DateTime.Now:HH:mm:ss} INF] HTTP {context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode} in {elapsed:F4} ms");
        }
      }
      else
      {
        // Just continue down the pipeline without logging
        await _next(context);
      }
    }
  }

  // Extension method to make it easy to add the middleware to the pipeline
  public static class EndpointLoggingMiddlewareExtensions
  {
    public static IApplicationBuilder UseEndpointLogging(
        this IApplicationBuilder app,
        params string[] endpointsToLog)
    {
      return app.UseMiddleware<EndpointLoggingMiddleware>(
          endpointsToLog as IEnumerable<string>);
    }
  }
}