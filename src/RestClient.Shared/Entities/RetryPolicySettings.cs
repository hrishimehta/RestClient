namespace RestClient.Shared.Entities
{
    public class RetryPolicySettings
    {
        public string Name { get; set; }
        public RetryPolicyConfiguration Policy { get; set; }
    }
}
