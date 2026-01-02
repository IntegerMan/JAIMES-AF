namespace MattEland.Jaimes.ServiceDefinitions.Exceptions;

/// <summary>
/// Exception thrown when an operation would create a duplicate resource.
/// </summary>
public class DuplicateResourceException : ArgumentException
{
    public DuplicateResourceException()
    {
    }

    public DuplicateResourceException(string? message) : base(message)
    {
    }

    public DuplicateResourceException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public DuplicateResourceException(string? message, string? paramName, Exception? innerException) : base(message, paramName, innerException)
    {
    }

    public DuplicateResourceException(string? message, string? paramName) : base(message, paramName)
    {
    }
}
