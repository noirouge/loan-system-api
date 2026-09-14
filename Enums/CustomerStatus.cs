namespace LoanSystemAPI.Enums
{
    public enum CustomerStatus : short
    {
        ACTIVE = 1,
        // 2 WAS INACTIVE: REMOVED, A CUSTOMER IS ACTIVE WHEN THEY HAVE AN ACTIVE LOAN
        DELETED = 3
    }
}
