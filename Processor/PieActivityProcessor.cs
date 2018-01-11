using BotCommon.Processors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCommon.Activity;
using System.Linq;
using BotCommon.Storage;

namespace PieBot
{
    public class PieActivityProcessor : IActivityProcessor
    {
        private readonly string GeneralContainer = "pie-submissions";
        private readonly string PieBlob = "allthepie";
        
        public async Task<ActivityResponse> ProcessActivityAsync(IStore blobStore, ActivityRequest request)
        {
            PieBotData pies = await this.GetOrCreatePiesAsync(blobStore).ConfigureAwait(false);

            // Preprocess input text
            string[] input = request.SanitizedText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (input.Length == 0)
            {
                return new ActivityResponse("I could not understand the action to take on the pies.");
            }

            string command = input.First().ToLowerInvariant(); // All the commands are in ASCII, so we can use ToLower instead of ToUpper
            IEnumerable<string> arguments = input.Skip(1);

            // Perform the requested command
            DataUpdateResponse response;
            switch (command)
            {
                case "add":
                    response = AddPieAction(pies.KnownPies, arguments);
                    break;
                case "vote":
                case "upvote":
                    response = VotePieAction(pies.KnownPies, request.From, "upvote", (pie) => pie.Upvotes, arguments);
                    break;
                case "dvote":
                case "downvote":
                    response = VotePieAction(pies.KnownPies, request.From, "downvote", (pie) => pie.Downvotes, arguments);
                    break;
                case "clearvote":
                    response = ClearVoteAction(pies.KnownPies, arguments);
                    break;
                case "rank":
                    response = new DataUpdateResponse(false, RankAction(pies.KnownPies, input));
                    break;
                case "list":
                    response = new DataUpdateResponse(false, "**Pies**:\n\n" + string.Join("\n\n", pies.KnownPies.OrderBy(pie => pie.Name).Select(pie => pie.Name)));
                    break;
                case "history":
                    response = new DataUpdateResponse(false, "TODO");
                    break;
                case "help":
                    response = new DataUpdateResponse(false, HelpAction());
                    break;
                default:
                    response = new DataUpdateResponse(false, "I did not understand the action you want me to take on pies!");
                    break;
            }
            
            // Save if necessary and return
            if (response.UpdateData)
            {
                await blobStore.CreateOrUpdateAsync(this.GeneralContainer, this.PieBlob, pies).ConfigureAwait(false);
            }

            return new ActivityResponse(response.Response);
        }

