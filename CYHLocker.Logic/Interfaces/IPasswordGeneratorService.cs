using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IPasswordGeneratorService
{
    GeneratedPasswordDto GeneratePassword(PasswordGeneratorOptionsDto options);
    string EvaluateStrength(string password);
}
