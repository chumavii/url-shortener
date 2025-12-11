using System.Text;

namespace Utilities
{
    public static class Base62Helper
    {
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static readonly int RadixBase = Alphabet.Length;

        public static string Encode(long number)
        {
            if (number < 0)
                throw new ArgumentException("value must be greater or equal to 0", nameof(number));

            if (number == 0)
                return Alphabet[0].ToString();

            var sb = new StringBuilder();

            while (number > 0)
            {
                sb.Append(Alphabet[(int)(number % RadixBase)]);
                number /= RadixBase;
            }

            var arr = sb.ToString().ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        public static long Decode(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Input empty", nameof(input));

            long result = 0;

            foreach (char c in input)
            {
                int digit = Alphabet.IndexOf(c);
                if (digit < 0) throw new FormatException($"Invalid character '{c}' for Base62.");
                checked { result = result * RadixBase + digit; }
            }

            return result;
        }
    }
}
