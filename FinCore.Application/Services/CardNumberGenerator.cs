using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FinCore.Application.Services
{
    public static class CardNumberGenerator
    {

        public static string Generate()
        {
            var digits = new int[16];
            digits[0] = 9;
            for (var i = 1; i < 15; i++)
                digits[i] = RandomNumberGenerator.GetInt32(10);

            digits[15] = LuhnCheckDigit(digits.AsSpan(0, 15));
            return string.Join(string.Empty, digits);
        }

        private static int LuhnCheckDigit(ReadOnlySpan<int> payload)
        {
            var sum = 0;
            var doubleIt = true;                       
            for (var i = payload.Length - 1; i >= 0; i--)
            {
                var n = payload[i];
                if (doubleIt) { n *= 2; if (n > 9) n -= 9; }
                sum += n;
                doubleIt = !doubleIt;
            }
            return (10 - sum % 10) % 10;
        }
    }
}
