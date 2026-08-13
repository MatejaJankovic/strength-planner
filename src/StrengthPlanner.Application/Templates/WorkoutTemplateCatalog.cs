namespace StrengthPlanner.Application.Templates;

/// <summary>
/// Ponuđeni šabloni treninga.
///
/// Šablon je <b>ponuda, a ne propis</b>: <see cref="Domain.Algorithms.SessionComposition"/>
/// iz njega bira onoliko vežbi koliko nivo iskustva dozvoljava. Zato svaki dan ovde nosi
/// više vežbi nego što ijedan trening zaista ima.
///
/// Tri pravila važe za svaki dan i na njih se oslanja ostatak sistema:
///
/// <list type="number">
/// <item><b>Složene vežbe idu prve.</b> Priručnik to i traži (<i>"Složene vežbe radiš pre
/// izolacionih vežbi, jer su zamornije"</i>), a izbor po nivou iskustva se gradi upravo
/// na tom redosledu.</item>
/// <item><b>Pet izolacija po danu.</b> Naprednom vežbaču pripada jedna složena vežba po
/// treningu i <i>"pretežno izolacije"</i>; bez pet izolacija njegov trening bi ostao kraći
/// od punog.</item>
/// <item><b>Prva složena vežba dana se rotira.</b> Napredni vežbač zadržava samo nju, pa
/// bi šablon koji svaki dan otvara čučnjem njemu dao nedelju bez ijednog gornjeg pokreta.</item>
/// </list>
///
/// Šabloni sa više dana namerno nose manje složenih vežbi po treningu: pri većoj
/// frekvenciji <i>"ključno je smanjiti volumen po treningu"</i>, jer se ista mišićna grupa
/// pogađa više puta nedeljno.
/// </summary>
public static class WorkoutTemplateCatalog
{
    public const string FullBodyTwoDayKey = "full-body-2";
    public const string FullBodyKey = "full-body";
    public const string PushPullLegsKey = "push-pull-legs";
    public const string UpperLowerKey = "upper-lower";
    public const string FullBodyFourDayKey = "full-body-4";
    public const string UpperLowerPushPullLegsKey = "upper-lower-ppl";
    public const string PushPullLegsSixDayKey = "push-pull-legs-6";

