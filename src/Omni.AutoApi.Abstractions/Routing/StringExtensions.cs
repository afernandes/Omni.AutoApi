using System;
using System.Text.RegularExpressions;

namespace Omni.AutoApi.Routing
{
    internal static class StringExtensions
    {
        public static string RemovePostfix(this string? str, params string[] postfixes)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var value = str!;

            foreach (var postfix in postfixes)
            {
                if (value.EndsWith(postfix))
                {
                    return value.Substring(0, value.Length - postfix.Length);
                }
            }

            return value;
        }

        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.Length == 1)
            {
                return str.ToLowerInvariant();
            }

            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        public static string ToKebabCase(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            str = str.ToCamelCase();

            return Regex.Replace(str, "[a-z][A-Z]", m => m.Value[0] + "-" + char.ToLowerInvariant(m.Value[1]));
        }
    }
}
