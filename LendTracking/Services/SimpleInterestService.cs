using LendTracking.Model;

namespace LendTracking.Services
{
    public class SimpleInterestService
    {
        public SimpleInterestService() { }

        public SimpleInterestResponse CalculateSimpleInterest (SimpleInterestModel model)
        {
            if (model.EndDate <= model.StartDate)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.");
            }
            if(model.Principal <= 0)
            {
                throw new ArgumentException("Principal must be a positive value.");
            }
            if (model.Rate < 0) { 

                throw new ArgumentException("Rate must be a non-negative value.");
            }
            
            decimal principal = model.Principal;
            decimal rate = model.Rate;
            decimal time = (model.EndDate.ToDateTime(new TimeOnly()) - model.StartDate.ToDateTime(new TimeOnly())).Days / 365m;
            decimal interest = (principal * rate * time) / 100;
            return new SimpleInterestResponse
            {
                Interest = interest,
                TotalAmount = principal + interest
            };
        }
    }
}
