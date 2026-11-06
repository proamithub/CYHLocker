namespace CYHLocker.Data.DTOs;

public class PasswordGeneratorOptionsDto
{
    public int Length { get; set; } = 16;
    public bool IncludeUppercase { get; set; } = true;
    public bool IncludeLowercase { get; set; } = true;
    public bool IncludeNumbers { get; set; } = true;
    public bool IncludeSymbols { get; set; } = true;
    public bool AvoidAmbiguous { get; set; } = true; // Avoid 0, O, l, 1, I
}

public class GeneratedPasswordDto
{
    public string Password { get; set; } = string.Empty;
    public int Length { get; set; }
    public string Strength { get; set; } = "Strong";
    public double EntropyBits { get; set; }
}
