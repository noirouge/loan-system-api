namespace LoanSystemAPI.DTOs
{
    // ONLY WHAT A SELECTOR NEEDS, FOR EXAMPLE TO CHOOSE THE counterpartyUserId OF A CASH ENTRY
    public class UserOptionDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Username { get; set; } = "";
    }
}
