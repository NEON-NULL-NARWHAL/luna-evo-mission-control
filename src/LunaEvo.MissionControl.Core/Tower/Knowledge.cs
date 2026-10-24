// LUNA EVO · LunaEvo.MissionControl.Core/Tower/Knowledge.cs
// [Core] What the server's AI agents know (the owner's "tech tree", kept on the LMP server): skills graded from their
//        real flights (KSP Wingman's outcome records), and lessons that must cite those flights. Pure, plus Load/Save.
// Used by: TowerServer (/v1/flight, /v1/lesson, /v1/knowledge), WingmanPage
// Guide:   Meat and Potatoes/30-Knowledge.md

using System.Text.Json;

namespace LunaEvo.MissionControl.Core.Tower;

/// <summary>A skill in the tree: what it is, which skills come first, and how a flight shows it.</summary>
public sealed record Skill(string Id, string Name, string What, IReadOnlyList<string> Requires);

/// <summary>One finished flight as the server keeps it: Wingman's outcome, and what it says about each skill it used.</summary>
public sealed record FlightRecord(long Id, DateTime Time, string CallSign, string Craft, string Result, string? Approach,
    int GoArounds, double Minutes, double? DownRunway, double? OffCenterline, double? HeadingError, IReadOnlyList<string> Phases,
    IReadOnlyDictionary<string, bool> Skills);

/// <summary>A short fact an agent learned, with the flights it came from. Confirmed when another flight says it again.</summary>
public sealed record Lesson(long Id, string Skill, string Text, string Author, DateTime Created, DateTime Confirmed,
    IReadOnlyList<long> Evidence, int Confirmations)
{
    /// <summary>Lessons with more evidence come first.</summary>
    public int Weight => Evidence.Count + Confirmations;
}

/// <summary>A skill's numbers: good flights out of attempts.</summary>
public readonly record struct Proficiency(int Good, int Attempts)
{
    public override string ToString() => $"{Good} of {Attempts} good";
}

public sealed class Knowledge
{
    /// <summary>The tree, easiest first. A skill is open to practise once each one it requires has this many good flights.</summary>
    public const int UnlockAfter = 3;

    public static readonly IReadOnlyList<Skill> Tree =
    [
        new("takeoff", "Take off", "off the runway and climbing out", []),
        new("route", "Fly a route", "an approach's fixes, to the final", ["takeoff"]),
        new("hold", "Hold", "circling a hold point until cleared", ["takeoff"]),
        new("land-visual", "Land, visual", "a circuit or the short visual, then the final", ["takeoff"]),
        new("land-rnav", "Land, RNAV", "a published RNAV approach, then the final", ["route", "land-visual"]),
        new("sequencing", "Land in sequence", "hold for the traffic, then land when the tower clears it", ["hold", "land-rnav"]),
    ];

    public const int MaxFlights = 2000;
    public const int MaxLessons = 300;
    public const int MaxLessonsPerSkill = 20;
    public const int MaxLessonText = 200;

    /// <summary>Lessons nobody confirmed are dropped after this long.</summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromDays(14);

    /// <summary>A good landing stops within this far of the centerline, without a go-around.</summary>
    public const double GoodCenterline = 15;

    private readonly List<FlightRecord> _flights = [];
    private readonly List<Lesson> _lessons = [];
    private long _nextFlight = 1, _nextLesson = 1;

    public IReadOnlyList<FlightRecord> Flights => _flights;
    public IReadOnlyList<Lesson> Lessons => _lessons;

    /// <summary>
    /// How a flight shows each skill it used (true = done well). From the phases it went through, in order, and how it
    /// ended: a landing counts as good when it stopped on the runway within 15 m of the centerline, first time.
    /// </summary>
    public static Dictionary<string, bool> Grade(string result, string? approach, int goArounds, double? offCenterline, IReadOnlyList<string> phases)
    {
        var skills = new Dictionary<string, bool>();
        int At(string phase) => phases.ToList().IndexOf(phase);
        bool Has(string phase) => At(phase) >= 0;
        var airborne = Has("Climbing") || Has("Cruising") || Has("OnRoute") || Has("Holding") || Has("OnFinal");
        if (Has("TakingOff") && airborne)
            skills["takeoff"] = Has("Cruising") || Has("OnRoute") || Has("Holding") || Has("OnFinal") || result != "crashed";
        var landedWell = result == "landed" && goArounds == 0 && offCenterline is { } off && off <= GoodCenterline;
        if (Has("OnRoute"))
            skills["route"] = Has("OnFinal");
        if (Has("Holding"))
            skills["hold"] = (At("OnRoute") > At("Holding") || At("OnFinal") > At("Holding")) && result != "crashed";
        if (Has("OnFinal"))
        {
            var rnav = approach != null && approach.StartsWith("RNAV", StringComparison.OrdinalIgnoreCase);
            skills[rnav ? "land-rnav" : "land-visual"] = landedWell;
            if (Has("Holding") && rnav)
                skills["sequencing"] = landedWell;
        }
        return skills;
    }

    public FlightRecord AddFlight(string callSign, string craft, string result, string? approach, int goArounds, double minutes,
        double? downRunway, double? offCenterline, double? headingError, IReadOnlyList<string> phases, DateTime now)
    {
        var record = new FlightRecord(_nextFlight++, now, callSign, craft, result, approach, goArounds, minutes, downRunway, offCenterline,
            headingError, phases, Grade(result, approach, goArounds, offCenterline, phases));
        _flights.Add(record);
        if (_flights.Count > MaxFlights)
            _flights.RemoveRange(0, _flights.Count - MaxFlights);
        return record;
    }

