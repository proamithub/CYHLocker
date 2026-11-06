using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;

namespace CYHLocker.Logic.Services;

public class PasswordGeneratorService : IPasswordGeneratorService
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits    = "0123456789";
    private const string Symbols   = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    private const string Ambiguous = "0O1lI|";

    public GeneratedPasswordDto GeneratePassword(PasswordGeneratorOptionsDto options)
    {
        var length = Math.Clamp(options.Length, 8, 128);
        var charPool = new StringBuilder();

        var allowedLower = options.AvoidAmbiguous ? FilterAmbiguous(Lowercase) : Lowercase;
        var allowedUpper = options.AvoidAmbiguous ? FilterAmbiguous(Uppercase) : Uppercase;
        var allowedDigits = options.AvoidAmbiguous ? FilterAmbiguous(Digits) : Digits;
        var allowedSymbols = options.AvoidAmbiguous ? FilterAmbiguous(Symbols) : Symbols;

        var guaranteedChars = new List<char>();

        if (options.IncludeLowercase && allowedLower.Length > 0)
        {
            charPool.Append(allowedLower);
            guaranteedChars.Add(GetRandomChar(allowedLower));
        }
        if (options.IncludeUppercase && allowedUpper.Length > 0)
        {
            charPool.Append(allowedUpper);
            guaranteedChars.Add(GetRandomChar(allowedUpper));
        }
        if (options.IncludeNumbers && allowedDigits.Length > 0)
        {
            charPool.Append(allowedDigits);
            guaranteedChars.Add(GetRandomChar(allowedDigits));
        }
        if (options.IncludeSymbols && allowedSymbols.Length > 0)
        {
            charPool.Append(allowedSymbols);
            guaranteedChars.Add(GetRandomChar(allowedSymbols));
        }

        if (charPool.Length == 0)
        {
            charPool.Append(Lowercase);
            guaranteedChars.Add(GetRandomChar(Lowercase));
        }

        var pool = charPool.ToString();
        var resultChars = new List<char>(guaranteedChars);

        while (resultChars.Count < length)
        {
            resultChars.Add(GetRandomChar(pool));
        }

        // Shuffle with Fisher-Yates
        Shuffle(resultChars);
        var password = new string(resultChars.ToArray());

        var strength = EvaluateStrength(password);
        var poolSize = pool.Length;
        var entropy = length * Math.Log2(poolSize > 0 ? poolSize : 26);

        return new GeneratedPasswordDto
        {
            Password = password,
            Length = length,
            Strength = strength,
            EntropyBits = Math.Round(entropy, 1)
        };
    }

    public string EvaluateStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return "Weak";
        if (password.Length < 8) return "Weak";

        int score = 0;
        if (password.Length >= 12) score += 2;
        else if (password.Length >= 8) score += 1;

        if (password.Length >= 16) score += 1;

        if (Regex.IsMatch(password, @"[a-z]")) score += 1;
        if (Regex.IsMatch(password, @"[A-Z]")) score += 1;
        if (Regex.IsMatch(password, @"[0-9]")) score += 1;
        if (Regex.IsMatch(password, @"[^a-zA-Z0-9]")) score += 2;

        return score switch
        {
            <= 3 => "Weak",
            4 or 5 => "Fair",
            6 or 7 => "Strong",
            _ => "Very Strong"
        };
    }

    private static string FilterAmbiguous(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (!Ambiguous.Contains(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private static char GetRandomChar(string pool)
    {
        var index = RandomNumberGenerator.GetInt32(0, pool.Length);
        return pool[index];
    }

    private static void Shuffle(List<char> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
