namespace StrengthPlanner.Domain.Enums;

/// <summary>
/// Pol korisnika. Ne ulazi ni u jedan algoritam - stoji uz uzrast i telesnu masu kao
/// evidencija, pa je i opcion (property je nullable).
///
/// Bio je slobodan string, i to je bilo dovoljno da se dva ekrana raziđu: registracija je
/// upisivala "male"/"female", a profil nudio "M"/"F". Nijedna vrednost sa registracije nije
/// odgovarala ponuđenoj na profilu, pa je izabrani pol na profilu ostajao prazan. Enum tu
/// vrstu neslaganja čini nemogućom, isto kao kod <see cref="ExperienceLevel"/>.
/// </summary>
public enum Sex
{
    Male = 0,
    Female = 1
}
