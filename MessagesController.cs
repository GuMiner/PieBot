using System;
using BotCommon.Processors;
using BotCommon;
using System.Configuration;
using BotCommon.Storage;

namespace PieBot
{
    public class MessagesController : ActivityMessagesController
    {
        private static Lazy<IActivityProcessor> activityProcessor = new Lazy<IActivityProcessor>(() =>
        {
            return new PieActivityProcessor();
        });

        public override IActivityProcessor ActivityProcessor { get; } = activityProcessor.Value;

        private static Lazy<IStore> store = new Lazy<IStore>(() =>
        {
            return new AzureBlobStore(ConfigurationManager.AppSettings["AzureStorageConnectionString"], ConfigurationManager.AppSettings["BotContainer"]);
        });

        public override IStore Store { get; } = store.Value;
    }
}