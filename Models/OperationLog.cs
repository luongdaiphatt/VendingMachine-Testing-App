namespace VendingMachineTest.Models
{
    public class OperationLog
    {
        public string Time { get; set; }
        public string ChannelID { get; set; }
        public string CheckStatus { get; set; }
        public string ReleaseStatus { get; set; }
    }
}
