namespace SchoolManagement.Bootstrap.API.Services;

public sealed class CatalogException : Exception
{
    public CatalogException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
