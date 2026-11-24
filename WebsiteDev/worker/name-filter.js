// Nickname moderation for Emberclad.
//
// A name is rejected if it contains a slur, sexual term, hate reference or reserved word,
// even when it is disguised. Matching is deliberately loose:
//   * accents are stripped, so "nìggér" reads as "nigger"
//   * leetspeak is folded, so "f4gg0t" and "n1gg3r" read as letters
//   * each letter may repeat, so "fuuuck" and "shiiiit" match
//   * non-letters between letters are ignored, so "f-u-c-k" and "a s s" match
// Loose matching creates false positives ("assassin" contains "ass"), so ALLOW lists the
// innocent words that contain a banned one: they are blanked out of the name before the
// banned list is applied. ALLOW cannot rescue a word that folds onto a slur exactly —
// "Niger" folds the same way as "nigger", so it is blocked on purpose.
//
// This is a floor, not a guarantee: it stops casual abuse, not a determined speedrunner.
// Keep it in step with Assets/Scripts/NameFilter.cs, which shows the player the same refusal
// before the request is even sent.

const LEET = {
  '0': 'o', '1': 'i', '!': 'i', '|': 'i', '¡': 'i',
  '3': 'e', '€': 'e', '4': 'a', '@': 'a', '5': 's', '$': 's',
  '6': 'g', '9': 'g', '7': 't', '+': 't', '8': 'b', '2': 'z', '£': 'l',
};

// Slurs, sexual terms, hate references and words reserved for the site.
const BANNED = [
  // racial and ethnic slurs
  'nigger', 'nigga', 'niger', 'niglet', 'wigger', 'negro', 'coon', 'jigaboo', 'groid',
  'chink', 'gook', 'jap', 'slanteye', 'zipperhead', 'spic', 'beaner', 'wetback',
  'kike', 'heeb', 'paki', 'pajeet', 'currymuncher', 'towelhead', 'raghead', 'mudslime',
  'redskin', 'injun', 'tarbaby',
  // homophobic and transphobic slurs
  'faggot', 'fag', 'dyke', 'tranny', 'shemale', 'homo',
  // ableist slurs
  'retard', 'mongoloid',
  // hate movements
  'hitler', 'nazi', 'heil', 'kkk', 'klansman', 'whitepower', 'swastika',
  // sexual content
  'fuck', 'shit', 'cunt', 'bitch', 'bastard', 'whore', 'slut', 'skank', 'douche',
  'cock', 'dick', 'penis', 'vagina', 'pussy', 'anal', 'anus', 'arse', 'asshole', 'ass',
  'cum', 'jizz', 'semen', 'boobs', 'tits', 'titties', 'titty', 'nipple',
  'blowjob', 'handjob', 'rimjob', 'deepthroat', 'bukkake', 'cumshot', 'creampie',
  'gangbang', 'orgasm', 'masturbate', 'wank', 'jerkoff', 'dildo', 'buttplug', 'fisting',
  'porn', 'hentai', 'milf', 'dilf', 'bdsm', 'sex', 'incest',
  // abuse and exploitation
  'rape', 'rapist', 'molest', 'pedo', 'pedophile', 'lolicon', 'jailbait',
  'bestiality', 'zoophilia', 'necrophilia',
  // crude
  'piss', 'turd', 'twat', 'prick', 'bollocks', 'wanker',
  // self-harm
  'kys', 'killyourself',
  // reserved for the site
  'admin', 'administrator', 'moderator', 'emberclad',
];

// Innocent words that contain a banned one. Blanked out before the banned list is applied.
const ALLOW = [
  // ass
  'assassin', 'assault', 'assembly', 'assemble', 'assert', 'assess', 'asset', 'assign',
  'assist', 'associate', 'assume', 'assure', 'ambassador', 'bass', 'brass', 'cassandra',
  'class', 'classic', 'compass', 'embassy', 'glass', 'grass', 'harass', 'hassle', 'lass',
  'mass', 'massive', 'molasses', 'morass', 'pass', 'passage', 'passive', 'potassium',
  'surpass', 'trespass',
  // anal, anus
  'analysis', 'analyst', 'analog', 'analogue', 'banal', 'canal', 'arsenal', 'janus', 'uranus',
  // cum
  'accumulate', 'circumstance', 'cucumber', 'cumberland', 'cumbria', 'cumin', 'cumulative',
  'cumulus', 'document', 'incumbent', 'succumb', 'vacuum',
  // tits
  'titan', 'titanic', 'titanium',
  // rape
  'grape', 'grapes', 'grapefruit', 'drape', 'drapes', 'drapery',
  // cock
  'cocktail', 'cockpit', 'cockney', 'cockroach', 'peacock', 'shuttlecock', 'woodcock', 'hitchcock',
  // dick
  'dickens', 'dickinson', 'dickson',
  // shit
  'shiitake', 'shitake',
  // spic
  'spice', 'spices', 'spicy', 'auspicious', 'conspicuous', 'despicable', 'suspicious',
  // arse
  'coarse', 'hoarse', 'parse', 'sparse', 'arsenic', 'marseille',
  // penis, cunt
  'penistone', 'scunthorpe', 'clitheroe',
  // negro
  'montenegro', 'rionegro',
  // niger (the slur folds onto this too, so only the longer forms are rescued)
  'nigeria', 'nigerian', 'nigerien',
  // homo
  'homogeneous', 'homogenize', 'homonym', 'homophone', 'homer',
  // jap, paki
  'japan', 'japanese', 'pakistan', 'pakistani',
  // sex
  'sussex', 'essex', 'middlesex', 'sexton', 'sextant', 'sextet',
  // turd
  'saturday',
  // prick
  'prickly', 'prickle',
  // retard
  'retardant',
  // fag, dyke, coon
  'fagin', 'vandyke', 'cocoon',
];

function fold(raw) {
  const stripped = (raw || '').normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase();
  return Array.from(stripped).map(c => (c in LEET ? LEET[c] : c)).join('');
}

// "fuck" -> /f+[^a-z]*u+[^a-z]*c+[^a-z]*k+/ : repeats and padding are ignored.
function loose(term, flags) {
  const body = Array.from(term).map(c => c + '+').join('[^a-z]*');
  return new RegExp(body, flags);
}

const BANNED_RE = BANNED.map(term => ({ term, re: loose(term, '') }));
const ALLOW_RE = ALLOW.slice().sort((a, b) => b.length - a.length).map(term => loose(term, 'g'));

/// The banned term a name matches, or null if the name is fine.
export function bannedTerm(nickname) {
  let probe = fold(nickname);
  for (const re of ALLOW_RE) probe = probe.replace(re, ' ');
  for (const { term, re } of BANNED_RE) if (re.test(probe)) return term;
  return null;
}

/// The message to show the player, or null if the name is fine.
export function nameRefusal(nickname) {
  return bannedTerm(nickname) ? 'That name is not allowed. Choose another.' : null;
}
