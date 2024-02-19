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
      "BackoffExponentialBase": 2,
      "RetryInterval": 1
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

# Propose feature/enhancement
## Retry Features:

1. **Customizable Backoff Strategies:**
   - Allow users to customize the backoff strategies for exponential or constant retry intervals.

2. **Jitter for Exponential Backoff:**
   - Introduce jitter to the exponential backoff strategy to prevent synchronization of retries.

3. **Retry Condition Configuration:**
   - Allow users to configure specific conditions under which retries should be attempted (e.g., specific HTTP status codes).

4. **Timeouts:**
   - Implement timeouts for individual requests to limit the time spent on each retry attempt.

5. **Retry Events and Logging:**
   - Emit events or logs for each retry attempt, providing visibility into the retry process for debugging and monitoring.

## Circuit Breaker Features:

1. **Threshold Configuration:**
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


