using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FinCore.Application.Services
{
    public static class AccountNumberGenerator
    {
        public static string Generate()
        {
            Span<char> digits = stackalloc char[16];
            for (var i = 0; i < digits.Length; i++)
                digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));

            return "FC" + new string(digits);
        }
    }
}
