namespace ChildrenCharity.Mailing.Core.Infrastructure.Common;

public class TransientProviderException : Exception
{
    public TransientProviderException(string message)
        : base(message)
    {
    }

    public TransientProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
