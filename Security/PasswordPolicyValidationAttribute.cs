using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.Security
{
    /// <summary>
    /// DataAnnotations attribute que reutiliza PasswordPolicy para mantener un único punto de validación.
    /// </summary>
    public class PasswordPolicyValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var password = value as string;
            if (PasswordPolicy.IsStrong(password, out var message))
            {
                return ValidationResult.Success;
            }

            // Usa el mensaje específico de la política para que el usuario sepa qué regla falló.
            return new ValidationResult(message);
        }
    }
}
