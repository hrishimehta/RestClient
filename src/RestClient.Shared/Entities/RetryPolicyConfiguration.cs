using System;

namespace RestClient.Shared.Entities
{
    public class RetryPolicyConfiguration
    {
        public string Name { get; set; }

        public RetryPolicy Retry { get; set; } = new RetryPolicy();
        public FaultTolerancePolicy FaultTolerancePolicy { get; set; } = new FaultTolerancePolicy();

        public int Timeout { get; set; } = 100;

        /// <summary>
        ///  Configuration for chaos policies
        /// </summary>
        public List<ChaosPolicyConfiguration> ChaosPolicies { get; set; }
    }

    public class RetryPolicy
    {
        public int MaxRetries { get; set; }
        public string RetryType { get; set; } = "Constant";
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

    public class ChaosPolicyConfiguration
    {
        public string Type { get; set; }
        public double InjectionRate { get; set; }
        public string Fault { get; set; }
        public int LatencySeconds { get; set; } = 0;
        public int? StatusCode { get; set; }

        public Exception GetException()
        {
            try
            {
                Type exceptionType = System.Type.GetType(this.Fault);

                if (!typeof(Exception).IsAssignableFrom(exceptionType))
                {
                    throw new InvalidCastException($"Invalid exception type{this.Fault}");
                }

                var instance = Activator.CreateInstance(exceptionType);
                var result = instance as Exception;

                return result;
            }
            catch
            {
                throw;
            }
        }
    }
}
