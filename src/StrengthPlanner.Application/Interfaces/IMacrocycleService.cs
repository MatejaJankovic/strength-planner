using StrengthPlanner.Application.DTOs.Macrocycles;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Application.Interfaces;

public interface IMacrocycleService
{
    /// <summary>
    /// Pravi dugoročan plan i odmah generiše njegov prvi blok. Ostali blokovi čekaju
    /// svoj red da bi krenuli od 1RM vrednosti koje važe tada.
    /// </summary>
    Task<MacrocycleDto> CreateAsync(
        Guid userId,
        CreateMacrocycleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Predlog blokova sa smenjujućim ciljevima — jedina definicija tog pravila živi u
    /// domenu, pa ga klijent ne ponavlja svojom logikom.
    ///
    /// Asinhrono je otkako ključ šablona može da bude i lični šablon: provera da on postoji
    /// i da pripada ovom korisniku traži bazu.
    /// </summary>
    Task<IReadOnlyList<CreateMacrocycleBlockDto>> SuggestBlocksAsync(
        Guid userId,
        int blockCount,
        Goal firstGoal,
        string templateKey,
        CancellationToken cancellationToken = default);

    Task<MacrocycleDto> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MacrocycleDto> GetByIdAsync(Guid userId, Guid macrocycleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Briše plan zajedno sa mezociklusima njegovih blokova.
    ///
    /// Mezociklusi se moraju brisati izričito: strani ključ bloka na mezociklus je
    /// <c>SetNull</c>, pa bi kaskada plan → blokovi ostavila mezocikluse iza sebe. Brisanje
    /// je i jedini put do uklanjanja treninga otkako plan ne može da se napravi mimo
    /// makrociklusa, pa mezociklus bez plana ni ne postoji.
    /// </summary>
    Task DeleteAsync(Guid userId, Guid macrocycleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ako je mezociklus u celosti odrađen a plan ima sledeći blok, generiše ga i
    /// postavlja kao aktivan. Vraća null kada nema šta da se prelazi.
    /// </summary>
    Task<MacrocycleAdvance?> AdvanceIfFinishedAsync(
        Guid userId,
        Guid mesocycleId,
        DateTime now,
        CancellationToken cancellationToken = default);
}

/// <summary>Prelazak na sledeći blok plana, za poruku korisniku posle treninga.</summary>
public sealed record MacrocycleAdvance(
    string PlanName,
    int BlockOrder,
    int BlockCount,
    Goal Goal,
    Guid MesocycleId,
    string MesocycleName);
