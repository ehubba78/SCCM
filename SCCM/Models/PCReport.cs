namespace SCCM.Models
{
    public class PCReport
    {
        public string Severity { get; set; }
        public string Step { get; set; }
        public string Time { get; set; }
        public string Component { get; set; }
        public string MessageID { get; set; }
        public string Description { get; set; }
    }
}