    private static readonly IReadOnlyList<WorkoutTemplate> Templates =
    [
        new(
            FullBodyTwoDayKey,
            "Full Body (2 dana)",
            [
                new("Day A",
                [
                    "Back Squat", "Bench Press", "Barbell Row",
                    "Leg Curl", "Cable Fly", "Lateral Raise", "Triceps Pushdown", "Calf Raise"
                ]),
                new("Day B",
                [
                    "Overhead Press", "Romanian Deadlift", "Lat Pulldown",
                    "Leg Extension", "Dumbbell Fly", "Straight-Arm Pulldown", "Barbell Curl", "Cable Crunch"
                ])
            ],
            // Dva treninga nedeljno ne staju u MEV za većinu mišićnih grupa — nema
            // dovoljno mesta u nedelji. Šablon ostaje jer je bolji od nijednog, ali
            // korisnik treba da zna šta bira.
            "Dva treninga nedeljno održavaju formu, ali za većinu mišića ostaju ispod "
            + "minimalnog volumena za rast. Ako možeš tri dana, biraj trodnevni plan."),
        new(
            FullBodyKey,
            "Full Body",
            [
                new("Day A",
                [
                    "Back Squat", "Bench Press", "Barbell Row",
                    "Leg Curl", "Straight-Arm Pulldown", "Cable Fly", "Lateral Raise", "Triceps Pushdown"
                ]),
                new("Day B",
                [
                    "Overhead Press", "Deadlift", "Lat Pulldown",
                    "Leg Extension", "Rear Delt Fly", "Barbell Curl", "Skull Crusher", "Cable Crunch"
                ]),
                new("Day C",
                [
                    "Pull-up", "Front Squat", "Dumbbell Bench Press",
                    "Dumbbell Fly", "Face Pull", "Hammer Curl", "Leg Curl", "Calf Raise"
                ])
            ]),
        new(
            PushPullLegsKey,
            "Push/Pull/Legs",
            [
                new("Push",
                [
                    "Bench Press", "Overhead Press", "Incline Bench Press",
                    "Cable Fly", "Dumbbell Fly", "Lateral Raise", "Triceps Pushdown", "Overhead Triceps Extension"
                ]),
                new("Pull",
                [
                    "Barbell Row", "Pull-up", "Seated Cable Row",
                    "Face Pull", "Cable Crunch", "Barbell Curl", "Calf Raise", "Hammer Curl"
                ]),
                new("Legs",
                [
                    "Back Squat", "Romanian Deadlift", "Leg Press",
                    "Leg Extension", "Leg Curl", "Calf Raise", "Plank", "Cable Crunch"
                ])
            ]),
        new(
            UpperLowerKey,
            "Upper/Lower",
            [
                new("Upper A",
                [
                    "Bench Press", "Barbell Row", "Overhead Press",
                    "Cable Fly", "Lateral Raise", "Triceps Pushdown", "Barbell Curl", "Face Pull"
                ]),
                new("Lower A",
                [
                    "Back Squat", "Romanian Deadlift", "Leg Press",
                    "Leg Curl", "Calf Raise", "Leg Extension", "Plank", "Cable Crunch"
                ]),
                new("Upper B",
                [
                    "Pull-up", "Incline Bench Press", "Dumbbell Shoulder Press",
                    "Dumbbell Fly", "Overhead Triceps Extension", "Straight-Arm Pulldown", "Hammer Curl", "Rear Delt Fly"
                ]),
                new("Lower B",
                [
                    "Deadlift", "Front Squat", "Hip Thrust",
                    "Leg Extension", "Cable Crunch", "Leg Curl", "Calf Raise", "Plank"
                ])
            ]),
        new(
            FullBodyFourDayKey,
            "Full Body (4 dana)",
            [
                new("Day A",
                [
                    "Back Squat", "Bench Press",
                    "Leg Curl", "Cable Fly", "Lateral Raise", "Triceps Pushdown", "Cable Crunch"
                ]),
                new("Day B",
                [
                    "Barbell Row", "Romanian Deadlift",
                    "Leg Extension", "Straight-Arm Pulldown", "Face Pull", "Barbell Curl", "Calf Raise"
                ]),
                new("Day C",
                [
                    "Overhead Press", "Leg Press",
                    "Leg Curl", "Dumbbell Fly", "Rear Delt Fly", "Skull Crusher", "Plank"
                ]),
                new("Day D",
                [
                    "Pull-up", "Front Squat",
                    "Leg Extension", "Lateral Raise", "Hammer Curl", "Overhead Triceps Extension", "Calf Raise"
                ])
            ]),
        new(
            UpperLowerPushPullLegsKey,
            "Upper/Lower + Push/Pull/Legs",
            [
                new("Upper",
                [
                    "Bench Press", "Barbell Row",
                    "Cable Fly", "Barbell Curl", "Triceps Pushdown", "Cable Crunch"
                ]),
                new("Lower",
                [
                    "Back Squat", "Romanian Deadlift",
                    "Leg Curl", "Leg Extension", "Calf Raise", "Plank"
                ]),
                new("Push",
                [
                    "Incline Bench Press", "Dumbbell Shoulder Press",
                    "Dumbbell Fly", "Lateral Raise", "Skull Crusher", "Cable Crunch"
                ]),
                new("Pull",
                [
                    "Pull-up", "Seated Cable Row",
                    "Straight-Arm Pulldown", "Hammer Curl", "Rear Delt Fly", "Plank"
                ]),
                new("Legs",
                [
                    "Leg Press", "Hip Thrust",
                    "Leg Extension", "Leg Curl", "Calf Raise", "Cable Crunch"
                ])
            ]),
        new(
            PushPullLegsSixDayKey,
            "Push/Pull/Legs x2",
            [
                new("Push A",
                [
                    "Bench Press", "Overhead Press",
                    "Cable Fly", "Lateral Raise", "Triceps Pushdown", "Cable Crunch"
                ]),
                new("Pull A",
                [
                    "Barbell Row", "Lat Pulldown",
                    "Straight-Arm Pulldown", "Barbell Curl", "Calf Raise", "Cable Crunch"
                ]),
                new("Legs A",
                [
                    "Back Squat", "Romanian Deadlift",
                    "Leg Curl", "Leg Extension", "Calf Raise", "Cable Crunch"
                ]),
                new("Push B",
                [
                    "Incline Bench Press", "Dumbbell Shoulder Press",
                    "Dumbbell Fly", "Rear Delt Fly", "Skull Crusher", "Plank"
                ]),
                new("Pull B",
                [
                    "Pull-up", "Seated Cable Row",
                    "Rear Delt Fly", "Hammer Curl", "Dumbbell Curl", "Plank"
                ]),
                new("Legs B",
                [
                    "Leg Press", "Hip Thrust",
                    "Leg Extension", "Leg Curl", "Calf Raise", "Plank"
                ])
            ])
    ];

    public static IReadOnlyList<WorkoutTemplate> GetAll()
    {
        return Templates;
    }

    public static WorkoutTemplate? GetByKey(string templateKey)
    {
        return Templates.FirstOrDefault(template =>
            string.Equals(template.Key, templateKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Šablon koji odgovara broju trenažnih dana iz profila. Ako tačnog nema, bira se
    /// najduži koji ne traži više dana nego što korisnik ima — bolje je odraditi ceo
    /// kraći plan nego stalno preskakati treninge iz dužeg.
    /// </summary>
    public static WorkoutTemplate SuggestedFor(int trainingDaysPerWeek)
    {
        var exact = Templates.FirstOrDefault(template => template.Days.Count == trainingDaysPerWeek);
        if (exact is not null)
        {
            return exact;
        }

        var fitting = Templates
            .Where(template => template.Days.Count <= trainingDaysPerWeek)
            .OrderByDescending(template => template.Days.Count)
            .FirstOrDefault();

        return fitting ?? Templates.MinBy(template => template.Days.Count)!;
    }
}
