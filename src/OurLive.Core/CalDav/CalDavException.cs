namespace OurLive.Core.CalDav;

/// <summary>A CalDAV request failed (non-success status, or a required property was missing from the response).</summary>
public sealed class CalDavException(string message, System.Net.HttpStatusCode? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The response status that caused this failure, when the failure came from an HTTP response
    /// (e.g. 412 Precondition Failed on a conditional PUT/DELETE) rather than a missing response header.</summary>
    public System.Net.HttpStatusCode? StatusCode { get; } = statusCode;
}
