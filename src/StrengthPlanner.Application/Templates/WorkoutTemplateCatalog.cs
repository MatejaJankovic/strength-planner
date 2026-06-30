namespace StrengthPlanner.Application.Templates;

public static class WorkoutTemplateCatalog
{
    public const string FullBodyKey = "full-body";
    public const string UpperLowerKey = "upper-lower";
    public const string PushPullLegsKey = "push-pull-legs";

    private static readonly IReadOnlyList<WorkoutTemplate> Templates =
    [
        new(
            FullBodyKey,
            "Full Body",
            [
                new("Day A", ["Back Squat", "Bench Press", "Barbell Row", "Overhead Press", "Leg Curl", "Barbell Curl"]),
                new("Day B", ["Deadlift", "Incline Bench Press", "Lat Pulldown", "Dumbbell Shoulder Press", "Leg Extension", "Triceps Pushdown"]),
                new("Day C", ["Front Squat", "Dumbbell Bench Press", "Seated Cable Row", "Lateral Raise", "Romanian Deadlift", "Cable Crunch"])
            ]),
        new(
            UpperLowerKey,
            "Upper/Lower",
            [
                new("Upper A", ["Bench Press", "Barbell Row", "Overhead Press", "Lat Pulldown", "Barbell Curl", "Triceps Pushdown"]),
                new("Lower A", ["Back Squat", "Romanian Deadlift", "Leg Press", "Leg Curl", "Calf Raise", "Plank"]),
                new("Upper B", ["Incline Bench Press", "Seated Cable Row", "Dumbbell Shoulder Press", "Pull-up", "Dumbbell Curl", "Overhead Triceps Extension"]),
                new("Lower B", ["Deadlift", "Front Squat", "Leg Extension", "Hip Thrust", "Calf Raise", "Cable Crunch"])
            ]),
        new(
            PushPullLegsKey,
            "Push/Pull/Legs",
            [
                new("Push", ["Bench Press", "Overhead Press", "Incline Bench Press", "Lateral Raise", "Triceps Pushdown", "Overhead Triceps Extension"]),
                new("Pull", ["Barbell Row", "Lat Pulldown", "Seated Cable Row", "Face Pull", "Barbell Curl", "Dumbbell Curl"]),
                new("Legs", ["Back Squat", "Romanian Deadlift", "Leg Press", "Leg Curl", "Calf Raise", "Cable Crunch"])
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
}
