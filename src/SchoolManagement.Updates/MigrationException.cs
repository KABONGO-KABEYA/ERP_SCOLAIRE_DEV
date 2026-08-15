namespace SchoolManagement.Updates;

public sealed class MigrationException : Exception
{
    public MigrationException(string message)
        : base(message)
    {
    }

    public MigrationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
