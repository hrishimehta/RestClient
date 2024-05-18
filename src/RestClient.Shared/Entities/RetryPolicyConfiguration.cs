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
        /// Configuration for fault injection chaos.
        /// </summary>
        public FaultChaosConfig FaultChaos { get; set; }

        /// <summary>
        /// Configuration for latency injection chaos.
        /// </summary>
        public LatencyChaosConfig LatencyChaos { get; set; }

        /// <summary>
        /// Configuration for outcome injection chaos.
        /// </summary>
        public OutcomeChaosConfig OutcomeChaos { get; set; }

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

    /// <summary>
    /// Represents the base class for chaos configurations with an injection rate.
    /// </summary>
    public abstract class ChaosConfigBase
    {
        /// <summary>
        /// The rate at which the chaos injection is applied.
        /// </summary>
        public double InjectionRate { get; set; }

        /// <summary>
        /// Validates the injection rate to be between 0 and 1 inclusive.
        /// </summary>
        /// <param name="injectionRate">The injection rate to validate.</param>
        protected void ValidateInjectionRate(double injectionRate)
        {
            if (injectionRate < 0 || injectionRate > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(injectionRate), "InjectionRate must be between 0 and 1 inclusive.");
            }
        }

        /// <summary>
        /// Validates the current configuration.
        /// </summary>
        public void Validate()
        {
            ValidateInjectionRate(InjectionRate);
        }
    }

    /// <summary>
    /// Configuration for fault injection chaos.
    /// </summary>
    public class FaultChaosConfig : ChaosConfigBase
    {
        /// <summary>
        /// The type of fault to inject.
        /// </summary>
        public string Fault { get; set; }

        public Exception GetException()
        {
            try
            {
                Type exceptionType = Type.GetType(this.Fault);


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

    /// <summary>
    /// Configuration for latency injection chaos.
    /// </summary>
    public class LatencyChaosConfig : ChaosConfigBase
    {
        /// <summary>
        /// The amount of latency (in seconds) to inject.
        /// </summary>
        public int LatencySeconds { get; set; }
    }

    /// <summary>
    /// Configuration for outcome injection chaos.
    /// </summary>
    public class OutcomeChaosConfig : ChaosConfigBase
    {
        /// <summary>
        /// The status code to return as the outcome.
        /// </summary>
        public int StatusCode { get; set; }
    }

}
