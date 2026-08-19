namespace OurLive.App.Services;

/// <summary>Attaches the stored bearer token to every API request; on a 401 it logs the session out
/// so the UI can redirect to the login page instead of silently failing every subsequent call.</summary>
internal sealed class AuthTokenHandler(AppAuthState authState, AuthTokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (authState.Token is { } token)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            tokenStore.Clear();
            authState.SetLoggedOut();
        }

        return response;
    }
}