    /// <summary>Good flights out of attempts for a skill: one agent's, or everyone's (null).</summary>
    public Proficiency Of(string skill, string? callSign = null)
    {
        var attempts = _flights.Where(f => f.Skills.ContainsKey(skill) && (callSign == null || f.CallSign.Equals(callSign, StringComparison.OrdinalIgnoreCase))).ToList();
        return new Proficiency(attempts.Count(f => f.Skills[skill]), attempts.Count);
    }

    /// <summary>Open to practise: everything it requires has been done well enough, by any agent on the server (shared).</summary>
    public bool Unlocked(string skill) =>
        Tree.First(s => s.Id == skill).Requires.All(r => Of(r).Good >= UnlockAfter);

    /// <summary>
    /// Adds a lesson, or confirms one that says the same. Refused (with why) without evidence: it must cite flights by
    /// the same agent that used the skill. Lessons are data shown to agents as facts, never as orders.
    /// </summary>
    public (Lesson? Lesson, string? Problem) AddLesson(string callSign, string skill, string text, IReadOnlyList<long> evidence, DateTime now)
    {
        text = text.Trim();
        if (Tree.All(s => s.Id != skill))
            return (null, $"no skill \"{skill}\" (skills: {string.Join(", ", Tree.Select(s => s.Id))})");
        if (text.Length is < 10 or > MaxLessonText)
            return (null, $"a lesson is 10 to {MaxLessonText} characters");
        var cited = evidence.Distinct().Take(10).Where(id => _flights.Any(f => f.Id == id && f.CallSign.Equals(callSign, StringComparison.OrdinalIgnoreCase) && f.Skills.ContainsKey(skill))).ToList();
        if (cited.Count == 0)
            return (null, $"a lesson must cite a flight of yours that used {skill} (its flight id)");

        var same = _lessons.Where(l => l.Skill == skill).OrderByDescending(l => Similarity(l.Text, text)).FirstOrDefault();
        if (same != null && Similarity(same.Text, text) >= 0.6)
        {
            var fresh = cited.Except(same.Evidence).ToList();
            var confirmed = same with
            {
                Evidence = same.Evidence.Concat(fresh).TakeLast(20).ToList(),
                Confirmations = same.Confirmations + (fresh.Count > 0 ? 1 : 0),
                Confirmed = now,
            };
            _lessons[_lessons.IndexOf(same)] = confirmed;
            return (confirmed, null);
        }
        var lesson = new Lesson(_nextLesson++, skill, text, callSign, now, now, cited, 0);
        _lessons.Add(lesson);
        // Too many for a skill or overall: the weakest, oldest goes.
        foreach (var group in new[] { _lessons.Where(l => l.Skill == skill).ToList() })
            if (group.Count > MaxLessonsPerSkill)
                _lessons.Remove(group.OrderBy(l => l.Weight).ThenBy(l => l.Confirmed).First());
        if (_lessons.Count > MaxLessons)
            _lessons.Remove(_lessons.OrderBy(l => l.Weight).ThenBy(l => l.Confirmed).First());
        return (lesson, null);
    }

    /// <summary>Word overlap (Jaccard) of two lessons, 0..1: "the same lesson" from 0.6.</summary>
    public static double Similarity(string a, string b)
    {
        static HashSet<string> Words(string s) => s.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '!', '?', '(', ')', '"'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
        var x = Words(a);
        var y = Words(b);
        return x.Count + y.Count == 0 ? 0 : (double)x.Intersect(y).Count() / x.Union(y).Count();
    }

    public bool Delete(long lessonId) => _lessons.RemoveAll(l => l.Id == lessonId) > 0;

    /// <summary>Drops lessons nobody confirmed for <see cref="Expiry"/>.</summary>
    public void Expire(DateTime now) => _lessons.RemoveAll(l => l.Confirmations == 0 && now - l.Confirmed > Expiry);

    /// <summary>A skill's best lessons, for an agent to read before it tries it.</summary>
    public IReadOnlyList<Lesson> For(string skill, int max = 5) =>
        _lessons.Where(l => l.Skill == skill).OrderByDescending(l => l.Weight).ThenByDescending(l => l.Confirmed).Take(max).ToList();

    private sealed record Saved(int Version, long NextFlight, long NextLesson, List<FlightRecord> Flights, List<Lesson> Lessons);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public const long MaxFileBytes = 20L * 1024 * 1024;

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new Saved(1, _nextFlight, _nextLesson, _flights, _lessons), Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The saved knowledge, or an empty one when there's no file (or it can't be read: it's kept as .bad).</summary>
    public static Knowledge Load(string path)
    {
        var knowledge = new Knowledge();
        if (!File.Exists(path))
            return knowledge;
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
                throw new JsonException("too big");
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path), Json) ?? throw new JsonException("empty");
            knowledge._flights.AddRange((saved.Flights ?? []).Where(f => f is { CallSign: not null, Skills: not null, Phases: not null }).TakeLast(MaxFlights));
            knowledge._lessons.AddRange((saved.Lessons ?? []).Where(l => l is { Text: not null, Skill: not null, Evidence: not null } && Tree.Any(s => s.Id == l.Skill)).Take(MaxLessons));
            knowledge._nextFlight = Math.Max(saved.NextFlight, knowledge._flights.Select(f => f.Id).DefaultIfEmpty(0).Max() + 1);
            knowledge._nextLesson = Math.Max(saved.NextLesson, knowledge._lessons.Select(l => l.Id).DefaultIfEmpty(0).Max() + 1);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or IOException)
        {
            try { File.Copy(path, path + ".bad", overwrite: true); } catch (IOException) { }
            return new Knowledge();
        }
        return knowledge;
    }
}
