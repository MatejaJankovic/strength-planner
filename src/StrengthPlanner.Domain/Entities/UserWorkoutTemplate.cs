namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Šablon treninga koji je korisnik sam sastavio.
///
/// Ugrađeni šabloni su <b>ponuda</b>: nose više vežbi nego što trening dobija, a
/// <see cref="Domain.Algorithms.SessionComposition"/> iz njih bira po nivou iskustva.
/// Lični šablon je <b>propis</b> - u trening ulazi tačno ono što je korisnik izabrao,
/// onim redom kojim je izabrao.
///
/// Čuva se, a ne troši jednokratno, jer blok dugoročnog plana pamti samo ključ šablona i
/// generiše se tek kada mu dođe red - ponekad mesecima kasnije.
/// </summary>
public class UserWorkoutTemplate
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<UserWorkoutTemplateDay> Days { get; set; } = new();
}

/// <summary>Jedan trenažni dan ličnog šablona. Redosled dana je redosled u nedelji.</summary>
public class UserWorkoutTemplateDay
{
    public Guid Id { get; set; }

    public Guid UserWorkoutTemplateId { get; set; }

    public UserWorkoutTemplate UserWorkoutTemplate { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public List<UserWorkoutTemplateExercise> Exercises { get; set; } = new();
}

/// <summary>
/// Vežba u danu ličnog šablona, sa brojem serija i opsegom ponavljanja koje je korisnik
/// uneo.
///
/// Ove vrednosti su <b>propis prve nedelje</b>, ne konačan broj: periodizacija ih pomera
/// kroz blok isto kao što pomera propis izveden iz cilja, a predlog serija ih dalje vuče ka
/// nedeljnom cilju volumena. Ono što je korisnik uneo ostaje sidro
/// (<c>ExercisePlan.PrescribedSets</c>), pa se na ekranu vidi i koliko je tražio i koliko
/// mu se predlaže.
/// </summary>
public class UserWorkoutTemplateExercise
{
    public Guid Id { get; set; }

    public Guid UserWorkoutTemplateDayId { get; set; }

    public UserWorkoutTemplateDay UserWorkoutTemplateDay { get; set; } = null!;

    public Guid ExerciseId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public int Order { get; set; }

    public int Sets { get; set; }

    public int RepRangeMin { get; set; }

    public int RepRangeMax { get; set; }
}