        private DataUpdateResponse VotePieAction(List<Pie> pies, string userName, string voteTypeName, Func<Pie, List<string>> pieVoteTypeSelector, IEnumerable<string> arguments)
        {
            return FindPieWrapper(
                pies,
                arguments,
                voteTypeName,
                (pie) =>
                {
                    bool added;
                    if (pieVoteTypeSelector(pie).Any(vote => vote.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                    {
                        added = false;
                        pieVoteTypeSelector(pie).RemoveAll(vote => vote.Equals(userName, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        added = true;
                        pieVoteTypeSelector(pie).Add(userName);
                    }
                    
                    return new DataUpdateResponse(true, $"{(added ? "Added" : "Removed")} a {voteTypeName} for the '{pie.Name}' pie, which now has (+{pie.Upvotes.Count}, -{pie.Downvotes.Count}) votes.");
                },
                (pieName) => new DataUpdateResponse(false, $"I could not find '{pieName}' pie to {voteTypeName}"));
        }

        private async Task<PieBotData> GetOrCreatePiesAsync(IStore blobStore)
        {
            PieBotData pies;
            if (!await blobStore.ExistsAsync(this.GeneralContainer, this.PieBlob).ConfigureAwait(false))
            {
                pies = new PieBotData()
                {
                    KnownPies = new List<Pie>(),
                    CreatedPies = new Dictionary<DateTime, string>(),
                };
            }
            else
            {
                pies = await blobStore.GetAsync<PieBotData>(this.GeneralContainer, this.PieBlob).ConfigureAwait(false);
            }

            return pies;
        }

        private static DataUpdateResponse AddPieAction(List<Pie> pies, IEnumerable<string> arguments)
        {
            return FindPieWrapper(
                pies,
                arguments,
                "add to the pie suggestion list",
                (pie) => new DataUpdateResponse(false, $"I already have a '{pie.Name}' pie in the request list."),
                (pieName) =>
                {
                    pies.Add(new Pie()
                    {
                        Name = pieName,
                        Upvotes = new List<string>(),
                        Downvotes = new List<string>(),
                    });

                    return new DataUpdateResponse(true, $"I've added a '{pieName}' pie to the request list with zero upvotes and downvotes.");
                });
        }

        private static string RankAction(List<Pie> pies, string[] input)
        {
            string responseText;
            bool detailed = false;
            bool all = false;
            if (input.Length > 1 && input[1].Equals("detailed", StringComparison.OrdinalIgnoreCase))
            {
                detailed = true;
            }
            if (input.Length > 2 && input[2].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                all = true;
            }

            if (!pies.Any(pie => pie.Upvotes.Count != 0 || pie.Downvotes.Count != 0))
            {
                return "No pies currently have upvotes or downvotes. Use **list** to see all pies you can upvote or downvote.";
            }

            responseText = "**Votes**:\n\n" + string.Join("\n\n", pies.Where(pie => (pie.Upvotes.Count != 0 || pie.Downvotes.Count != 0) || all).OrderByDescending(pie => pie.GetTotalVote()).ThenBy(pie => pie.Name).Select(pie => pie.GetNameWithVotes(detailed)));
            return responseText;
        }

        private static DataUpdateResponse ClearVoteAction(List<Pie> pies, IEnumerable<string> arguments)
        {
            return FindPieWrapper(
                pies,
                arguments,
                "clear votes for",
                (pie) =>
                {
                    pie.Downvotes.Clear();
                    pie.Upvotes.Clear();
                    return new DataUpdateResponse(true, $"All votes were cleared from '{pie.Name}' pie");
                },
                (pieName) => new DataUpdateResponse(false, $"I could not find the '{pieName}' pie to clear"));
        }
        
        private static string HelpAction()
        {
            return "PiBot is a distributed pie request submission and voting system, as requested by Helen Huang." +
                   "\n\n **list** --Lists the names of all pies known to *piebot*" +
                   "\n\n **vote NameOfPie** -- Upvotes a pie request. **upvote** is a valid synonym. Voting twice will clear your upvote." +
                   "\n\n **downvote NameOfPie** -- Downvotes a pie request. Downvoting twice will clear your downvote. You are allowed to both upvote and downvote a pie." +
                   "\n\n **rank** -- Lists all pies in ranked order, based on upvotes minus downvotes. Skips pies with no votes." +
                   "\n\n **rank detailed** -- Lists all pies in ranked order, including who voted for and against each pie. Skips pies with no votes." +
                   "\n\n **rank detailed all** -- Lists all pies in ranked order, including who voted for and against each pie. Includes pies with no votes." +
                   "\n\n **history** -- Lists all pies created from these suggestions." +
                   "\n\n **help** -- Displays how to use the PieBot interface. Shows this help text.";
        }

        /// <summary>
        /// Given a series of pies and their arguments, either fails to parse the pie name, finds a pie with the pie name, or doesn't find a pie with the pie name.
        /// </summary>
        private static DataUpdateResponse FindPieWrapper(
            List<Pie> pies,
            IEnumerable<string> arguments,
            string pieNameEmptyMessageAction,
            Func<Pie, DataUpdateResponse> existingPieAction,
            Func<string, DataUpdateResponse> missingPieAction)
        {
            Pie existingPie = null;

            string pieName = string.Join(" ", arguments);
            if (string.IsNullOrWhiteSpace(pieName))
            {
                return new DataUpdateResponse(false, $"I did not receive a pie to {pieNameEmptyMessageAction}.");
            }
            else if ((existingPie = pies.FirstOrDefault(pie => pie.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase))) != null)
            {
                return existingPieAction(existingPie);
            }
            else
            {
                return missingPieAction(pieName);
            }
        }
    }
}