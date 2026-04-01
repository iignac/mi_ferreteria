namespace mi_ferreteria.Helpers
{
    public static class ValidationConstants
    {
        // Permite letras (incluyendo acentos), espacios, apóstrofes y guiones.
        public const string NombreSoloLetrasPattern = @"^[A-Za-zÁÉÍÓÚáéíóúÑñÜü' -]+$";

        // Unidades de medida válidas para productos. Fuente única compartida entre ProductoController y ProductoApiController.
        public static readonly string[] UnidadesPermitidas = new[]
        {
            "unidad", "gramos", "kilos", "metros cuadrados", "juego", "bolsa",
            "placa", "rollo", "litro", "mililitro", "bidon", "kit", "par"
        };
    }
}
