using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.Security;

namespace StrengthPlanner.Application.DTOs.Notes;

/// <summary>
/// Sets a note; null or only whitespace clears it. The call replaces the note, so a body without the
/// property (<c>{}</c>, or a misspelt name) clears it too. The attribute caps the raw text, spaces
/// included; the service checks the length again after trimming.
/// </summary>
public class SetNoteRequest
{
    [MaxLength(NotePolicy.MaximumLength)]
    public string? Note { get; set; }
}

/// <summary>The note as it was stored (trimmed), or null when it was cleared.</summary>
public class NoteDto
{
    public string? Note { get; set; }
}
