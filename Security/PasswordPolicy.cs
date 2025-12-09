using System.Linq;
using System.Text.RegularExpressions;

namespace mi_ferreteria.Security
{
    public static class PasswordPolicy
    {
        private static readonly Regex UpperCase = new Regex("[A-Z]", RegexOptions.Compiled);
        private static readonly Regex LowerCase = new Regex("[a-z]", RegexOptions.Compiled);
        private static readonly Regex Digit = new Regex("[0-9]", RegexOptions.Compiled);
        private static readonly Regex Special = new Regex("[^a-zA-Z0-9]", RegexOptions.Compiled);

        public static bool IsStrong(string? password, out string message)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                message = "La contrasena es obligatoria.";
                return false;
            }

            if (password.Length < 10)
            {
                message = "La contrasena debe tener al menos 10 caracteres.";
                return false;
            }

            if (!UpperCase.IsMatch(password))
            {
                message = "Debe incluir al menos una mayuscula.";
                return false;
            }

            if (!LowerCase.IsMatch(password))
            {
                message = "Debe incluir al menos una minuscula.";
                return false;
            }

            if (!Digit.IsMatch(password))
            {
                message = "Debe incluir al menos un numero.";
                return false;
            }

            if (!Special.IsMatch(password))
            {
                message = "Debe incluir al menos un caracter especial.";
                return false;
            }

            if (HasRepeatedChars(password))
            {
                message = "Evita contrasenas con el mismo caracter repetido.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool HasRepeatedChars(string password)
        {
            return password.GroupBy(c => c).Any(g => g.Count() > password.Length / 2);
        }
    }
}
