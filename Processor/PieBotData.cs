using System;
using System.Collections.Generic;

namespace PieBot
{
    /// <summary>
    /// Defines the aggregate data stored for use in pie bot.
    /// </summary>
    public class PieBotData
    {
        /// <summary>
        /// The list of known pies
        /// </summary>
        public List<Pie> KnownPies { get; set; }

        /// <summary>
        /// A mapping of DateTime->pie name for when pies were created.
        /// </summary>
        public Dictionary<DateTime, string> CreatedPies { get; set; }
    }
}