using BotCommon.Web;
using System.Web.Http;

namespace PieBot
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            CommonWebConfig.Register(config);
        }
    }
}
