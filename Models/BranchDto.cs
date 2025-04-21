namespace DatabaseAPI.Models
{
    public class BranchDto
    {
        public string BranchName { get; set; }
    }

    public class UpdateBranchDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
    }

}
