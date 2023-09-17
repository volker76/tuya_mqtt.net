using Markdig.Helpers;

namespace tuya_mqtt.net.Helper
{
    public class StringHelper
    {
        private static char[]? _whiteSpaceChars;
        public static char[] WhiteSpaceChars
        {
            get
            {
                if (_whiteSpaceChars == null)
                {
                    _whiteSpaceChars = DetermineWhiteSpaceChars();
                }
                return _whiteSpaceChars;
            }    
        }

        private static char[] DetermineWhiteSpaceChars()
        {
            List<char> result = new List<char>();
            for (int i = 0; i < 0xffff; i++)
            {
                char c = Convert.ToChar(i);
                if (c.IsWhiteSpaceOrZero())
                {
                    result.Add(c);
                }

            }
            return result.ToArray();
        }

        public static char[] WhiteSpaceCharsPlus(char c)
        {
            List<char> charArray = new List<char>(WhiteSpaceChars);
            charArray.Add(c);
            return charArray.ToArray();
        }

        public static char[] WhiteSpaceCharsPlus(char[] c)
        {
            List<char> charArray = new List<char>(WhiteSpaceChars);
            charArray.AddRange(c);
            return charArray.ToArray();
        }
    }
}
