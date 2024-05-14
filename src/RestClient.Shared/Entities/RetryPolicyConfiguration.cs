using System;

namespace RestClient.Shared.Entities
{
    public class RetryPolicyConfiguration
    {
        public RetryPolicy Retry { get; set; } = new RetryPolicy();
        public FaultTolerancePolicy FaultTolerancePolicy { get; set; } = new FaultTolerancePolicy();

        public Timeout Timeout { get; set; } = new Timeout();

    }

    public class RetryPolicy
    {
        public int MaxRetries { get; set; }
        public string RetryType { get; set; } = "Constant";
        public int RetryInterval { get; set; }

        public List<int> RetryForHttpCodes { get; set; } = new();
        public List<string> RetryForExceptions { get; set; } = new();

        public bool UseJitter { get; set; } = false;
    }

    public class FaultTolerancePolicy
    {
        public bool Enabled { get; set; } = true;
        public double FailureThreshold { get; set; }
        public int BreakDurationSeconds { get; set; }
        public int SamplingDurationSeconds { get; set; }

        public int MinThroughPut { get; set; } = 5;

        public List<int> OpenCircuitForHttpCodes { get; set; } = new();
        public List<string> OpenCircuitForExceptions { get; set; } = new();
    }
    public class Timeout
    {
        public int TimeoutDuration { get; set; } = 100;
    }

}
