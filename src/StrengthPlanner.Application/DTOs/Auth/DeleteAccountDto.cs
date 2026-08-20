using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Application.DTOs.Auth;

/// <summary>
/// Potvrda brisanja naloga.
///
/// Traže se dve stvari, i to namerno: trenutna lozinka, i otkucana reč potvrde. Lozinka
/// štiti od nekoga kome je telefon ostao otključan; otkucana reč štiti od samog korisnika,
/// jer je ovo jedina operacija u aplikaciji koja se ne može vratiti — nema oporavka
/// lozinke, nema rezervne kopije, i nema drugog naloga sa istim podacima.
/// </summary>
public class DeleteAccountDto
{
    [Required]
    [MaxLength(PasswordPolicy.MaximumLength)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Reč koju korisnik mora da otkuca. Poredi se sa
    /// <see cref="AccountDeletionPolicy.ConfirmationWord"/>, bez obzira na velika i mala
    /// slova — traži se svesna namera, ne pogađanje tastature.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string Confirmation { get; set; } = string.Empty;
}
