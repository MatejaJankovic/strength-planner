namespace StrengthPlanner.Application.Security;

/// <summary>
/// Zahtevi za lozinku, na jednom mestu.
///
/// Identity ih primenjuje pri upisu, DTO ih odbija pre nego što zahtev uopšte stigne do
/// baze, a frontend ih saopštava korisniku. Ranije su te tri vrednosti živele odvojeno, pa
/// je granica mogla da se pomeri na jednom mestu a ostane stara na drugom.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// Najmanja dužina lozinke.
    ///
    /// Bilo je šest znakova bez ijedne dodatne provere, što je prihvatalo i „123456".
    /// Dužina je jedina mera koja stvarno otežava pogađanje — pravila o velikim slovima i
    /// znakovima uglavnom teraju ljude na predvidive obrasce, a ne na jače lozinke.
    /// </summary>
    public const int MinimumLength = 10;

    /// <summary>
    /// Najveća dužina lozinke.
    ///
    /// Nije zaštita od trošenja procesora: izmereno, lozinka od 500.000 znakova se
    /// heširala za 130 ms, jer PBKDF2 dugačak ključ prvo sažme pa tek onda ponavlja.
    /// Gornja granica postoji zato što lozinka koju niko ne može da otkuca nije lozinka
    /// nego greška u unosu, a bez granice bi neograničen tekst putovao kroz validaciju,
    /// heširanje i logove. Sto dvadeset osam znakova je iznad svega što ijedan menadžer
    /// lozinki generiše.
    /// </summary>
    public const int MaximumLength = 128;
}
