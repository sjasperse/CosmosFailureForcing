using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CosmosFailureForcing;

/// <summary>
/// This class uses policies defined in CosmosHttpOptions to cause fake responses to come back from cosmos.
/// Primarily, this is for testing failure paths around 429s and timeouts.
/// </summary>
class CosmosTestingHttpHandler : HttpClientHandler
{
    private readonly ILogger logger;
    private IEnumerable<RequestPolicy> requestPolicies;

    public CosmosTestingHttpHandler(ILogger<CosmosTestingHttpHandler> logger, CosmosHttpOptions cosmosHttpOptions)
    {
        this.logger = logger;
        this.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        var requestPolicyNumber = 0;
        this.requestPolicies =
            cosmosHttpOptions.RequestPolicies
            .Select(x =>
            {
                requestPolicyNumber++;
                if (string.IsNullOrEmpty(x.Path)) throw new InvalidOperationException($"Request policy #{requestPolicyNumber} must not have an empty path");
                return new RequestPolicy(
                        Path: x.Path,
                        IsQuery: x.IsQuery,
                        IsUpsert: x.IsUpsert,
                        RequestsPattern: x.Requests, 
                        RespondWith429: x.RespondWith429, 
                        Timeout: x.Timeout);
            })
            .ToArray();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        logger.LogRequest(request);

        var isQuery = request.Headers.Any(x => x.Key == "x-ms-documentdb-isquery" && x.Value.Contains(bool.TrueString));
        var isUpsert = request.Headers.Any(x => x.Key == "x-ms-documentdb-is-upsert" && x.Value.Contains(bool.TrueString));

        var policies =
            requestPolicies
            // filter down to which policies match
            .Where(x => x.Path == path)
            .Where(x => !x.IsQuery || isQuery)
            .Where(x => !x.IsUpsert || isUpsert)
            // increment and track count in a thread-friendly manner
            .Select(policy => (Policy: policy, Count: Interlocked.Increment(ref policy.Count)))
            // filter by the request number
            .Where(FilterByRequestCount)
            .Select(x => x.Policy)
            .ToArray();

        var fakeResponse = (HttpResponseMessage?)null;
        foreach (var policy in policies)
        {
            if (policy.RespondWith429)
            {
                fakeResponse = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests)
                {
                    RequestMessage = request,
                };
                fakeResponse.Headers.Add("x-ms-retry-after-ms", "1"); // keeps retries quick
            }
            if (policy.Timeout)
            {
                logger.LogDebug($"Causing request timout.");
                await Task.Delay(-1, cancellationToken);
            }
        }

        var response = fakeResponse ?? await base.SendAsync(request, cancellationToken);

        logger.LogResponse(response);

        return response;
    }

    private bool FilterByRequestCount((RequestPolicy Policy, int Count) policyCount)
    {
        if (string.IsNullOrEmpty(policyCount.Policy.RequestsPattern)) return true;

        var requestNumber = policyCount.Count;

        var ranges = policyCount.Policy.RequestsPattern.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var range in ranges)
        {
            // individual number
            if (range == requestNumber.ToString()) return true;

            // range
            var rangeMatch = Regex.Match(range, @"(\d+)-(\d+)");
            if (rangeMatch.Success)
            {
                var start = int.Parse(rangeMatch.Groups[1].Value);
                var end = int.Parse(rangeMatch.Groups[2].Value);

                if (requestNumber >= start && requestNumber <= end) return true;
            }

            // percent
            var percentMatch = Regex.Match(range, @"([\d\.?]+)\%");
            if (percentMatch.Success)
            {
                var pcnt = decimal.Parse(percentMatch.Groups[1].Value);
                var rnd = Random.Shared.Next(100);

                if (rnd < pcnt) return true;
            }
        }

        return false;
    }

    record RequestPolicy(
        string Path, 
        bool IsQuery,
        bool IsUpsert,
        string? RequestsPattern, 
        bool RespondWith429, 
        bool Timeout)
    {
        public int Count;
        
        public override string ToString()
            => $"{Path}{(IsQuery ? " Query" : "")}{(IsUpsert ? " Upsert" : "")}";
    }
}
