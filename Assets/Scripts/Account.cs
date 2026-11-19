using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// Local player accounts for Emberclad.
///
/// Credentials live in a JSON file under Application.persistentDataPath. The password is
/// never stored: each account keeps a random per-account salt plus a PBKDF2-SHA256 hash,
/// and signing in re-derives the hash and compares it. That protects the password if the
/// file is read or shared, but the file still sits on the player's own machine, so this
/// is local gatekeeping, not server-grade authentication.
public static class Account
{
    public const int MinUserLength = 3, MaxUserLength = 16, MinPassLength = 8;
    const int SaltBytes = 16, HashBytes = 32, Iterations = 100000;
    const string FileName = "accounts.json";

    [Serializable]
    class Record
    {
        public string user;       // as typed, for display
        public string key;        // lowercased username, the lookup key
        public string salt;       // base64
        public string hash;       // base64
        public int iterations;    // kept per record so the cost can be raised later
        public string created;    // ISO-8601 UTC
    }

    [Serializable]
    class Store { public List<Record> accounts = new List<Record>(); }

    static Store store;

    public static string SignedInUser { get; private set; }
    public static bool SignedIn { get { return !string.IsNullOrEmpty(SignedInUser); } }
    public static string StoragePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }
    public static int Count { get { Load(); return store.accounts.Count; } }
    public static bool AnyAccounts { get { return Count > 0; } }

    // ---- persistence ----------------------------------------------------------------

    static void Load()
    {
        if (store != null) return;
        try
        {
            string path = StoragePath;
            if (File.Exists(path)) store = JsonUtility.FromJson<Store>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            // A corrupt or unreadable file must not lock the player out of the game.
            Debug.LogWarning("Emberclad: accounts file unreadable, starting a fresh one. " + e.Message);
            store = null;
        }
        if (store == null) store = new Store();
        if (store.accounts == null) store.accounts = new List<Record>();
    }

    static void Save()
    {
        string path = StoragePath;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, JsonUtility.ToJson(store));
            // Write to a sibling then swap, so an interrupted save cannot truncate the real file.
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogError("Emberclad: could not save accounts. " + e.Message);
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    static Record Find(string user)
    {
        Load();
        string key = Key(user);
        for (int i = 0; i < store.accounts.Count; i++)
            if (store.accounts[i] != null && store.accounts[i].key == key) return store.accounts[i];
        return null;
    }

    static string Key(string user) { return (user ?? string.Empty).Trim().ToLowerInvariant(); }

    // ---- validation -----------------------------------------------------------------

    public static bool ValidateUsername(string user, out string error)
    {
        user = (user ?? string.Empty).Trim();
        if (user.Length == 0) { error = "Enter a username."; return false; }
        if (user.Length < MinUserLength) { error = "Username needs at least " + MinUserLength + " characters."; return false; }
        if (user.Length > MaxUserLength) { error = "Username can be at most " + MaxUserLength + " characters."; return false; }
        for (int i = 0; i < user.Length; i++)
        {
            char c = user[i];
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) { error = "Letters, numbers and underscore only."; return false; }
        }
        error = null;
        return true;
    }

    public static bool ValidatePassword(string pass, out string error)
    {
        if (string.IsNullOrEmpty(pass)) { error = "Enter a password."; return false; }
        if (pass.Length < MinPassLength) { error = "Password needs at least " + MinPassLength + " characters."; return false; }
        error = null;
        return true;
    }

    // ---- create / sign in -----------------------------------------------------------

    public static bool TryCreate(string user, string pass, string confirm, out string error)
    {
        if (!ValidateUsername(user, out error)) return false;
        if (!ValidatePassword(pass, out error)) return false;
        if (pass != confirm) { error = "Passwords do not match."; return false; }
        if (Find(user) != null) { error = "That username is taken."; return false; }

        user = user.Trim();
        byte[] salt = new byte[SaltBytes];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

        var record = new Record
        {
            user = user,
            key = Key(user),
            salt = Convert.ToBase64String(salt),
            hash = Convert.ToBase64String(Derive(pass, salt, Iterations)),
            iterations = Iterations,
            created = DateTime.UtcNow.ToString("o")
        };
        store.accounts.Add(record);
        Save();
        SignedInUser = user;
        error = null;
        return true;
    }

    public static bool TrySignIn(string user, string pass, out string error)
    {
        var record = Find(user);
        if (record == null || string.IsNullOrEmpty(pass))
        {
            // Same message either way, so this cannot be used to enumerate usernames.
            error = "Username or password is incorrect.";
            return false;
        }
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(record.salt);
            expected = Convert.FromBase64String(record.hash);
        }
        catch { error = "That account's data is damaged."; return false; }

        int iterations = record.iterations > 0 ? record.iterations : Iterations;
        if (!FixedTimeEquals(Derive(pass, salt, iterations), expected))
        {
            error = "Username or password is incorrect.";
            return false;
        }
        SignedInUser = record.user;
        error = null;
        return true;
    }

    public static void SignOut() { SignedInUser = null; }

    /// Only for tests and for a deliberate "forget me" action; removes the stored account.
    public static bool Delete(string user)
    {
        var record = Find(user);
        if (record == null) return false;
        store.accounts.Remove(record);
        Save();
        if (SignedIn && Key(SignedInUser) == record.key) SignedInUser = null;
        return true;
    }

    // ---- hashing --------------------------------------------------------------------

    static byte[] Derive(string pass, byte[] salt, int iterations)
    {
        using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pass ?? string.Empty), salt, iterations, HashAlgorithmName.SHA256))
            return kdf.GetBytes(HashBytes);
    }

    static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
