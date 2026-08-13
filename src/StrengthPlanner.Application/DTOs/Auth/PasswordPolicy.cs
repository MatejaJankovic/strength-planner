namespace StrengthPlanner.Application.DTOs.Auth;

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
}
