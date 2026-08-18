namespace BIMFlow.Shared;

public static class BimFlowConfig
{
    /// <summary>
    /// Base URL of the BIMFlow web app's API. Override with the BIMFLOW_API_URL
    /// environment variable to point at a local dev server while testing.
    /// </summary>
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("BIMFLOW_API_URL") ?? "https://bimflow.app";
}
