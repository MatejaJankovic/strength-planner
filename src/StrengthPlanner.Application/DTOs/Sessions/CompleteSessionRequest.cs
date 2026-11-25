using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Application.DTOs.Sessions;

/// <summary>Šta vežbač kaže uz završetak treninga. Sve je neobavezno.</summary>
public class CompleteSessionRequest
{
    /// <summary>Koliko je umoran posle treninga, 1 (sveže) do 5 (iscrpljen); izostavljeno znači bez odgovora.</summary>
    [Range(SubjectiveFatigue.MinRating, SubjectiveFatigue.MaxRating)]
    public int? PerceivedFatigue { get; set; }
}
