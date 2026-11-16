namespace SampleApp.Ksef;

public enum InvoiceStatus { Oczekuje, Wyslana }

/// <summary>Wysyła faktury do KSeF; przy wygasłym tokenie faktura zostaje w statusie Oczekuje.</summary>
public sealed class InvoiceSender(KsefClient client)
{
    public InvoiceStatus Send(string invoiceNumber, DateTimeOffset now)
    {
        try
        {
            client.SendInvoice(invoiceNumber, now);
            return InvoiceStatus.Wyslana;
        }
        catch (KsefException e) when (e.Code == 21401)
        {
            return InvoiceStatus.Oczekuje;
        }
    }
}
