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
    public Profile? Profile { get; set; }
    public ICollection<Mesocycle> Mesocycles { get; set; } = new List<Mesocycle>();
    public ICollection<OneRepMaxRecord> OneRepMaxRecords { get; set; } = new List<OneRepMaxRecord>();
}
