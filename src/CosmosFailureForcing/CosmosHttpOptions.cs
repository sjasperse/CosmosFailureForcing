namespace CosmosFailureForcing;

public class CosmosHttpOptions
{
    public bool UseTestingHandler { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public IEnumerable<RequestPolicy> RequestPolicies { get; set; } = new List<RequestPolicy>();

    public class RequestPolicy
    {
        public string? Path { get; set; }
        public bool IsQuery { get; set; }
        public bool IsUpsert { get; set; }
        public string? Requests { get; set; }

        public bool RespondWith429 { get; set; }
        public bool Timeout { get; set; }
    }
}


