using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// Nickname moderation, shown on the name page before the request leaves the game.
///
/// This mirrors WebsiteDev/worker/name-filter.js, which is the one that counts: the site
/// re-checks every name, so a patched client gains nothing. Keep the two lists in step.
///
/// Matching is loose on purpose: accents are stripped, leetspeak is folded, letters may
/// repeat and anything that is not a letter is ignored, so "n1gg3r", "f-u-c-k" and
/// "fuuuck" all match. That catches innocent words too ("assassin" contains "ass"), so
/// Allow lists those and blanks them out of the name before Banned is applied.
public static class NameFilter
{
    static readonly Dictionary<char, char> Leet = new Dictionary<char, char>
    {
        { '0', 'o' }, { '1', 'i' }, { '!', 'i' }, { '|', 'i' }, { '¡', 'i' },
        { '3', 'e' }, { '€', 'e' }, { '4', 'a' }, { '@', 'a' }, { '5', 's' }, { '$', 's' },
        { '6', 'g' }, { '9', 'g' }, { '7', 't' }, { '+', 't' }, { '8', 'b' }, { '2', 'z' }, { '£', 'l' },
    };

    // Slurs, sexual terms, hate references and words reserved for the site.
    static readonly string[] Banned =
    {
        // racial and ethnic slurs
        "nigger", "nigga", "niger", "niglet", "wigger", "negro", "coon", "jigaboo", "groid",
        "chink", "gook", "jap", "slanteye", "zipperhead", "spic", "beaner", "wetback",
        "kike", "heeb", "paki", "pajeet", "currymuncher", "towelhead", "raghead", "mudslime",
        "redskin", "injun", "tarbaby",
        // homophobic and transphobic slurs
        "faggot", "fag", "dyke", "tranny", "shemale", "homo",
        // ableist slurs
        "retard", "mongoloid",
        // hate movements
        "hitler", "nazi", "heil", "kkk", "klansman", "whitepower", "swastika",
        // sexual content
        "fuck", "shit", "cunt", "bitch", "bastard", "whore", "slut", "skank", "douche",
        "cock", "dick", "penis", "vagina", "pussy", "anal", "anus", "arse", "asshole", "ass",
        "cum", "jizz", "semen", "boobs", "tits", "titties", "titty", "nipple",
        "blowjob", "handjob", "rimjob", "deepthroat", "bukkake", "cumshot", "creampie",
        "gangbang", "orgasm", "masturbate", "wank", "jerkoff", "dildo", "buttplug", "fisting",
        "porn", "hentai", "milf", "dilf", "bdsm", "sex", "incest",
        // abuse and exploitation
        "rape", "rapist", "molest", "pedo", "pedophile", "lolicon", "jailbait",
        "bestiality", "zoophilia", "necrophilia",
        // crude
        "piss", "turd", "twat", "prick", "bollocks", "wanker",
        // self-harm
        "kys", "killyourself",
        // reserved for the site
        "admin", "administrator", "moderator", "emberclad",
    };

    // Innocent words that contain a banned one. Blanked out before Banned is applied.
    static readonly string[] Allow =
    {
        "assassin", "assault", "assembly", "assemble", "assert", "assess", "asset", "assign",
        "assist", "associate", "assume", "assure", "ambassador", "bass", "brass", "cassandra",
        "class", "classic", "compass", "embassy", "glass", "grass", "harass", "hassle", "lass",
        "mass", "massive", "molasses", "morass", "pass", "passage", "passive", "potassium",
        "surpass", "trespass",
        "analysis", "analyst", "analog", "analogue", "banal", "canal", "arsenal", "janus", "uranus",
        "accumulate", "circumstance", "cucumber", "cumberland", "cumbria", "cumin", "cumulative",
        "cumulus", "document", "incumbent", "succumb", "vacuum",
        "titan", "titanic", "titanium",
        "grape", "grapes", "grapefruit", "drape", "drapes", "drapery",
        "cocktail", "cockpit", "cockney", "cockroach", "peacock", "shuttlecock", "woodcock", "hitchcock",
        "dickens", "dickinson", "dickson",
        "shiitake", "shitake",
        "spice", "spices", "spicy", "auspicious", "conspicuous", "despicable", "suspicious",
        "coarse", "hoarse", "parse", "sparse", "arsenic", "marseille",
        "penistone", "scunthorpe", "clitheroe",
        "montenegro", "rionegro",
        "nigeria", "nigerian", "nigerien",
        "homogeneous", "homogenize", "homonym", "homophone", "homer",
        "japan", "japanese", "pakistan", "pakistani",
        "sussex", "essex", "middlesex", "sexton", "sextant", "sextet",
        "saturday",
        "prickly", "prickle",
        "retardant",
        "fagin", "vandyke", "cocoon",
    };

    static Regex[] bannedRe, allowRe;

    static void Build()
    {
        if (bannedRe != null) return;
        bannedRe = new Regex[Banned.Length];
        for (int i = 0; i < Banned.Length; i++) bannedRe[i] = Loose(Banned[i]);
        // Longest first, so a long innocent word is removed before a shorter one eats part of it.
        var allow = new List<string>(Allow);
        allow.Sort((a, b) => b.Length.CompareTo(a.Length));
        allowRe = new Regex[allow.Count];
        for (int i = 0; i < allow.Count; i++) allowRe[i] = Loose(allow[i]);
    }

    /// "fuck" -> f+[^a-z]*u+[^a-z]*c+[^a-z]*k+ : repeats and padding are ignored.
    static Regex Loose(string term)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < term.Length; i++)
        {
            if (i > 0) sb.Append("[^a-z]*");
            sb.Append(term[i]).Append('+');
        }
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant);
    }

    static string Fold(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string flat = raw;
        try
        {
            var d = raw.Normalize(NormalizationForm.FormD);
            var keep = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) keep.Append(c);
            flat = keep.ToString();
        }
        catch { /* WebGL without full ICU: fall back to the raw name */ }

        var sb = new StringBuilder(flat.Length);
        foreach (char c in flat.ToLowerInvariant())
            sb.Append(Leet.TryGetValue(c, out char mapped) ? mapped : c);
        return sb.ToString();
    }

    /// The banned term the name matches, or null if the name is fine.
    public static string BannedTerm(string nickname)
    {
        Build();
        string probe = Fold(nickname);
        foreach (var re in allowRe) probe = re.Replace(probe, " ");
        for (int i = 0; i < bannedRe.Length; i++) if (bannedRe[i].IsMatch(probe)) return Banned[i];
        return null;
    }

    /// False with the message to show the player when the name can't be used.
    public static bool IsAllowed(string nickname, out string error)
    {
        if (BannedTerm(nickname) != null) { error = "That name is not allowed. Choose another."; return false; }
        error = null;
        return true;
    }
}
