namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Slika profila spremna za slanje: bajtovi i tip koji je server sam utvrdio.
/// </summary>
public class AvatarDto
{
    public AvatarDto(byte[] content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }

    /// <summary>
    /// Bajtovi slike.
    ///
    /// Niz se **ne** kopira pri prosleđivanju, iako geteri bez setera to nagoveštavaju:
    /// slika je do dva megabajta i odbrambena kopija na svakom čitanju profila nije
    /// besplatna. Vlasništvo je jednosmerno — DTO nastaje iz upita i odmah ide u odgovor,
    /// i niko ga u tom putu ne menja. Ako se to promeni, ovde treba `ReadOnlyMemory{byte}`.
    /// </summary>
    public byte[] Content { get; }

    /// <summary>
    /// MIME tip iz <c>ImageFormat.Detect</c>, nikada onaj koji je klijent poslao pri
    /// otpremanju — ovaj tip ide u odgovor koji pregledač tumači.
    /// </summary>
    public string ContentType { get; }
}
