namespace PieBot
{
    /// <summary>
    /// Defines a response that may contain a data update.
    /// </summary>
    public class DataUpdateResponse
    {
        public DataUpdateResponse(bool updateData, string response)
        {
            this.UpdateData = updateData;
            this.Response = response;
        }

        public bool UpdateData { get; }

        public string Response { get; }
    }
}