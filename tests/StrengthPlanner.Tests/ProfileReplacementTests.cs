using System.Reflection;
using StrengthPlanner.Application.DTOs.Auth;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Tests;

/// <summary>
/// <c>PUT /api/auth/profile</c> zamenjuje profil u celini: <c>UpdateProfileAsync</c> upisuje
/// svako polje iz zahteva, pa polje koje klijent ne pošalje postaje prazno u bazi.
///
/// To nije teorija. Kada su registraciji dodati ime i visina, ekran profila ih još nije
/// slao — i prvo čuvanje telesne mase brisalo je oboje, uz poruku da je profil sačuvan.
/// Ništa na serveru nije puklo, jer sa njegove strane je prazno polje legitiman zahtev.
///
/// Ovaj test zaključava upravo taj oblik greške: svako polje profila koje korisnik može da
/// menja mora da postoji i u <see cref="UpdateProfileDto"/>. Sledeća kolona dodata u
/// <see cref="Profile"/> obara build ovde, a ne tiho briše podatke nekome na ekranu.
/// </summary>
public class ProfileReplacementTests
{
    /// <summary>
    /// Polja profila koja ne dolaze iz zahteva, sa razlogom zašto.
    ///
    /// Spisak je namerno kratak i imenovan: dopisati ime ovde je svesna izjava da polje
    /// nije korisnikovo da ga menja, a ne način da se test ućutka.
    /// </summary>
    private static readonly Dictionary<string, string> NotUserEditable = new()
    {
        [nameof(Profile.Id)] = "Surogat ključ; ProfileConfiguration ga i ignoriše.",
        [nameof(Profile.UserId)] = "Identitet vlasnika, dolazi iz tokena, nikada iz tela zahteva.",
        // Slika ide svojim endpointom (PUT/DELETE /api/auth/avatar) kao multipart, ne kroz
        // JSON zamenu profila: base64 u telu zahteva uveća sadržaj za trećinu, a tip slike
        // mora da utvrdi server iz bajtova. Da su ova dva polja u UpdateProfileDto, svako
        // čuvanje osnovnih podataka slalo bi i brisalo sliku.
        [nameof(Profile.AvatarBytes)] = "Otprema se kroz PUT /api/auth/avatar, ne kroz zamenu profila.",
        [nameof(Profile.AvatarContentType)] = "Utvrđuje ga server iz sadržaja slike, klijent ga ne šalje."
    };

    [Fact]
    public void EveryEditableProfileFieldIsCarriedByTheUpdateRequest()
    {
        var requestFields = typeof(UpdateProfileDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet();

        var missing = typeof(Profile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .Where(name => !NotUserEditable.ContainsKey(name))
            .Where(name => !requestFields.Contains(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Profile ima polja koja UpdateProfileDto ne nosi: {string.Join(", ", missing)}. "
            + "Pošto PUT /api/auth/profile zamenjuje profil u celini, takvo polje se pri "
            + "svakom čuvanju profila briše. Dodaj ga u DTO, ili ga upiši u NotUserEditable "
            + "sa razlogom zašto korisnik ne sme da ga menja.");
    }

    [Fact]
    public void TheExclusionListOnlyNamesFieldsThatStillExist()
    {
        // Bez ovoga bi preimenovano polje ostalo na spisku izuzetaka i tiho izuzelo ništa,
        // pa bi provera iznad propustila pravo polje pod novim imenom.
        var profileFields = typeof(Profile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet();

        var stale = NotUserEditable.Keys.Where(name => !profileFields.Contains(name)).ToList();

        Assert.True(
            stale.Count == 0,
            $"NotUserEditable navodi polja koja Profile više nema: {string.Join(", ", stale)}.");
    }
}
