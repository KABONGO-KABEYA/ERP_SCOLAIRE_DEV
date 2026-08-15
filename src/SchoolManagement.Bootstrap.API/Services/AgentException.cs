namespace SchoolManagement.Bootstrap.API.Services;

public sealed class AgentException : Exception
{
    public AgentException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
