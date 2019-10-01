using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    //https://www.nayuki.io/res/forcing-a-files-crc-to-any-value
    //https://github.com/google/proto-quic/blob/master/depot_tools/external_bin/gsutil/gsutil_4.15/gsutil/third_party/crcmod/python3/crcmod/predefined.py

    internal class CrcForce
    {
        private static long POLYNOMIAL = 0x104C11DB7L; // Generator polynomial. Do not modify, because there are many dependencies


        public static uint Calculate(uint crc, long length, uint newCrc, long offset, uint existingOffsetValue)
        {
            newCrc = reverseBits(newCrc);
            crc = reverseBits(crc);
            uint delta = crc ^ newCrc;
            delta = (uint)multiplyMod(reciprocalMod(powMod(2, (length - offset) * 8)), delta & 0xFFFFFFFFL);

            uint result = existingOffsetValue ^ reverseBits(delta);
            return swapBytes(result);
        }

        private static uint swapBytes(uint x)
        {
            x = (x >> 16) | (x << 16);
            return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
        }

        /*---- Polynomial arithmetic ----*/

        // Returns polynomial x multiplied by polynomial y modulo the generator polynomial.
        private static long multiplyMod(long x, long y)
        {
            // Russian peasant multiplication algorithm
            long z = 0;
            while (y != 0)
            {
                z ^= x * (y & 1);
                y >>= 1;
                x <<= 1;
                if (((x >> 32) & 1) != 0)
                    x ^= POLYNOMIAL;
            }
            return z;
        }


        // Returns polynomial x to the power of natural number y modulo the generator polynomial.
        private static long powMod(long x, long y)
        {
            // Exponentiation by squaring
            long z = 1;
            while (y != 0)
            {
                if ((y & 1) != 0)
                    z = multiplyMod(z, x);
                x = multiplyMod(x, x);
                y >>= 1;
            }
            return z;
        }


        // Computes polynomial x divided by polynomial y, returning the quotient and remainder.
        private static long[] divideAndRemainder(long x, long y)
        {
            if (y == 0)
                throw new Exception("Division by zero");
            if (x == 0)
                return new long[] { 0, 0 };

            int ydeg = getDegree(y);
            long z = 0;
            for (int i = getDegree(x) - ydeg; i >= 0; i--)
            {
                if (((x >> (i + ydeg)) & 1) != 0)
                {
                    x ^= y << i;
                    z |= 1L << i;
                }
            }
            return new long[] { z, x };
        }


        // Returns the reciprocal of polynomial x with respect to the generator polynomial.
        private static long reciprocalMod(long x)
        {
            // Based on a simplification of the extended Euclidean algorithm
            long y = x;
            x = POLYNOMIAL;
            long a = 0;
            long b = 1;
            while (y != 0)
            {
                long[] divRem = divideAndRemainder(x, y);
                long c = a ^ multiplyMod(divRem[0], b);
                x = y;
                y = divRem[1];
                a = b;
                b = c;
            }
            if (x == 1)
                return a;
            else
                throw new Exception("Reciprocal does not exist");
        }


        private static int getDegree(long x)
        {
            return 63 - leadingZeros(x);
        }

        private static int leadingZeros(long value)
        {
            // Shift right unsigned to work with both positive and negative values
            long uValue = (long)value;
            int leadingZeros = 0;
            while (uValue != 0)
            {
                uValue = uValue >> 1;
                leadingZeros++;
            }

            return (64 - leadingZeros);
        }

        private static uint reverseBits(uint x)
        {
            uint result = 0;
            for (int i = 0; i < 32; i++)
                result = (result << 1) | ((x >> i) & 1U);
            return result;
        }
    }
}
