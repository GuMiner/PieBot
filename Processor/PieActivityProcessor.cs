using BotCommon.Processors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCommon;
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
            bool dataUpdate = false;
            

            string[] input = request.SanitizedText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (input.Length == 0)
            {
                return new ActivityResponse("I could not understand the action to take on the pies.");
            }

            string responseText = string.Empty;
            if (input[0].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                responseText = AddPieAction(pies.KnownPies, ref dataUpdate, input);
            }
            else if (input[0].Equals("vote", StringComparison.OrdinalIgnoreCase) || input[0].Equals("upvote", StringComparison.OrdinalIgnoreCase))
            {
                string pieName = string.Join(" ", input.Skip(1));
                if (string.IsNullOrWhiteSpace(pieName))
                {
                    responseText = "I did not receive a pie to vote on.";
                }
                else if (!pies.KnownPies.Any(pie => pie.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase)))
                {
                    responseText = $"I could not find '{pieName}' pie in the request list.";
                }
                else
                {
                    Pie pie = pies.KnownPies.First(p => p.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase));
                    string userName = request.From;

                    bool added;
                    if (pie.Upvotes.Any(vote => vote.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                    {
                        added = false;
                        pie.Upvotes = pie.Upvotes.Where(vote => !vote.Equals(userName, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                    else
                    {
                        added = true;
                        pie.Upvotes.Add(userName);
                    }

                    dataUpdate = true;
                    responseText = $"{(added ? "Added" : "Removed")} an upvote for the '{pieName}' pie, which now has (+{pie.Upvotes.Count}, -{pie.Downvotes.Count}) votes.";
                }
            }
            else if (input[0].Equals("downvote", StringComparison.OrdinalIgnoreCase))
            {
                string pieName = string.Join(" ", input.Skip(1));
                if (string.IsNullOrWhiteSpace(pieName))
                {
                    responseText = "I did not receive a pie to vote on.";
                }
                else if (!pies.KnownPies.Any(pie => pie.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase)))
                {
                    responseText = $"I could not find '{pieName}' pie in the request list.";
                }
                else
                {
                    Pie pie = pies.KnownPies.First(p => p.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase));
                    string userName = request.From;

                    bool added;
                    if (pie.Downvotes.Any(vote => vote.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                    {
                        added = false;
                        pie.Downvotes = pie.Downvotes.Where(vote => !vote.Equals(userName, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                    else
                    {
                        added = true;
                        pie.Downvotes.Add(userName);
                    }

                    dataUpdate = true;
                    responseText = $"{(added ? "Added" : "Removed")} a downvote for the '{pieName}' pie, which now has (+{pie.Upvotes.Count}, -{pie.Downvotes.Count}) votes.";
                }
            }
            else if (input[0].Equals("rank", StringComparison.OrdinalIgnoreCase))
            {
                responseText = RankAction(pies.KnownPies, input);
            }
            else if (input[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                responseText = "**Pies**:\n\n" + string.Join("\n\n", pies.KnownPies.OrderBy(pie => pie.Name).Select(pie => pie.Name));
            }
            else if (input[0].Equals("clearvote", StringComparison.OrdinalIgnoreCase))
            {
                responseText = ClearVoteAction(pies.KnownPies, ref dataUpdate, input);
            }
            else if (input[0].Equals("history", StringComparison.OrdinalIgnoreCase))
            {
                responseText = "No history yet! WIP!";
            }
            else if (input[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                responseText = HelpAction();
            }
            else if (input[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
            {
                responseText = request.ChannelId + "|" + request.ConversationId + "|" + request.From + "|" + request.FromId + "|" + request.UserId + "|" + request.IsGroup + "|" + request.Recipient;
            }
            else
            {
                responseText = "I did not understand the action to take on pies!";
            }

            if (dataUpdate)
            {
                await blobStore.CreateOrUpdateAsync(this.GeneralContainer, this.PieBlob, pies).ConfigureAwait(false);
            }

            return new ActivityResponse(string.Join("\r\n\r\n", responseText));
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
                // Perform a forwards-conversion of our old List<Pie> data structure into a PieBotData structure, if necessary.
                try
                {
                    pies = await blobStore.GetAsync<PieBotData>(this.GeneralContainer, this.PieBlob).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    List<Pie> listOfPiesOnly = await blobStore.GetAsync<List<Pie>>(this.GeneralContainer, this.PieBlob).ConfigureAwait(false);
                    pies = new PieBotData()
                    {
                        KnownPies = listOfPiesOnly,
                        CreatedPies = new Dictionary<DateTime, string>(),
                    };

                    await blobStore.CreateOrUpdateAsync(this.GeneralContainer, this.PieBlob, pies).ConfigureAwait(false);
                }
            }

            return pies;
        }

        private static string AddPieAction(List<Pie> pies, ref bool dataUpdate, string[] input)
        {
            string responseText;
            string pieName = string.Join(" ", input.Skip(1));
            if (string.IsNullOrWhiteSpace(pieName))
            {
                responseText = "I did not receive a pie to add to the request list.";
            }
            else if (pies.Any(pie => pie.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase)))
            {
                responseText = $"I already have a '{pieName}' pie in the request list.";
            }
            else
            {
                dataUpdate = true;
                pies.Add(new Pie()
                {
                    Name = pieName,
                    Upvotes = new List<string>(),
                    Downvotes = new List<string>(),
                });

                responseText = $"I've added a '{pieName}' pie to the request list with zero upvotes and downvotes";
            }

            return responseText;
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

        private static string HelpAction()
        {
            return "PiBot is a distributed pie request submission and voting system, as requested by Helen Huang." +
                                "\n\n **list** --Lists the names of all pies known to *piebot*" +
                                "\n\n **vote NameOfPie** -- Upvotes a pie request. **upvote** is a valid synonym. Voting twice will clear your upvote." +
                                "\n\n **downvote NameOfPie** -- Downvotes a pie request. Downvoting twice will clear your downvote. You are allowed to both upvote and downvote a pie." +
                                "\n\n **rank** -- Lists all pies in ranked order, based on upvotes minus downvotes. Skips pies with no votes." +
                                "\n\n **rank detailed** -- Lists all pies in ranked order, including who voted for and against each pie. Skips pies with no votes." +
                                "\n\n **rank detailed all** -- Lists all pies in ranked order, including who voted for and against each pie. Includes pies with no votes.";
        }

        private static string ClearVoteAction(List<Pie> pies, ref bool dataUpdate, string[] input)
        {
            string responseText;
            string pieName = string.Join(" ", input.Skip(1));
            if (string.IsNullOrWhiteSpace(pieName))
            {
                responseText = "I did not receive the pie to clear.";
            }
            else if (!pies.Any(pie => pie.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase)))
            {
                responseText = $"I could not find the pie to clear";
            }
            else
            {
                dataUpdate = true;
                Pie pie = pies.First(p => p.Name.Equals(pieName, StringComparison.OrdinalIgnoreCase));
                pie.Downvotes = new List<string>();
                pie.Upvotes = new List<string>();

                responseText = $"All votes were cleared from '{pieName}' pie";
            }

            return responseText;
        }
    }
}