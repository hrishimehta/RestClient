# HttpClientRetryExtensions

HttpClientRetryExtensions is a set of extension methods for configuring HttpClient instances with retry policies using the Polly library.

## Usage

### AddHttpClientWithRetryPolicy

The `AddHttpClientWithRetryPolicy` extension method allows you to configure an HttpClient with a retry policy based on the specified system name.

```csharp
// In Startup.cs or any service configuration file

services.AddHttpClientWithRetryPolicy("System1");
```

## Usage with http client

In your `System1Service.cs` or wherever the `HttpClient` is injected:

```csharp
// In System1Service.cs or wherever the HttpClient is injected

public class System1Service
{
    private readonly IHttpClientFactory _httpClientFactory;

    public System1Service(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<System1Response> GetSystem1DataAsync()
    {
        var httpClient = _httpClientFactory.CreateClient("System1"); //same as appsettings.json
        var response = await httpClient.GetAsync("/api/system1/data");

        // Handle the response as needed

        return response;
    }
}
```
## Retry Policy Configuration

The retry policy is configured in your app settings JSON file under the system name. Here's an example configuration:

### Exponential Retry Type

```json
{
  "System1": {
    "BaseUrl": "https://system1.example.com",
    "RetryPolicy": {
      "MaxRetries": 3,
      "RetryType": "Exponential",
      "BackoffExponentialBase": 2
    }
  }
}
```

## Example Scenario

