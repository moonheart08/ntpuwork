using System;
using System.Collections.Generic;
namespace NTPUAssembler
{
    public class Preprocessor
    {
        /// <summary>
        /// Maximum number of define/
        /// </summary>
        public uint MAX_INVOCATIONS = 4000;

        /// <summary>
        /// Dictionary that contains all %define'd words, to handle 
        /// replacements.
        /// </summary>
        public Dictionary<string, string> Defines = new Dictionary<string, string>();

        public Preprocessor()
        {
        }
    }
}
