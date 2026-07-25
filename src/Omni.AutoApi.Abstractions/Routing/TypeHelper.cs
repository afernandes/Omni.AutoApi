namespace Omni.AutoApi.Routing
{
    /// <summary>
    /// Classifica tipos "simples" (que viajam na query string) de forma robusta,
    /// incluindo <see cref="System.Nullable{T}"/> e os tipos de data/hora modernos —
    /// espelhando <c>Volo.Abp.TypeHelper.IsPrimitiveExtended</c> do ABP.
    /// </summary>
    public static class TypeHelper
    {
        public static bool IsPrimitiveExtended(Type type, bool includeEnums = false)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType.IsPrimitive)
            {
                return true;
            }

            if (includeEnums && actualType.IsEnum)
            {
                return true;
            }

            return actualType == typeof(string)
                || actualType == typeof(decimal)
                || actualType == typeof(DateTime)
                || actualType == typeof(DateTimeOffset)
                || actualType == typeof(DateOnly)
                || actualType == typeof(TimeOnly)
                || actualType == typeof(TimeSpan)
                || actualType == typeof(Guid);
        }
    }
}
