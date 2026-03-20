namespace mi_ferreteria.Helpers
{
    public static class ValidationConstants
    {
        // Permite letras (incluyendo acentos), espacios, apóstrofes y guiones.
        public const string NombreSoloLetrasPattern = @"^[A-Za-zÁÉÍÓÚáéíóúÑñÜü' -]+$";
    }
}
