using System;

namespace RestClient.Shared.Entities
{
    public class RetryPolicyConfiguration
    {
        public int MaxRetries { get; set; }
        public string RetryType { get; set; }
        public int RetryInterval { get; set; }
        public int? BackoffExponentialBase { get; set; }
        public FaultTolerancePolicy FaultTolerancePolicy { get; set; }
        public JitterStrategy JitterStrategy { get; set; }
    }

    public class FaultTolerancePolicy
    {
        public bool Enabled { get; set; }
        public double FailureThreshold { get; set; }
        public int BreakDurationSeconds { get; set; }
        public int SamplingDurationSeconds { get; set; }
        
        public int[] OpenCircuitForHttpCodes { get; set; }
        public string[] OpenCircuitForExceptions { get; set; }
        public JitterStrategy JitterStrategy { get; set; }

        public FaultTolerancePolicy()
        {
            this.Enabled = true;
        }
    }

    public class JitterStrategy
    {
        public bool Enabled { get; set; }
        public int Percentage { get; set; }
    }
}
