using System.Text;
using Microsoft.Extensions.Logging;

namespace CosmosFailureForcing;

public static class LoggingExtensions
{
    public static void LogRequest(this ILogger logger, HttpRequestMessage request)
    {
        logger.LogInformation($"REQUEST: {request.Method} {request.RequestUri}");
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var debugMsg = new StringBuilder();
            foreach (var header in request.Headers)
            {
                foreach (var value in header.Value)
                {
                    debugMsg.AppendLine($"{header.Key}: {value}");
                }
            }

            logger.LogDebug(debugMsg.ToString());
        }
    }

    public static void LogResponse(this ILogger logger, HttpResponseMessage response)
    {
        logger.LogInformation($"RESPONSE: {response.RequestMessage!.Method} {response.RequestMessage!.RequestUri} {(int)response.StatusCode} {response.ReasonPhrase}");
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var debugMsg = new StringBuilder();

            foreach (var header in response.Headers)
            {
                foreach (var value in header.Value)
                {
                    debugMsg.AppendLine($"{header.Key}: {value}");
                }
            }

            logger.LogDebug(debugMsg.ToString());
        }
    }
}
