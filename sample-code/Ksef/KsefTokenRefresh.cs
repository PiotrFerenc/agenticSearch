namespace SampleApp.Ksef;

/// <summary>
/// Job odnawiający token sesji KSeF. Uruchamiany wyłącznie przez harmonogram, bez odnowienia przy starcie aplikacji.
/// </summary>
public sealed class KsefTokenRefresh(KsefClient client, KsefSession session, ILogger log)
{
    public const string Schedule = "0 */8 * * *";   // cron: 00:00, 08:00, 16:00
    private const int MaxRetries = 3;

    public void Run(DateTimeOffset now)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                session.Renew(client.RequestNewToken(), now);
                return;
            }
            catch (Exception e)
            {
                log.Warn($"Odnowienie tokenu KSeF nie powiodło się (próba {attempt}/{MaxRetries}): {e.Message}");
            }
        }
        // Po wyczerpaniu prób job kończy się bez alarmu, kolejna szansa dopiero za 8 godzin.
    }
}

public interface ILogger
{
    void Warn(string message);
}
