using Hangfire.Dashboard;
using System.Text;

public class BasicAuthAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public BasicAuthAuthorizationFilter(string username, string password)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Check if the Authorization header exists
        if (!httpContext.Request.Headers.ContainsKey("Authorization"))
        {
            Challenge(httpContext);
            return false;
        }

        // Get the Authorization header value
        var authHeader = httpContext.Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(httpContext);
            return false;
        }

        // Decode the credentials from the header
        var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
        var decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        var credentials = decodedCredentials.Split(':');

        // Validate username and password
        if (credentials.Length == 2 &&
            credentials[0] == _username &&
            credentials[1] == _password)
        {
            return true;
        }

        Challenge(httpContext);
        return false;
    }

    private void Challenge(HttpContext httpContext) 
    {
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
        httpContext.Response.StatusCode = 401; // Unauthorized
    }
}
