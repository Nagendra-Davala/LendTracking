namespace LendTracking.Model
{
    public class SimpleInterestModel
    {
        public decimal Principal { get; set; }
        public decimal Rate { get; set; } = 24;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    }
}
