namespace LoanSystemAPI.Services
{
    // WHAT A JOB DID IN ONE RUN. Skipped IS NOT AN ERROR: IT IS WORK ALREADY DONE, NORMAL WHEN A PERIOD IS RUN AGAIN
    public class JobRunCounters
    {
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }
}
