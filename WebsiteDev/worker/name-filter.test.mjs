// node name-filter.test.mjs
import { bannedTerm } from './name-filter.js';

const shouldBlock = [
  'nigga', 'nigger', 'n1gg3r', 'n i g g a', 'NIIIGGA', 'nìggér', 'wigger', 'coon',
  'faggot', 'f4gg0t', 'fag', 'tranny', 'retard', 'chink', 'spic', 'paki', 'kike',
  'hitler', 'nazi', 'KKK', 'heil', 'whitepower',
  'fuck', 'fuuuck', 'f-u-c-k', 'shit', 'cunt', 'bitch', 'whore', 'slut',
  'cock', 'dick', 'pussy', 'anal', 'asshole', 'bigass', 'tits', 'cumshot',
  'porn', 'hentai', 'sexy', 'rape', 'rapist', 'pedo', 'molest',
  'piss', 'turd', 'twat', 'kys', 'killyourself',
  'admin', 'Moderator', 'Emberclad', 'Niger',
];

const shouldPass = [
  'Pvack', 'Knight', 'Ash', 'test', 'Ember Knight', 'Bone-Warden', 'Sir_Cinder',
  'Assassin', 'Grassland', 'Classic', 'Ambassador', 'Compass', 'Cassandra',
  'Analyst', 'Canal', 'Arsenal', 'Uranus', 'Titanium', 'Titan',
  'Cucumber', 'Document', 'Vacuum', 'Cumin',
  'Grapefruit', 'Drapery', 'Cocktail', 'Peacock', 'Hitchcock', 'Dickens',
  'Shiitake', 'Spicy', 'Suspicious', 'Sparse', 'Coarse', 'Marseille',
  'Scunthorpe', 'Penistone', 'Montenegro', 'Nigerian', 'Nigeria',
  'Homonym', 'Japanese', 'Pakistani', 'Sussex', 'Sexton', 'Saturday',
  'Prickly', 'Retardant', 'Vandyke', 'Cocoon', 'Count', 'Countess',
  'Keys', 'Mississippi', 'Happiness', 'Tyranny', 'Condor', 'Second', 'Wangle',
];

let bad = 0;
for (const n of shouldBlock) {
  const t = bannedTerm(n);
  if (!t) { console.log('MISS  (should block) ' + JSON.stringify(n)); bad++; }
}
for (const n of shouldPass) {
  const t = bannedTerm(n);
  if (t) { console.log('FALSE POSITIVE ' + JSON.stringify(n) + ' -> ' + t); bad++; }
}
console.log(bad === 0
  ? `ok: ${shouldBlock.length} blocked, ${shouldPass.length} allowed`
  : `${bad} problem(s)`);
process.exit(bad === 0 ? 0 : 1);
