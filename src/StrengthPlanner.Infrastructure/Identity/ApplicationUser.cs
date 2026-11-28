using Microsoft.AspNetCore.Identity;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Identity;

/// <summary>
/// Identity nalog korisnika. Zamenjuje raniji domenski <c>User</c> entitet:
/// jedan korisnik = jedan Identity nalog, isti Guid Id nosi i domenske veze.
/// Domenski entiteti (Profile, Mesocycle, OneRepMaxRecord) drže samo Guid UserId;
/// obrnute navigacije stoje ovde da Domain ostane bez zavisnosti na Identity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Kada je nalog izričito pristao na obradu podataka, po UTC. Zakon traži da rukovalac to ume da
    /// dokaže. Null je kod naloga napravljenih pre nego što se pristanak tražio.
    /// </summary>
    public DateTime? PrivacyConsentAtUtc { get; set; }

    /// <summary>Verzija politike privatnosti na koju je pristanak dat.</summary>
    public string? PrivacyPolicyVersion { get; set; }

    public Profile? Profile { get; set; }
    public ICollection<Mesocycle> Mesocycles { get; set; } = new List<Mesocycle>();
    public ICollection<OneRepMaxRecord> OneRepMaxRecords { get; set; } = new List<OneRepMaxRecord>();
}
