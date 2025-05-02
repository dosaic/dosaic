using static NanoidDotNet.Nanoid.Alphabets.SubAlphabets;

namespace Dosaic.Extensions.NanoIds
{
    public static class NanoIdConfig
    {
        public const string Alphabet = NoLookAlikeDigits + NoLookAlikeLetters;

        public static class Lengths
        {
            /// <summary>
            /// Calculated safe lengths for different Nanoid.Alphabets based on this site
            /// https://zelark.github.io/nano-id-cc/
            /// </summary>
            public static class NoLookAlikeDigitsAndLetters
            {
                /// 6 IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L2 = 2;

                /// 48 IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L3 = 3;

                /// 340 IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L4 = 4;

                /// 2K IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L5 = 5;

                /// 16K IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L6 = 6;

                /// 116K IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L7 = 7;

                /// 817K IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L8 = 8;

                /// 5 Million IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L9 = 9;

                /// 40 Millon IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L10 = 10;

                /// 280 Millon IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L11 = 11;

                /// 1 Billion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L12 = 12;

                /// 13 Billion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L13 = 13;

                /// 96 Billion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L14 = 14;

                /// 673 Billion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L15 = 15;

                /// 4 Trillion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L16 = 16;

                /// 32 Trillion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L17 = 17;

                /// 230 Trillion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L18 = 18;

                /// 1616 Trillion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L19 = 19;

                /// 11,312 Trillion IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L20 = 20;

                /// 27,161,781T IDs needed, in order to have a 1% probability of at least one collision.
                public const byte L24 = 24;
            }
        }
    }
}
