using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Tests;

/// <summary>
/// Provera sadržaja slike profila.
///
/// Slika je jedini podatak u aplikaciji koji korisnik otpremi kao fajl i koji se posle
/// vraća pregledaču sa tipom koji mu server pripiše. Zato se tip utvrđuje iz bajtova, a ne
/// iz <c>Content-Type</c> zaglavlja ni iz ekstenzije: oba dolaze od klijenta. Testovi
/// ispod drže obe strane te odluke — da se podržani formati prepoznaju, i da se sve što
/// nije slika odbije, uključujući sadržaj koji se pravi da je slika.
/// </summary>
public class AvatarUploadTests
{
    private static byte[] Jpeg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

    private static byte[] Png() =>
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

    private static byte[] Webp()
    {
        // RIFF....WEBP — četiri bajta potpisa, četiri dužine, pa oznaka formata.
        var content = new byte[16];
        "RIFF"u8.CopyTo(content);
        content[4] = 0x10;
        "WEBP"u8.CopyTo(content.AsSpan(8));
        return content;
    }

    [Fact]
    public void RecognisesJpeg()
    {
        Assert.Equal("image/jpeg", ImageFormat.Detect(Jpeg()));
    }

    [Fact]
    public void RecognisesPng()
    {
        Assert.Equal("image/png", ImageFormat.Detect(Png()));
    }

    [Fact]
    public void RecognisesWebp()
    {
        Assert.Equal("image/webp", ImageFormat.Detect(Webp()));
    }

    [Fact]
    public void RejectsAnExecutableRenamedToLookLikeAnImage()
    {
        // "MZ" je početak Windows izvršnog fajla. Ime fajla i zaglavlje zahteva mogu da
        // tvrde image/png; sadržaj ne može.
        var executable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        Assert.Null(ImageFormat.Detect(executable));
    }

    [Fact]
    public void RejectsAnHtmlDocument()
    {
        // Ovo je konkretno opasno: HTML koji se vrati sa tipom slike jedan pregledač
        // prikaže kao sliku, a drugi izvrši. Zato tip nikada ne sme da bude klijentov.
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        Assert.Null(ImageFormat.Detect(html));
    }

    [Fact]
    public void RejectsAnSvg()
    {
        // SVG je slika, ali i dokument koji nosi skript. Namerno nije na spisku.
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8
            .ToArray();

        Assert.Null(ImageFormat.Detect(svg));
    }

    [Fact]
    public void RejectsARiffContainerThatIsNotWebp()
    {
        // WAV i AVI koriste isti RIFF kontejner kao WebP. Provera samo "RIFF" bi ih pustila.
        var wav = new byte[16];
        "RIFF"u8.CopyTo(wav);
        "WAVE"u8.CopyTo(wav.AsSpan(8));

        Assert.Null(ImageFormat.Detect(wav));
    }

    [Fact]
    public void RejectsEmptyContent()
    {
        Assert.Null(ImageFormat.Detect(Array.Empty<byte>()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void RejectsContentTooShortToCarryASignature(int length)
    {
        // Odsečen PNG potpis ne sme da prođe kao PNG. Bez provere dužine bi čitanje
        // osmog bajta iz sadržaja od dva puklo umesto da vrati "nije slika".
        var truncated = Png().AsSpan(0, length).ToArray();

        Assert.Null(ImageFormat.Detect(truncated));
    }

    [Fact]
    public void JpegNeedsTheThirdSignatureByteToo()
    {
        // FF D8 sam nije dovoljan: iza SOI markera mora da stoji još jedan marker.
        Assert.Null(ImageFormat.Detect(new byte[] { 0xFF, 0xD8, 0x00, 0x00 }));
    }

    [Fact]
    public void SizeLimitIsSmallEnoughToBeWorthSomething()
    {
        // Slika stoji u koloni baze i vraća se pri svakom otvaranju profila, pa granica
        // štiti i sopstveni odgovor, ne samo upis. Podizanje ovoga mora da bude odluka.
        Assert.True(
            ImageFormat.MaximumSizeBytes <= 4 * 1024 * 1024,
            $"Granica veličine slike je podignuta na {ImageFormat.MaximumSizeBytes} bajtova.");
    }
}
