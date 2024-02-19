namespace RestClient.Shared.Entities
{
    public class RetryPolicyConfiguration
    {
        public int MaxRetries { get; set; }
        public string RetryType { get; set; }
        public int RetryInterval { get; set; }
        public int? BackoffExponentialBase { get; set; }
    }
}