Suppose your system encounters a transient failure during an HTTP request to [https://system1.example.com](https://system1.example.com). With the provided configuration:

- **MaxRetries**: 3 (Maximum of 3 retry attempts)
- **RetryType**: "Exponential" (Using exponential retry strategy)
- **BackoffExponentialBase**: 2 (Base for exponential backoff calculation)

### Retry Attempts:

1. **Attempt 1**: The system makes the initial request and encounters a failure. It will wait for 2 seconds before the first retry.
2. **Attempt 2**: If the second attempt fails, it will wait for 4 seconds before the next retry.
3. **Attempt 3**: If the third attempt fails, it will wait for 8 seconds before the final retry.

This exponential backoff strategy increases the wait time between each retry exponentially, providing a spaced-out approach to retrying the operation. Adjust the parameters based on your specific requirements and scenarios.


## Constant Retry Type

Suppose your system encounters a transient failure during an HTTP request to [https://system2.example.com](https://system2.example.com). With the provided configuration for Constant Retry Type:

```json
{
  "System2": {
    "BaseUrl": "https://system2.example.com",
    "RetryPolicy": {
      "MaxRetries": 3,
      "RetryType": "Constant",
      "RetryInterval": 1
    }
  }
}
```

## Example Scenario

Suppose your system encounters a transient failure during an HTTP request to [https://system2.example.com](https://system2.example.com). With the provided configuration:

- **MaxRetries**: 3 (Maximum of 3 retry attempts)
- **RetryType**: "Constant" (Using constant retry strategy)
- **RetryInterval**: 1 (Fixed time interval between retries)

### Retry Attempts:

1. **Attempt 1**: The system makes the initial request and encounters a failure. It will wait for 1 second before the first retry.
2. **Attempt 2**: If the second attempt fails, it will wait for 1 second before the next retry.
3. **Attempt 3**: If the third attempt fails, it will wait for 1 second before the final retry.

This constant retry strategy maintains a fixed time interval between each retry, providing a consistent approach to retrying the operation. Adjust the parameters based on your specific requirements and scenarios.

## Circuit Breaker Policy

The circuit breaker policy is configured under the `RetryPolicy` section for each system in your app settings JSON file. Below is an example configuration for a system using the circuit breaker policy:

### Example Configuration

```json
{
  "System3": {
    "BaseUrl": "https://system3.example.com",
    "RetryPolicy": {
      "MaxRetries": 3,
      "RetryType": "Exponential",
      "BackoffExponentialBase": 2,
      "CircuitBreakerPolicy": {
        "FailureThreshold": 5,
        "BreakDurationSeconds": 30,
        "SamplingDurationSeconds": 60,
        "ResetCountOnSuccess": true,
        "TimeoutForHalfOpenSeconds": 10,
        "OpenCircuitForHttpCodes": [500, 503],
        "OpenCircuitForExceptions": ["System.Net.Http.HttpRequestException"],
        "JitterStrategy": {
          "Enabled": true,
          "Percentage": 10,
          "MaxMilliseconds": 5000
        }
      }
    }
  }
}
```
## Circuit Breaker Policy Configuration Details

### FailureThreshold

The `FailureThreshold` represents the number of consecutive failures that need to occur to trigger the circuit breaker. For example, if set to `5`, the circuit breaker will open after encountering 5 consecutive failures.

### BreakDurationSeconds

The `BreakDurationSeconds` specifies the duration in seconds for which the circuit breaker will stay open once triggered. During this time, any requests to the system will fail fast without attempting to execute the actual operation.

### SamplingDurationSeconds

The `SamplingDurationSeconds` defines the duration in seconds over which failure occurrences are counted. This duration is crucial for evaluating whether the failure threshold is met.

### ResetCountOnSuccess

The `ResetCountOnSuccess` property indicates whether the failure count should reset on a successful operation. If set to `true`, a successful operation will reset the consecutive failure count.

### TimeoutForHalfOpenSeconds

The `TimeoutForHalfOpenSeconds` determines the duration in seconds during which the circuit breaker transitions to the half-open state. In the half-open state, a limited number of requests are allowed to pass through to test system recovery.

### OpenCircuitForHttpCodes

The `OpenCircuitForHttpCodes` is an array of HTTP status codes that, when encountered, will open the circuit breaker. For example, `[500, 503]` specifies that the circuit should open for HTTP status codes 500 and 503.

### OpenCircuitForExceptions

The `OpenCircuitForExceptions` is an array of exception types that, when encountered, will open the circuit breaker. For example, `["System.Net.Http.HttpRequestException"]` specifies that the circuit should open for the specified exception type.

### JitterStrategy

The `JitterStrategy` configuration allows introducing jitter in retry intervals to avoid synchronization of requests. Jitter adds randomness to the wait time between retries.

- **Enabled**: Indicates whether jitter is enabled. If set to `true`, jitter will be applied.

- **Percentage**: The `Percentage` property represents the percentage of jitter to apply. For example, if set to `10`, the jitter will be within 10% of the original wait time.

- **MaxMilliseconds**: The `MaxMilliseconds` property specifies the maximum milliseconds for jitter. If set to `5000`, the maximum added jitter will be 5000 milliseconds.

### Example Scenario

Suppose your system encounters transient failures during HTTP requests to [https://system3.example.com](https://system3.example.com). With the provided configuration:

- **FailureThreshold**: `5` (Circuit breaks after 5 consecutive failures)
- **BreakDurationSeconds**: `30` (Circuit remains open for 30 seconds once triggered)
- **SamplingDurationSeconds**: `60` (Failure occurrences are counted over 60 seconds)
- **ResetCountOnSuccess**: `true` (Failure count resets on a successful operation)
- **TimeoutForHalfOpenSeconds**: `10` (Duration during which the circuit transitions to the half-open state)
- **OpenCircuitForHttpCodes**: `[500, 503]` (Circuit opens for HTTP status codes 500 and 503)
- **OpenCircuitForExceptions**: `["System.Net.Http.HttpRequestException"]` (Circuit opens for the specified exception type)
- **JitterStrategy**:
  - **Enabled**: `true` (Jitter is enabled)
  - **Percentage**: `10` (10% jitter applied)
  - **MaxMilliseconds**: `5000` (Maximum 5000 milliseconds for jitter)

Adjust the parameters based on your specific requirements and scenarios.

## Retry Attempts:
### Attempt 1 (Parallel):

**Request 1 and Request 2:**
- The system makes the initial request to [https://system3.example.com](https://system3.example.com), and both requests encounter a failure.
- **Circuit Status**: Closed (initial state)
- **Configuration Read** (for both parallel requests):
  - FailureThreshold: `5`
  - BreakDurationSeconds: `30`
  - SamplingDurationSeconds: `60`
  - ResetCountOnSuccess: `true`
  - TimeoutForHalfOpenSeconds: `10`
  - OpenCircuitForHttpCodes: `[500, 503]`
  - OpenCircuitForExceptions: `["System.Net.Http.HttpRequestException"]`
  - JitterStrategy:
    - Enabled: `true`
    - Percentage: `10`
    - MaxMilliseconds: `5000`

- Both Request 1 and Request 2 wait for a predefined time with jitter before retrying.
- Since the response is a failure, the failure count increments for both parallel requests.

### Attempt 2 (Parallel):

**Request 1 and Request 2:**
- Both parallel requests attempt a retry based on the configuration.
- **Circuit Status**: Closed (if previous attempts didn't reach the FailureThreshold)
- **Configuration Read** (same as Attempt 1)

- Both parallel requests wait for a time with jitter before retrying. If the response fails again, the failure count increments for both requests.

### Attempt 3 (Parallel):

**Request 1:**
- Request 1 attempts another retry.
- **Circuit Status**: Closed (if previous attempts didn't reach the FailureThreshold)
- **Configuration Read** (same as Attempt 1)

- Request 1 waits for a time with jitter before retrying. If the response fails again, the failure count increments for Request 1.

**Request 2:**
- Request 2 does not encounter a failure during Attempt 3.
- The failure count for Request 2 does not increment.

- Since the cumulative failure count across both parallel requests (Request 1 and Request 2) reaches the `FailureThreshold` of 5, the circuit opens.

- **Circuit Status**: Open
- Subsequent attempts during the `BreakDurationSeconds` will fail fast without attempting to execute the actual operation.

### Circuit Open Duration:

- The circuit remains open for the configured `BreakDurationSeconds` (e.g., 30 seconds).

### After Circuit Breaks:

**New Request:**
- After the circuit breaks (i.e., after `BreakDurationSeconds`), a new request is made to [https://system3.example.com](https://system3.example.com).

- **Circuit Status**: Half-Open (limited requests allowed to test system recovery)
- The system evaluates the health by allowing a limited number of requests to pass through.

- If the new request passes (succeeds):
  - **Circuit Status**: Closed
  - The failure count resets on a successful operation.
  - Subsequent requests are processed as usual.

- If the new request fails:
  - **Circuit Status**: Open
  - The circuit remains open, and the failure count increments.

I hope this provides a clear understanding. Feel free to adjust the parameters or ask if you have further questions!

# Propose feature/enhancement
## Retry Features:

~~1. **Customizable Backoff Strategies:**~~
   - Allow users to customize the backoff strategies for exponential or constant retry intervals.

~~2. **Jitter for Exponential Backoff:**~~
   - Introduce jitter to the exponential backoff strategy to prevent synchronization of retries.

3. **Retry Condition Configuration:**
   - Allow users to configure specific conditions under which retries should be attempted (e.g., specific HTTP status codes).

4. **Timeouts:**
   - Implement timeouts for individual requests to limit the time spent on each retry attempt.

5. **Retry Events and Logging:**
   - Emit events or logs for each retry attempt, providing visibility into the retry process for debugging and monitoring.

## Circuit Breaker Features:

~~1. **Threshold Configuration:**~~
   - Allow users to configure thresholds for opening and closing the circuit breaker based on failure rates or errors.

2. **Time-Based Circuit Reset:**
   - Implement a time-based circuit reset mechanism to periodically attempt to close the circuit breaker and allow traffic.
3. **Half-Open State:**
   - Introduce a half-open state to test if the system has recovered before fully closing the circuit.
4. **Fallback Mechanism:**
   - Provide a fallback mechanism to handle requests when the circuit is open, preventing total service unavailability.
5. **Circuit State Events and Logging:**
   - Emit events or logs for transitions between circuit states (open, closed, half-open) to monitor and analyze circuit behavior.
6. **Dynamic Configuration:**
   - Allow dynamic adjustment of circuit breaker configurations without requiring application restart.
7. **Health Checks:**
   - Integrate health checks to assess the overall health of the system and inform circuit breaker decisions.
8. **Concurrency Limiting:**
   - Include features for limiting the number of concurrent requests when the circuit is in a partially open state.

By combining retry and circuit breaker strategies, you can significantly improve the robustness and fault tolerance of your distributed systems. Consider these features based on the specific requirements and challenges of your application.


