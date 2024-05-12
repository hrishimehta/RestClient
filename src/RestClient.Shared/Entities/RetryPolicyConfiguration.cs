using System;

namespace RestClient.Shared.Entities
{
    public class RetryPolicyConfiguration
    {
        public int MaxRetries { get; set; }
        public string RetryType { get; set; } = String.Empty;
        public int RetryInterval { get; set; }
        public int? BackoffExponentialBase { get; set; }
        public FaultTolerancePolicy FaultTolerancePolicy { get; set; } = new FaultTolerancePolicy();
       
    }

    public class FaultTolerancePolicy
    {
        public bool Enabled { get; set; } = true;
        public double FailureThreshold { get; set; }
        public int BreakDurationSeconds { get; set; }
        public int SamplingDurationSeconds { get; set; }

        public List<int> OpenCircuitForHttpCodes { get; set; } = new();
        public List<string> OpenCircuitForExceptions { get; set; } = new();
        public JitterStrategy JitterStrategy { get; set; } = new JitterStrategy();
      
    }

    public class JitterStrategy
    {
        public bool Enabled { get; set; }
        public int Percentage { get; set; }
    }
}
