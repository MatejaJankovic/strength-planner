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

    public byte[] Content { get; }

    /// <summary>
    /// MIME tip iz <c>ImageFormat.Detect</c>, nikada onaj koji je klijent poslao pri
    /// otpremanju — ovaj tip ide u odgovor koji pregledač tumači.
    /// </summary>
    public string ContentType { get; }
}
