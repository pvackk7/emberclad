/// TEMPORARY TEST SWITCH -- not shipping code.
///
/// Flip On to false (or delete this file and the five `Cheats.On` references it
/// has in Actors.cs and Enemy.cs) to put the game back to normal. Nothing here
/// touches the save: the ember unlock is faked at the property, never written to
/// PlayerPrefs, so turning this off leaves progress exactly as it was.
public static class Cheats
{
    public static bool On = false;   // off at the user's request (2026-11-23)
}
