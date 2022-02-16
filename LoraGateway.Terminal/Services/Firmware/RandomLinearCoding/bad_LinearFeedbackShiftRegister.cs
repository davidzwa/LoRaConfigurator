// using System.Collections;
// using System.Security.Cryptography;
//
// namespace LoraGateway.Services.Firmware.RandomLinearCoding;
//
// /// <summary>
// /// https://github.com/xudonax/LFSR-RNG-CS
// /// </summary>
// public class LinearFeedbackShiftRegister : IEnumerable<ulong>
// {
//     private ulong _shiftRegister;
//     public readonly ulong Seed;
//
//     public LinearFeedbackShiftRegister()
//     {
//         do
//         {
//             using (var rng = new RNGCryptoServiceProvider())
//             {
//                 var randomBytes = new byte[8];
//                 rng.GetBytes(randomBytes);
//
//                 Seed = BitConverter.ToUInt64(randomBytes, 0);
//             }
//         } while (Seed == 0uL);
//
//         _shiftRegister = Seed;
//     }
//
//     public LinearFeedbackShiftRegister(ulong seed)
//     {
//         Seed = seed;
//
//         if (Seed == 0uL)
//         {
//             throw new ArgumentException(
//                 $"Seed cannot be `{seed}`. When in doubt, please use the parameterless constructor.", nameof(seed));
//         }
//
//         _shiftRegister = seed;
//     }
//
//     public IEnumerator<ulong> GetEnumerator()
//     {
//         do
//         {
//             // Get the bits at position 60, 61, 63 and 64:
//             var bit60 = (_shiftRegister & (1uL << 4 - 1)) != 0;
//             var bit61 = (_shiftRegister & (1uL << 3 - 1)) != 0;
//             var bit63 = (_shiftRegister & (1uL << 1 - 1)) != 0;
//             var bit64 = (_shiftRegister & (1uL << 0 - 1)) != 0;
//
//             // XOR 64 with 63, that with 61 and that with 60
//             var immediate1 = bit64 ^ bit63;
//             var immediate2 = immediate1 ^ bit61;
//             var immediate3 = immediate2 ^ bit60;
//             var shiftMeIn = (immediate3 ^ bit60 ? 1uL : 0uL) & 1;
//
//             // Shift the bit into shiftRegister
//             _shiftRegister = (_shiftRegister >> 1) | (shiftMeIn << 63);
//
//             // Let's see what the period is
//             Period++;
//
//             yield return _shiftRegister;
//         } while (_shiftRegister != Seed);
//     }
//
//     IEnumerator IEnumerable.GetEnumerator()
//     {
//         return GetEnumerator();
//     }
//
//     public ulong Period { get; set; }
// }