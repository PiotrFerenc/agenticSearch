namespace SampleApp.Ksef;

public sealed class KsefException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>Klient API KSeF (uproszczony na potrzeby przykładu).</summary>
public sealed class KsefClient(KsefSession session)
{
    public string RequestNewToken() => Guid.NewGuid().ToString("N");

    public void SendInvoice(string invoiceNumber, DateTimeOffset now)
    {
        if (session.IsExpired(now))
            throw new KsefException(21401, "Session token expired");

        // ... wysłanie faktury do KSeF ...
    }
}
