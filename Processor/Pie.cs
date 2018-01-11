using System.Collections.Generic;
using System.Linq;

namespace PieBot
{
    /// <summary>
    /// Defines a pie known to <see cref="PieBot"/>
    /// </summary>
    public class Pie
    {
        /// <summary>
        /// The user-friendly name of the pie.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The users who have upvoted this pie.
        /// </summary>
        public List<string> Upvotes { get; set; }

        /// <summary>
        /// The users who have downvoted this pie.
        /// </summary>
        public List<string> Downvotes { get; set; }

        public int GetTotalVote() => this.Upvotes.Count - this.Downvotes.Count;

        /// <summary>
        /// Gets the name of the pie along with votes in a human-friendly way.
        /// </summary>
        /// <param name="detailed">If  true, includes who upvoted and downvoted the pie</param>
        public string GetNameWithVotes(bool detailed)
        {
            string pieSummary = $"  **{this.Name}**: {this.GetTotalVote()} (+{this.Upvotes.Count}) (-{this.Downvotes.Count})";
            if (detailed)
            {
                List<string> summaryDetails = new List<string>();
                if (this.Upvotes.Any())
                {
                    summaryDetails.Add("\n\n*Upvotes:*");
                    for (int i = 0; i < Upvotes.Count; i++)
                    {
                        summaryDetails.Add($"    {this.Upvotes[i]}");
                    }
                }

                if (this.Downvotes.Any())
                {
                    summaryDetails.Add($"{(!this.Upvotes.Any() ? "\n\n" : string.Empty)}*Downvotes:*");
                    for (int i = 0; i < Downvotes.Count; i++)
                    {
                        summaryDetails.Add($"    {this.Downvotes[i]}");
                    }
                }

                pieSummary += string.Join("\n\n", summaryDetails);
            }

            return pieSummary;
        }
    }
}