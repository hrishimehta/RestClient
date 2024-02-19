namespace RestClient.Shared.Entities
{
    public class SystemRetryConfiguration
    {
        public string BaseUrl { get; set; }
        public RetryPolicyConfiguration RetryPolicy { get; set; }
        public int? BackoffExponentialBase { get; set; }
    }
}
