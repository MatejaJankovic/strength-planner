namespace StrengthPlanner.Application.Security;

/// <summary>
/// Prepoznavanje formata slike po sadržaju fajla.
///
/// Format se **ne** čita iz <c>Content-Type</c> zaglavlja ni iz ekstenzije imena fajla:
/// oba šalje klijent i oba se slobodno lažu. Ko pošalje izvršni fajl pod imenom
/// <c>slika.png</c> i zaglavljem <c>image/png</c>, prošao bi svaku proveru koja veruje
/// klijentu — a bajtovi bi se posle vraćali svakom pregledaču koji otvori profil, sa
/// tipom koji smo mu sami pripisali.
///
/// Ovde se gleda potpis na početku fajla, i vraća se tip koji server sam utvrdi. Za
/// avatar to je dovoljno: prihvataju se samo tri formata koja svaki pregledač prikazuje.
/// </summary>
public static class ImageFormat
{
    /// <summary>Najveća veličina slike profila u bajtovima.</summary>
    ///
    /// <remarks>
    /// Slika se čuva u koloni baze i vraća pri svakom čitanju profila, pa granica nije
    /// samo zaštita od zloupotrebe nego i od sopstvenog odgovora. Dva megabajta su
    /// preko potrebnog za krug od 96 piksela.
    /// </remarks>
    public const int MaximumSizeBytes = 2 * 1024 * 1024;

    /// <summary>Tipovi koje prepoznajemo, za poruku o grešci i za dokumentaciju.</summary>
    public const string SupportedFormats = "JPEG, PNG ili WebP";

    /// <summary>
    /// Vraća MIME tip koji odgovara sadržaju, ili <c>null</c> ako sadržaj nije nijedna od
    /// podržanih slika.
    /// </summary>
    public static string? Detect(ReadOnlySpan<byte> content)
    {
        if (IsJpeg(content))
        {
            return "image/jpeg";
        }

        if (IsPng(content))
        {
            return "image/png";
        }

        return IsWebp(content) ? "image/webp" : null;
    }

    // FF D8 FF — svaki JPEG počinje markerom SOI praćenim još jednim markerom.
    private static bool IsJpeg(ReadOnlySpan<byte> content) =>
        content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

    // 89 "PNG" CR LF SUB LF — potpis je osmobajtni i namerno sadrži znakove koji otkrivaju
    // prenos koji kvari nove redove.
    private static bool IsPng(ReadOnlySpan<byte> content) =>
        content.Length >= 8
        && content[0] == 0x89
        && content[1] == 0x50
        && content[2] == 0x4E
        && content[3] == 0x47
        && content[4] == 0x0D
        && content[5] == 0x0A
        && content[6] == 0x1A
        && content[7] == 0x0A;

    /// <summary>
    /// WebP je RIFF kontejner: bajtovi 0-3 su „RIFF", 4-7 su dužina, a 8-11 „WEBP".
    /// Provera samo „RIFF" ne bi bila dovoljna — isti kontejner nosi i WAV i AVI.
    /// </summary>
    private static bool IsWebp(ReadOnlySpan<byte> content) =>
        content.Length >= 12
        && content[0] == 0x52
        && content[1] == 0x49
        && content[2] == 0x46
        && content[3] == 0x46
        && content[8] == 0x57
        && content[9] == 0x45
        && content[10] == 0x42
        && content[11] == 0x50;
}
