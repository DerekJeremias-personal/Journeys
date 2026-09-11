namespace Journeys.Core.Services;

public sealed class JourneyMaterializePathException : Exception
{
    public JourneyMaterializePathException(string message, Exception innerException)
        : base(message, innerException) { }
}
