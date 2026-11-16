namespace SampleApp.Ksef;

/// <summary>Sesja KSeF. Token jest ważny 12 godzin od momentu wystawienia.</summary>
public sealed class KsefSession
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    public string Token { get; private set; } = "";
    public DateTimeOffset IssuedAt { get; private set; } = DateTimeOffset.MinValue;

    public bool IsExpired(DateTimeOffset now) => now - IssuedAt >= TokenLifetime;

    public void Renew(string newToken, DateTimeOffset now)
    {
        Token = newToken;
        IssuedAt = now;
    }
}
