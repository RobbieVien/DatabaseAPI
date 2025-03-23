namespace DatabaseAPI.Models
{
    public class Categorydto
    {
        public int CategoryId { get; set; }
        public string CategoryLegalCase { get; set; }  // Must match alias
        public string CategoryRepublicAct { get; set; } // Must match alias
        public string CategoryNatureCase { get; set; }  // Must match alias
    }
}
